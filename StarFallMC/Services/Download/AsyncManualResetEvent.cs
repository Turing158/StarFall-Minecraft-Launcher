namespace StarFallMC.Services.Download;

internal sealed class AsyncManualResetEvent
{
    private volatile TaskCompletionSource<bool> _source = CreateSource(completed: true);

    public Task WaitAsync(CancellationToken cancellationToken) => _source.Task.WaitAsync(cancellationToken);

    public void Set() => _source.TrySetResult(true);

    public void Reset()
    {
        while (true)
        {
            var current = _source;
            if (!current.Task.IsCompleted)
            {
                return;
            }

            var replacement = CreateSource(completed: false);
            if (ReferenceEquals(Interlocked.CompareExchange(ref _source, replacement, current), current))
            {
                return;
            }
        }
    }

    private static TaskCompletionSource<bool> CreateSource(bool completed)
    {
        var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (completed)
        {
            source.TrySetResult(true);
        }

        return source;
    }
}
