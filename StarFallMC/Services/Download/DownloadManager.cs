using System.Net;
using System.Net.Http;
using System.IO;
using StarFallMC.Entity;

namespace StarFallMC.Services.Download;

public sealed class DownloadManager : IAsyncDisposable
{
    private readonly DownloadManagerOptions _options;
    private readonly SocketsHttpHandler _handler;
    private readonly HttpClient _httpClient;
    private readonly DownloadClient _client;
    private readonly SemaphoreSlim _sessionGate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<long, Task> _singleDownloads = new();
    private DownloadSession? _currentSession;
    private long _nextSessionId;
    private long _nextSingleDownloadId;
    private bool _disposed;

    public DownloadManager(DownloadManagerOptions? options = null)
    {
        _options = (options ?? new DownloadManagerOptions()).Normalize();
        _handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            UseCookies = true,
            CookieContainer = new CookieContainer(),
            AutomaticDecompression = DecompressionMethods.All,
            MaxConnectionsPerServer = _options.Concurrency,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            ConnectTimeout = _options.ConnectTimeout,
            UseProxy = true
        };
        _httpClient = new HttpClient(_handler, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        _client = new DownloadClient(_httpClient, _options.BufferSize, _options.NoProgressTimeout);
    }

    public event Action<DownloadProgressSnapshot>? ProgressChanged;

    public DownloadSession? CurrentSession => Volatile.Read(ref _currentSession);

    public DownloadProgressSnapshot? CurrentSnapshot => CurrentSession?.GetSnapshot();

    public IReadOnlyList<DownloadFile> FailedFiles => CurrentSession?.GetFailedFiles() ?? Array.Empty<DownloadFile>();

    public async Task<DownloadBatchHandle> StartAsync(
        IReadOnlyCollection<DownloadFile> files,
        DownloadSubmissionMode mode = DownloadSubmissionMode.Replace,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (mode == DownloadSubmissionMode.Replace)
            {
                await PrepareForNewSessionAsync(cancelActive: true, cancellationToken).ConfigureAwait(false);
                return await CreateSessionAndSubmitAsync(files, cancellationToken).ConfigureAwait(false);
            }

            var current = _currentSession;
            if (current != null)
            {
                var handle = await current.AddBatchAsync(files, retryFailed: false, cancellationToken).ConfigureAwait(false);
                if (handle != null)
                {
                    return handle;
                }
            }

            await PrepareForNewSessionAsync(cancelActive: false, cancellationToken).ConfigureAwait(false);
            return await CreateSessionAndSubmitAsync(files, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    public async Task<DownloadBatchHandle> RetryFailedAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = _currentSession;
            if (current == null)
            {
                return new DownloadBatchHandle(Array.Empty<DownloadFile>());
            }

            var failed = FailedFiles;
            if (failed.Count == 0)
            {
                return new DownloadBatchHandle(Array.Empty<DownloadFile>());
            }

            var handle = await current.AddBatchAsync(failed, retryFailed: true, cancellationToken).ConfigureAwait(false);
            if (handle != null)
            {
                return handle;
            }

            await PrepareForNewSessionAsync(cancelActive: false, cancellationToken).ConfigureAwait(false);
            return await CreateSessionAndSubmitAsync(failed, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    public Task PauseAsync(CancellationToken cancellationToken = default) =>
        CurrentSession?.PauseAsync(cancellationToken) ?? Task.CompletedTask;

    public Task ResumeAsync(CancellationToken cancellationToken = default) =>
        CurrentSession?.ResumeAsync(cancellationToken) ?? Task.CompletedTask;

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _sessionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = _currentSession;
            if (current != null)
            {
                await current.CancelAndDrainAsync(cancellationToken).ConfigureAwait(false);
                current.ProgressChanged -= RelayProgress;
                await current.DisposeAsync().ConfigureAwait(false);
                _currentSession = null;
            }
        }
        finally
        {
            _sessionGate.Release();
        }
    }

    public Task<bool> DownloadSingleAsync(DownloadFile file, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var id = Interlocked.Increment(ref _nextSingleDownloadId);
        var task = DownloadSingleCoreAsync(file, cancellationToken);
        _singleDownloads[id] = task;
        _ = task.ContinueWith(
            completedTask => _singleDownloads.TryRemove(id, out _),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return task;
    }

    private async Task<bool> DownloadSingleCoreAsync(DownloadFile file, CancellationToken cancellationToken)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeCts.Token);
        var token = linkedCancellation.Token;
        try
        {
            var urls = GetOrderedUrls(file);
            if (urls.Count == 0)
            {
                file.State = DownloadFile.StateType.Error;
                file.ErrorMessage = "The download address is empty.";
                return false;
            }

            var target = Path.GetFullPath(file.FilePath);
            var allowedRoot = Path.GetDirectoryName(target) ?? Path.GetPathRoot(target) ?? target;
            DownloadFailureException? lastFailure = null;
            foreach (var uri in urls)
            {
                file.UrlPath = uri.ToString();
                for (var retry = 0; retry <= _options.RetryCount; retry++)
                {
                    token.ThrowIfCancellationRequested();
                    file.RetryCount = retry;
                    file.State = DownloadFile.StateType.Downloading;
                    try
                    {
                        var transfer = await _client.DownloadAsync(
                            new DownloadRequest(uri, target, allowedRoot, file.Sha1),
                            (written, length) =>
                            {
                                if (length.HasValue && file.Size <= 0)
                                {
                                    file.Size = length.Value;
                                }
                            },
                            token).ConfigureAwait(false);
                        file.Size = transfer.ContentLength ?? transfer.BytesWritten;
                        file.RetryCount = 0;
                        file.ErrorMessage = string.Empty;
                        file.State = DownloadFile.StateType.Finished;
                        return true;
                    }
                    catch (DownloadFailureException exception)
                    {
                        lastFailure = exception;
                        if (!exception.IsTransient || retry == _options.RetryCount)
                        {
                            break;
                        }

                        await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(2000, 200 * Math.Pow(2, retry))), token).ConfigureAwait(false);
                    }
                }
            }

