using System.IO;
using StarFallMC.Entity;

namespace StarFallMC.Services.Download;

public enum DownloadState
{
    Created,
    Running,
    Pausing,
    Paused,
    Resuming,
    Completing,
    Completed,
    Cancelling,
    Cancelled,
    Faulted
}

public enum DownloadSubmissionMode
{
    Replace,
    Append
}

public enum DownloadErrorKind
{
    Network,
    Http,
    HashMismatch,
    InvalidPath,
    InvalidUrl,
    Io,
    Cancelled,
    Unknown
}

public sealed record DownloadErrorSummary(
    DownloadErrorKind Kind,
    string Message,
    int? StatusCode = null);

public sealed record DownloadFileSnapshot(
    long Id,
    string Name,
    string FilePath,
    string UrlPath,
    DownloadFile.StateType State,
    long Size,
    long BytesDownloaded,
    string ErrorMessage)
{
    public string FileName => Path.GetFileName(FilePath);

    public string SizeStr => Size < 0
        ? "未知"
        : Size >= 1024L * 1024 * 1024
            ? $"{Size / (1024L * 1024 * 1024):F0} GB"
            : Size >= 1024L * 1024
                ? $"{Size / (1024L * 1024):F0} MB"
                : Size >= 1024
                    ? $"{Size / 1024L:F0} KB"
                    : $"{Size} B";

    public string StateColor => State switch
    {
        DownloadFile.StateType.Waiting => "DarkGoldenrod",
        DownloadFile.StateType.Downloading => "DarkCyan",
        DownloadFile.StateType.Finished => "DarkGreen",
        DownloadFile.StateType.Error => "DarkRed",
        _ => "#f1f1f1"
    };
}

public sealed record DownloadProgressSnapshot(
    long SessionId,
    DownloadState State,
    int Total,
    int Pending,
    int Running,
    int Succeeded,
    int Failed,
    int Cancelled,
    long? TotalBytes,
    long CompletedBytes,
    IReadOnlyList<DownloadFileSnapshot> Files)
{
    public bool IsTerminal => State is DownloadState.Completed or DownloadState.Cancelled or DownloadState.Faulted;
}

public sealed record DownloadFileResult(
    DownloadFile File,
    bool Success,
    bool Cancelled,
    long BytesWritten,
    DownloadErrorSummary? Error);

public sealed record DownloadBatchResult(
    int Total,
    int Succeeded,
    int Failed,
    int Cancelled,
    IReadOnlyList<DownloadFileResult> Files)
{
    public bool Success => Failed == 0 && Cancelled == 0;
}

public sealed record DownloadResult(
    long SessionId,
    DownloadState State,
    int Total,
    int Succeeded,
    int Failed,
    int Cancelled,
    IReadOnlyList<DownloadFileResult> Files);

public sealed record DownloadManagerOptions
{
    public int Concurrency { get; init; } = 8;

    public int RetryCount { get; init; } = 10;

    public int BufferSize { get; init; } = 128 * 1024;

    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(15);

    public TimeSpan NoProgressTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan ProgressInterval { get; init; } = TimeSpan.FromMilliseconds(150);

    public TimeSpan DrainTimeout { get; init; } = TimeSpan.FromSeconds(5);

    internal DownloadManagerOptions Normalize() => this with
    {
        Concurrency = Math.Clamp(Concurrency, 1, 30),
        RetryCount = Math.Clamp(RetryCount, 0, 20),
        BufferSize = Math.Clamp(BufferSize, 64 * 1024, 128 * 1024),
        ConnectTimeout = ConnectTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(15) : ConnectTimeout,
        NoProgressTimeout = NoProgressTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : NoProgressTimeout,
        ProgressInterval = ProgressInterval < TimeSpan.FromMilliseconds(100)
            ? TimeSpan.FromMilliseconds(100)
            : ProgressInterval,
        DrainTimeout = DrainTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : DrainTimeout
    };
}

public sealed class DownloadHashMismatchException : Exception
{
    public DownloadHashMismatchException(string expected, string actual)
        : base($"SHA-1 validation failed. Expected {expected}, actual {actual}.")
    {
        Expected = expected;
        Actual = actual;
    }

    public string Expected { get; }

    public string Actual { get; }
}

public sealed class DownloadDrainException : TimeoutException
{
    public DownloadDrainException(long sessionId, TimeSpan timeout)
        : base($"Download session {sessionId} did not drain within {timeout}.")
    {
        SessionId = sessionId;
    }

    public long SessionId { get; }
}

internal sealed class DownloadFailureException : Exception
{
    public DownloadFailureException(DownloadErrorKind kind, string message, bool isTransient, int? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        IsTransient = isTransient;
        StatusCode = statusCode;
    }

    public DownloadErrorKind Kind { get; }

    public bool IsTransient { get; }

    public int? StatusCode { get; }

    public DownloadErrorSummary ToSummary() => new(Kind, Message, StatusCode);
}
