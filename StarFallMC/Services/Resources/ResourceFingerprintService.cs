using System.Security.Cryptography;
using System.IO;

namespace StarFallMC.Services.Resources;

/// <summary>
/// Computes the fingerprints used by the resource protocols without buffering a
/// complete archive in memory. MurmurHash2 intentionally uses two passes because
/// its seed contains the whitespace-filtered byte count.
/// </summary>
public sealed class ResourceFingerprintService
{
    public const int DefaultBufferSize = 128 * 1024;
    private const uint MurmurMultiplier = 0x5BD1E995u;

    public Task<string> ComputeSha1Async(string filePath, CancellationToken cancellationToken = default) =>
        Task.Run(() => ComputeSha1(filePath, cancellationToken), cancellationToken);

    public Task<uint> ComputeMurmurHash2Async(string filePath, CancellationToken cancellationToken = default) =>
        Task.Run(() => ComputeMurmurHash2(filePath, cancellationToken), cancellationToken);

    public string ComputeSha1(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        using var sha1 = SHA1.Create();
        using var stream = OpenRead(filePath);
        byte[] buffer = new byte[DefaultBufferSize];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sha1.TransformBlock(buffer, 0, read, buffer, 0);
        }

        cancellationToken.ThrowIfCancellationRequested();
        sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha1.Hash!).ToLowerInvariant();
    }

    public uint ComputeMurmurHash2(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        long filteredLength = CountFilteredBytes(filePath, cancellationToken);
        if (filteredLength > uint.MaxValue)
        {
            throw new OverflowException("The filtered file is too large for the MurmurHash2 length field.");
        }

        uint hash = 1u ^ (uint)filteredLength;
        uint block = 0;
        int blockBytes = 0;
        long consumed = 0;
        using var stream = OpenRead(filePath);
        byte[] buffer = new byte[DefaultBufferSize];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int index = 0; index < read; index++)
            {
                byte value = buffer[index];
                if (IsMurmurWhitespace(value)) continue;

                block |= (uint)value << (blockBytes * 8);
                blockBytes++;
                consumed++;
                if (blockBytes == 4)
                {
                    hash = MixMurmurBlock(hash, block);
                    block = 0;
                    blockBytes = 0;
                }
            }
        }

        if (consumed != filteredLength)
        {
            throw new IOException("The file changed while its MurmurHash2 fingerprint was being calculated.");
        }

        if (blockBytes > 0)
        {
            hash ^= block;
            hash *= MurmurMultiplier;
        }

        hash ^= hash >> 13;
        hash *= MurmurMultiplier;
        hash ^= hash >> 15;
        return hash;
    }

    private static long CountFilteredBytes(string filePath, CancellationToken cancellationToken)
    {
        long length = 0;
        using var stream = OpenRead(filePath);
        byte[] buffer = new byte[DefaultBufferSize];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int index = 0; index < read; index++)
            {
                if (IsMurmurWhitespace(buffer[index])) continue;
                if (length == uint.MaxValue)
                {
                    throw new OverflowException("The filtered file is too large for the MurmurHash2 length field.");
                }
                length++;
            }
        }
        return length;
    }

    private static FileStream OpenRead(string filePath) => new(
        filePath,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        DefaultBufferSize,
        FileOptions.SequentialScan);

    private static bool IsMurmurWhitespace(byte value) => value is 9 or 10 or 13 or 32;

    private static uint MixMurmurBlock(uint hash, uint block)
    {
        block *= MurmurMultiplier;
        block ^= block >> 24;
        block *= MurmurMultiplier;
        hash *= MurmurMultiplier;
        return hash ^ block;
    }
}