            file.State = DownloadFile.StateType.Error;
            file.ErrorMessage = lastFailure?.Message ?? "The download failed.";
            return false;
        }
        catch (OperationCanceledException)
        {
            file.State = DownloadFile.StateType.Waiting;
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetimeCts.Cancel();
        await _sessionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_currentSession != null)
            {
                await _currentSession.DisposeAsync().ConfigureAwait(false);
                _currentSession.ProgressChanged -= RelayProgress;
                _currentSession = null;
            }

            await Task.WhenAll(_singleDownloads.Values).ConfigureAwait(false);
        }
        finally
        {
            _sessionGate.Release();
            _sessionGate.Dispose();
            _lifetimeCts.Dispose();
            _httpClient.Dispose();
            _handler.Dispose();
        }
    }

    private async Task<DownloadBatchHandle> CreateSessionAndSubmitAsync(
        IReadOnlyCollection<DownloadFile> files,
        CancellationToken cancellationToken)
    {
        var session = new DownloadSession(Interlocked.Increment(ref _nextSessionId), _options, _client);
        session.ProgressChanged += RelayProgress;
        _currentSession = session;
        var handle = await session.AddBatchAsync(files, retryFailed: false, cancellationToken).ConfigureAwait(false);
        return handle!;
    }

    private async Task PrepareForNewSessionAsync(bool cancelActive, CancellationToken cancellationToken)
    {
        var current = _currentSession;
        if (current == null)
        {
            return;
        }

        if (!current.Completion.IsCompleted)
        {
            if (cancelActive)
            {
                await current.CancelAndDrainAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await current.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        current.ProgressChanged -= RelayProgress;
        await current.DisposeAsync().ConfigureAwait(false);
    }

    private static IReadOnlyList<Uri> GetOrderedUrls(DownloadFile file)
    {
        List<string> values = [];
        if (file.UrlPaths != null)
        {
            values.AddRange(file.UrlPaths.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        if (!string.IsNullOrWhiteSpace(file.UrlPath) && !values.Contains(file.UrlPath, StringComparer.OrdinalIgnoreCase))
        {
            values.Insert(0, file.UrlPath);
        }

        return values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(value => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri : null)
            .Where(uri => uri != null)
            .Cast<Uri>()
            .ToArray();
    }

    private void RelayProgress(DownloadProgressSnapshot snapshot)
    {
        var current = CurrentSession;
        if (current == null || current.SessionId != snapshot.SessionId)
        {
            return;
        }

        ProgressChanged?.Invoke(snapshot);
    }
}
