using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using StarFallMC.Util;

namespace StarFallMC.Services.Download;

internal sealed record DownloadRequest(
    Uri Uri,
    string TargetPath,
    string AllowedRoot,
    string Sha1);

internal sealed record DownloadTransferResult(long BytesWritten, long? ContentLength);

internal sealed class DownloadClient
{
    private readonly HttpClient _httpClient;
    private readonly int _bufferSize;
    private readonly TimeSpan _noProgressTimeout;

    public DownloadClient(HttpClient httpClient, int bufferSize, TimeSpan noProgressTimeout)
    {
        _httpClient = httpClient;
        _bufferSize = bufferSize;
        _noProgressTimeout = noProgressTimeout;
    }

    public async Task<DownloadTransferResult> DownloadAsync(
        DownloadRequest request,
        Action<long, long?>? progress,
        CancellationToken cancellationToken)
    {
        var targetPath = ValidateTargetPath(request.TargetPath, request.AllowedRoot);
        var directory = Path.GetDirectoryName(targetPath)!;
        Directory.CreateDirectory(directory);
        var partialPath = $"{targetPath}.{Guid.NewGuid():N}.part";
        byte[]? buffer = null;

        try
        {
            using var noProgress = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            noProgress.CancelAfter(_noProgressTimeout);
            using var message = new HttpRequestMessage(HttpMethod.Get, request.Uri);
            message.Headers.TryAddWithoutValidation("User-Agent", PropertiesUtil.UserAgent);
            message.Headers.TryAddWithoutValidation("Accept", "*/*");
            using var response = await _httpClient.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                noProgress.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw CreateHttpFailure(response.StatusCode);
            }

            var contentLength = response.Content.Headers.ContentLength;
            progress?.Invoke(0, contentLength);
            await using var responseStream = await response.Content.ReadAsStreamAsync(noProgress.Token).ConfigureAwait(false);
            await using var fileStream = new FileStream(
                partialPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                _bufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var expectedHash = ParseSha1(request.Sha1, targetPath);
            using var hash = expectedHash == null ? null : IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            buffer = ArrayPool<byte>.Shared.Rent(_bufferSize);
            long written = 0;
            while (true)
            {
                var read = await responseStream.ReadAsync(buffer.AsMemory(0, _bufferSize), noProgress.Token).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                noProgress.CancelAfter(_noProgressTimeout);
                await fileStream.WriteAsync(buffer.AsMemory(0, read), noProgress.Token).ConfigureAwait(false);
                hash?.AppendData(buffer, 0, read);
                written += read;
                progress?.Invoke(written, contentLength);
            }

            await fileStream.FlushAsync(noProgress.Token).ConfigureAwait(false);
            fileStream.Close();

            if (expectedHash != null)
            {
                var actualHash = hash!.GetHashAndReset();
                if (!CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
                {
                    throw new DownloadHashMismatchException(
                        Convert.ToHexString(expectedHash).ToLowerInvariant(),
                        Convert.ToHexString(actualHash).ToLowerInvariant());
                }
            }

            File.Move(partialPath, targetPath, overwrite: true);
            return new DownloadTransferResult(written, contentLength);
        }
        catch (DownloadFailureException)
        {
            throw;
        }
        catch (DownloadHashMismatchException exception)
        {
            throw new DownloadFailureException(
                DownloadErrorKind.HashMismatch,
                exception.Message,
                isTransient: true,
                innerException: exception);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DownloadFailureException(
                DownloadErrorKind.Network,
                $"No download progress was received for {_noProgressTimeout}.",
                isTransient: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new DownloadFailureException(
                DownloadErrorKind.Network,
                exception.Message,
                isTransient: true,
                innerException: exception);
        }
        catch (IOException exception)
        {
            throw new DownloadFailureException(
                DownloadErrorKind.Io,
                exception.Message,
                isTransient: true,
                innerException: exception);
        }
        finally
        {
            if (buffer != null)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            TryDeletePartial(partialPath);
        }
    }

    private static string ValidateTargetPath(string targetPath, string allowedRoot)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(targetPath) || !DirFileUtil.IsValidFilePath(targetPath))
            {
                throw new DownloadFailureException(DownloadErrorKind.InvalidPath, "The download target path is invalid.", false);
            }

            var fullPath = Path.GetFullPath(targetPath);
            var fullRoot = Path.GetFullPath(allowedRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var rootPrefix = fullRoot + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new DownloadFailureException(DownloadErrorKind.InvalidPath, "The download target escapes its allowed root.", false);
            }

            return fullPath;
        }
        catch (DownloadFailureException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new DownloadFailureException(DownloadErrorKind.InvalidPath, "The download target path is invalid.", false, innerException: exception);
        }
    }

    private static byte[]? ParseSha1(string sha1, string targetPath)
    {
        if (string.IsNullOrEmpty(sha1))
        {
            return null;
        }

        if (sha1.Length == 40 && sha1.All(Uri.IsHexDigit))
        {
            return Convert.FromHexString(sha1);
        }

        Console.WriteLine($"Ignored an invalid SHA-1 value for '{Path.GetFileName(targetPath)}'.");
        return null;
    }

    private static DownloadFailureException CreateHttpFailure(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        var transient = code >= 500 || statusCode is HttpStatusCode.RequestTimeout or (HttpStatusCode)429;
        return new DownloadFailureException(
            DownloadErrorKind.Http,
            $"The download server returned HTTP {code} ({statusCode}).",
            transient,
            code);
    }

    private static void TryDeletePartial(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Failed to remove partial download '{Path.GetFileName(path)}': {exception.Message}");
        }
    }
}
