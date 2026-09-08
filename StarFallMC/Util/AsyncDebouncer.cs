namespace StarFallMC.Util;

/// <summary>
/// Owns a replaceable delayed operation. Replacement cancels and awaits the
/// previous operation before starting the latest request.
/// </summary>
internal sealed class AsyncDebouncer : IAsyncDisposable
{
    private readonly SemaphoreSlim replacementGate = new(1, 1);
    private CancellationTokenSource? operationCts;
    private Task operationTask = Task.CompletedTask;
    private long generation;
    private bool disposed;

    public async Task RunAsync(
        TimeSpan delay,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ObjectDisposedException.ThrowIf(disposed, this);
        long requestGeneration = Interlocked.Increment(ref generation);
        CancellationTokenSource? currentCts = null;
        CancellationToken currentToken = default;
        Task currentTask = Task.CompletedTask;

        await replacementGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var previousCts = operationCts;
            var previousTask = operationTask;
            operationCts = null;
            operationTask = Task.CompletedTask;
            previousCts?.Cancel();
            await ObservePreviousAsync(previousTask).ConfigureAwait(false);
            previousCts?.Dispose();

            if (disposed || requestGeneration != Volatile.Read(ref generation))
            {
                return;
            }

            currentCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            currentToken = currentCts.Token;
            currentTask = ExecuteAsync(delay, action, currentToken);
            operationCts = currentCts;
            operationTask = currentTask;
        }
        finally
        {
            replacementGate.Release();
        }

        try
        {
            await currentTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (currentToken.IsCancellationRequested)
        {
        }
    }

    public async Task CancelAsync()
    {
        Interlocked.Increment(ref generation);
        await replacementGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var currentCts = operationCts;
            var currentTask = operationTask;
            operationCts = null;
            operationTask = Task.CompletedTask;
            currentCts?.Cancel();
            await ObservePreviousAsync(currentTask).ConfigureAwait(false);
            currentCts?.Dispose();
        }
        finally
        {
            replacementGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await CancelAsync().ConfigureAwait(false);
        replacementGate.Dispose();
    }

    private static async Task ExecuteAsync(
        TimeSpan delay,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await action(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ObservePreviousAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
    }
}
