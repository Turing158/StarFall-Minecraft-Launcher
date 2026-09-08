using StarFallMC.Entity;

namespace StarFallMC.Services.Download;

public sealed class DownloadBatchHandle
{
    private readonly object _sync = new();
    private readonly TaskCompletionSource<DownloadBatchResult> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<DownloadFileResult> _results;
    private int _remaining;
    private int _succeeded;
    private int _failed;
    private int _cancelled;

    internal DownloadBatchHandle(IReadOnlyList<DownloadFile> files)
    {
        Files = files;
        _remaining = files.Count;
        _results = new List<DownloadFileResult>(files.Count);
        if (_remaining == 0)
        {
            _completion.TrySetResult(new DownloadBatchResult(0, 0, 0, 0, Array.Empty<DownloadFileResult>()));
        }
    }

    public IReadOnlyList<DownloadFile> Files { get; }

    public Task<DownloadBatchResult> Completion => _completion.Task;

    internal void Settle(DownloadFileResult result)
    {
        DownloadBatchResult? completed = null;
        lock (_sync)
        {
            if (_remaining == 0)
            {
                return;
            }

            _results.Add(result);
            if (result.Success)
            {
                _succeeded++;
            }
            else if (result.Cancelled)
            {
                _cancelled++;
            }
            else
            {
                _failed++;
            }

            _remaining--;
            if (_remaining == 0)
            {
                completed = new DownloadBatchResult(
                    Files.Count,
                    _succeeded,
                    _failed,
                    _cancelled,
                    _results.ToArray());
            }
        }

        if (completed != null)
        {
            _completion.TrySetResult(completed);
        }
    }
}
