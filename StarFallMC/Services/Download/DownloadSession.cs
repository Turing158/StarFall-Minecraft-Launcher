using System.IO;
using System.Threading.Channels;
using StarFallMC.Entity;

namespace StarFallMC.Services.Download;

public sealed class DownloadSession : IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly DownloadManagerOptions _options;
    private readonly DownloadClient _client;
    private readonly Channel<DownloadWorkItem> _queue;
    private readonly Channel<bool> _progressSignals;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly SemaphoreSlim _admissionGate = new(1, 1);
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly AsyncManualResetEvent _runGate = new();
    private readonly Dictionary<string, DownloadWorkItem> _itemsByTarget = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<DownloadWorkItem> _items = [];
    private readonly List<Task> _producerTasks = [];
    private readonly Task[] _workerTasks;
    private readonly Task _publisherTask;
    private readonly Task _completionCoordinatorTask;
    private readonly TaskCompletionSource<DownloadResult> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource _attemptCts = new();
    private TaskCompletionSource<bool> _noRunning = CompletedSource();
    private DownloadState _state = DownloadState.Created;
    private bool _admissionOpen = true;
    private int _openAdmissions;
    private int _pending;
    private int _running;
    private int _succeeded;
    private int _failed;
    private int _cancelled;
    private long _nextItemId;
    private int _activeWorkers;
    private int _disposed;

    internal DownloadSession(long sessionId, DownloadManagerOptions options, DownloadClient client)
    {
        SessionId = sessionId;
        _options = options;
        _client = client;
        _queue = Channel.CreateBounded<DownloadWorkItem>(new BoundedChannelOptions(Math.Max(1, 2 * options.Concurrency))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = options.Concurrency == 1,
            AllowSynchronousContinuations = false
        });
        _progressSignals = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        _publisherTask = PublishProgressAsync();
        _workerTasks = Enumerable.Range(0, options.Concurrency)
            .Select(index => WorkerAsync(index))
            .ToArray();
        _completionCoordinatorTask = CoordinateCompletionAsync();
    }

    public long SessionId { get; }

    public Task<DownloadResult> Completion => _completion.Task;

    public DownloadState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public int ActiveWorkers => Volatile.Read(ref _activeWorkers);

    public int OpenAdmissions
    {
        get
        {
            lock (_sync)
            {
                return _openAdmissions;
            }
        }
    }

    internal IReadOnlyList<DownloadFile> GetFailedFiles()
    {
        lock (_sync)
        {
            return _items
                .Where(item => item.Status == WorkStatus.Failed)
                .Select(item => item.File)
                .ToArray();
        }
    }

    internal event Action<DownloadProgressSnapshot>? ProgressChanged;

    public DownloadProgressSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return CreateSnapshotLocked();
        }
    }

    internal async Task<DownloadBatchHandle?> AddBatchAsync(
        IReadOnlyCollection<DownloadFile> files,
        bool retryFailed,
        CancellationToken cancellationToken)
    {
        await _admissionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<DownloadWorkItem> admitted = [];
            lock (_sync)
            {
                if (!_admissionOpen || _state is DownloadState.Completing or DownloadState.Completed or
                    DownloadState.Cancelling or DownloadState.Cancelled or DownloadState.Faulted)
                {
                    return null;
                }

                foreach (var file in files)
                {
                    var targetKey = NormalizeTarget(file.FilePath);
                    if (_itemsByTarget.TryGetValue(targetKey, out var existing))
                    {
                        if (retryFailed && existing.Status == WorkStatus.Failed)
                        {
                            admitted.Add(existing);
                        }

                        continue;
                    }

                    if (retryFailed)
                    {
                        continue;
                    }

                    var item = new DownloadWorkItem(
                        Interlocked.Increment(ref _nextItemId),
                        file,
                        targetKey,
                        Path.GetDirectoryName(targetKey) ?? Path.GetPathRoot(targetKey) ?? targetKey);
                    _itemsByTarget.Add(targetKey, item);
                    _items.Add(item);
                    admitted.Add(item);
                    _pending++;
                }

                var handleFiles = admitted.Select(item => item.File).ToArray();
                var handle = new DownloadBatchHandle(handleFiles);
                if (admitted.Count == 0)
                {
                    if (_state == DownloadState.Created && _items.Count == 0)
                    {
                        _state = DownloadState.Completing;
                        _admissionOpen = false;
                        _queue.Writer.TryComplete();
                        SignalProgress();
                    }

                    return handle;
                }

                foreach (var item in admitted)
                {
                    if (item.Status == WorkStatus.Failed)
                    {
                        _failed--;
                        _pending++;
                        item.Status = WorkStatus.Pending;
                        item.BytesDownloaded = 0;
                        item.ContentLength = null;
                        item.File.State = DownloadFile.StateType.Waiting;
                        item.File.ErrorMessage = string.Empty;
                    }

                    item.Batch = handle;
                }

                _openAdmissions++;
                if (_state == DownloadState.Created)
                {
                    _state = DownloadState.Running;
                }

                var producer = ProduceAsync(admitted);
                _producerTasks.Add(producer);
                SignalProgress();
                return handle;
            }
        }
        finally
        {
            _admissionGate.Release();
        }
    }

    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Task noRunning;
            CancellationTokenSource attempt;
            lock (_sync)
            {
                if (_state == DownloadState.Paused)
                {
                    return;
                }

                if (_state != DownloadState.Running)
                {
                    return;
                }

                _state = DownloadState.Pausing;
                _runGate.Reset();
                attempt = _attemptCts;
                noRunning = _noRunning.Task;
                SignalProgress();
            }

            attempt.Cancel();
            await noRunning.WaitAsync(_options.DrainTimeout, cancellationToken).ConfigureAwait(false);

            lock (_sync)
            {
                if (_state == DownloadState.Pausing)
                {
                    _state = DownloadState.Paused;
                }

                SignalProgress();
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_sync)
            {
                if (_state != DownloadState.Paused)
                {
                    return;
                }

                _state = DownloadState.Resuming;
                _attemptCts.Dispose();
                _attemptCts = new CancellationTokenSource();
                _state = DownloadState.Running;
                _runGate.Set();
                SignalProgress();
            }

            await TryEnterCompletingAsync().ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task CancelAndDrainAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _admissionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                List<(DownloadBatchHandle Batch, DownloadFileResult Result)> settlements = [];
                lock (_sync)
                {
                    if (_state is DownloadState.Completed or DownloadState.Cancelled or DownloadState.Faulted)
                    {
                        return;
                    }

                    _state = DownloadState.Cancelling;
                    _admissionOpen = false;
                    _runGate.Set();
                    foreach (var item in _items)
                    {
                        if (item.Status is WorkStatus.Succeeded or WorkStatus.Failed or WorkStatus.Cancelled)
                        {
                            continue;
                        }

                        if (item.Status == WorkStatus.Running)
                        {
                            _running--;
                        }
                        else
                        {
                            _pending--;
                        }

                        item.Status = WorkStatus.Cancelled;
                        item.File.State = DownloadFile.StateType.Waiting;
                        _cancelled++;
                        if (item.Batch != null)
                        {
                            settlements.Add((item.Batch, CancelledResult(item.File, item.BytesDownloaded)));
                        }
                    }

                    CompleteNoRunningIfNeededLocked();
                    SignalProgress();
                }

                foreach (var (batch, result) in settlements)
                {
                    batch.Settle(result);
                }

                _attemptCts.Cancel();
                _lifetimeCts.Cancel();
                _queue.Writer.TryComplete();
            }
            finally
            {
                _admissionGate.Release();
            }

            try
            {
                await _completionCoordinatorTask.WaitAsync(_options.DrainTimeout, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new DownloadDrainException(SessionId, _options.DrainTimeout);
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (!Completion.IsCompleted)
        {
            await CancelAndDrainAsync().ConfigureAwait(false);
        }

        await Completion.ConfigureAwait(false);
        _attemptCts.Dispose();
        _lifetimeCts.Dispose();
        _admissionGate.Dispose();
        _operationGate.Dispose();
    }

    private async Task ProduceAsync(IReadOnlyList<DownloadWorkItem> items)
    {
        await Task.Yield();
        try
        {
            foreach (var item in items)
            {
                await _queue.Writer.WriteAsync(item, _lifetimeCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
        }
        catch (ChannelClosedException)
        {
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Download admission failed: {exception.Message}");
            await FailUnqueuedItemsAsync(items, exception).ConfigureAwait(false);
        }
        finally
        {
            lock (_sync)
            {
                _openAdmissions--;
            }

            await TryEnterCompletingAsync().ConfigureAwait(false);
        }
    }

    private async Task FailUnqueuedItemsAsync(IReadOnlyList<DownloadWorkItem> items, Exception exception)
    {
        foreach (var item in items)
        {
            DownloadBatchHandle? batch = null;
            DownloadFileResult? result = null;
            lock (_sync)
            {
                if (item.Status != WorkStatus.Pending)
                {
                    continue;
                }

                _pending--;
                _failed++;
                item.Status = WorkStatus.Failed;
                item.File.State = DownloadFile.StateType.Error;
                item.File.ErrorMessage = exception.Message;
                batch = item.Batch;
                result = FailedResult(item.File, item.BytesDownloaded, new DownloadErrorSummary(DownloadErrorKind.Unknown, exception.Message));
            }

            batch?.Settle(result!);
        }

        SignalProgress();
        await Task.CompletedTask;
    }

    private async Task WorkerAsync(int workerId)
    {
        Interlocked.Increment(ref _activeWorkers);
        DownloadWorkItem? heldItem = null;
        try
        {
            while (true)
            {
                await _runGate.WaitAsync(_lifetimeCts.Token).ConfigureAwait(false);
                if (heldItem == null)
                {
                    if (!await _queue.Reader.WaitToReadAsync(_lifetimeCts.Token).ConfigureAwait(false))
                    {
                        break;
                    }

                    if (!_queue.Reader.TryRead(out heldItem))
                    {
                        continue;
                    }
                }

                await _runGate.WaitAsync(_lifetimeCts.Token).ConfigureAwait(false);
                if (heldItem is not DownloadWorkItem currentItem)
                {
                    continue;
                }

                if (!TryStart(currentItem, out var attemptToken))
                {
                    if (!IsPending(currentItem))
                    {
                        heldItem = null;
                    }

                    continue;
                }

                try
                {
                    var transfer = await DownloadWithRetriesAsync(currentItem, attemptToken).ConfigureAwait(false);
                    CompleteSucceeded(currentItem, transfer);
                    heldItem = null;
                }
                catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
                {
                    heldItem = null;
                    break;
                }
                catch (OperationCanceledException)
                {
                    ReturnToPendingAfterPause(currentItem);
                }
                catch (DownloadFailureException exception)
                {
                    CompleteFailed(currentItem, exception.ToSummary());
                    heldItem = null;
                }
                catch (Exception exception)
                {
                    CompleteFailed(currentItem, new DownloadErrorSummary(DownloadErrorKind.Unknown, exception.Message));
                    heldItem = null;
                }

                await TryEnterCompletingAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Download worker {workerId} failed: {exception}");
            await FaultSessionAsync(exception).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _activeWorkers);
        }
    }

    private bool TryStart(DownloadWorkItem item, out CancellationToken token)
    {
        lock (_sync)
        {
            if (item.Status != WorkStatus.Pending || _state != DownloadState.Running)
            {
                token = default;
                return false;
            }

            if (_running == 0)
            {
                _noRunning = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            item.Status = WorkStatus.Running;
            item.File.State = DownloadFile.StateType.Downloading;
            _pending--;
            _running++;
            token = _attemptCts.Token;
            SignalProgress();
            return true;
        }
    }

    private bool IsPending(DownloadWorkItem item)
    {
        lock (_sync)
        {
            return item.Status == WorkStatus.Pending;
        }
    }

    private async Task<DownloadTransferResult> DownloadWithRetriesAsync(DownloadWorkItem item, CancellationToken cancellationToken)
    {
        var urls = GetOrderedUrls(item.File);
        if (urls.Count == 0)
        {
            throw new DownloadFailureException(DownloadErrorKind.InvalidUrl, "The download address is empty.", false);
        }

        DownloadFailureException? lastFailure = null;
        var retryLimit = _options.RetryCount;
        for (var mirrorIndex = 0; mirrorIndex < urls.Count; mirrorIndex++)
        {
            var uri = urls[mirrorIndex];
            item.File.UrlPath = uri.ToString();
            for (var retry = 0; retry <= retryLimit; retry++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                item.File.RetryCount = retry;
                try
                {
                    return await _client.DownloadAsync(
                        new DownloadRequest(uri, item.File.FilePath, item.AllowedRoot, item.File.Sha1),
                        (written, length) => UpdateBytes(item, written, length),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (DownloadFailureException exception)
                {
                    lastFailure = exception;
                    if (!exception.IsTransient || retry == retryLimit)
                    {
                        break;
                    }

                    var delay = TimeSpan.FromMilliseconds(Math.Min(2000, 200 * Math.Pow(2, retry)));
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        throw lastFailure ?? new DownloadFailureException(DownloadErrorKind.Unknown, "The download failed.", false);
    }

    private void UpdateBytes(DownloadWorkItem item, long bytesDownloaded, long? contentLength)
    {
        lock (_sync)
        {
            if (item.Status != WorkStatus.Running)
            {
                return;
            }

            item.BytesDownloaded = bytesDownloaded;
            item.ContentLength = contentLength;
            if (contentLength.HasValue && item.File.Size <= 0)
            {
                item.File.Size = contentLength.Value;
            }
        }

        SignalProgress();
    }

    private void CompleteSucceeded(DownloadWorkItem item, DownloadTransferResult transfer)
    {
        DownloadBatchHandle? batch;
        DownloadFileResult result;
        lock (_sync)
        {
            if (item.Status != WorkStatus.Running)
            {
                return;
            }

            _running--;
            _succeeded++;
            item.Status = WorkStatus.Succeeded;
            item.BytesDownloaded = transfer.BytesWritten;
            item.ContentLength = transfer.ContentLength;
            item.File.RetryCount = 0;
            item.File.ErrorMessage = string.Empty;
            item.File.State = DownloadFile.StateType.Finished;
            batch = item.Batch;
            result = new DownloadFileResult(item.File, true, false, transfer.BytesWritten, null);
            CompleteNoRunningIfNeededLocked();
            SignalProgress();
        }

        batch?.Settle(result);
    }

    private void CompleteFailed(DownloadWorkItem item, DownloadErrorSummary error)
    {
        DownloadBatchHandle? batch;
        DownloadFileResult result;
        lock (_sync)
        {
            if (item.Status != WorkStatus.Running)
            {
                return;
            }

            _running--;
            _failed++;
            item.Status = WorkStatus.Failed;
            item.File.ErrorMessage = error.Message;
            item.File.State = DownloadFile.StateType.Error;
            batch = item.Batch;
            result = FailedResult(item.File, item.BytesDownloaded, error);
            CompleteNoRunningIfNeededLocked();
            SignalProgress();
        }

        batch?.Settle(result);
    }

    private void ReturnToPendingAfterPause(DownloadWorkItem item)
    {
        lock (_sync)
        {
            if (item.Status != WorkStatus.Running)
            {
                return;
            }

            _running--;
            _pending++;
            item.Status = WorkStatus.Pending;
            item.BytesDownloaded = 0;
            item.ContentLength = null;
            item.File.State = DownloadFile.StateType.Waiting;
            CompleteNoRunningIfNeededLocked();
            SignalProgress();
        }
    }

    private async Task TryEnterCompletingAsync()
    {
        await _admissionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_sync)
            {
                if (_state is DownloadState.Created or DownloadState.Pausing or DownloadState.Paused or
                    DownloadState.Cancelling or DownloadState.Cancelled or DownloadState.Completed or DownloadState.Faulted)
                {
                    return;
                }

                if (_openAdmissions != 0 || _pending != 0 || _running != 0)
                {
                    return;
                }

                _state = DownloadState.Completing;
                _admissionOpen = false;
                _queue.Writer.TryComplete();
                SignalProgress();
            }
        }
        finally
        {
            _admissionGate.Release();
        }
    }

    private async Task CoordinateCompletionAsync()
    {
        Exception? fault = null;
        try
        {
            await Task.WhenAll(_workerTasks).ConfigureAwait(false);
            Task[] producers;
            lock (_sync)
            {
                producers = _producerTasks.ToArray();
            }

            await Task.WhenAll(producers).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            fault = exception;
        }

        DownloadResult result;
        lock (_sync)
        {
            if (fault != null && _state != DownloadState.Cancelling)
            {
                _state = DownloadState.Faulted;
            }
            else if (_state == DownloadState.Cancelling || _lifetimeCts.IsCancellationRequested)
            {
                _state = DownloadState.Cancelled;
            }
            else
            {
                _state = DownloadState.Completed;
            }

            result = CreateResultLocked();
            SignalProgress();
        }

        _progressSignals.Writer.TryComplete();
        await _publisherTask.ConfigureAwait(false);
        _completion.TrySetResult(result);
    }

    private async Task FaultSessionAsync(Exception exception)
    {
        await _admissionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_sync)
            {
                if (_state is DownloadState.Completed or DownloadState.Cancelled or DownloadState.Faulted)
                {
                    return;
                }

                _state = DownloadState.Faulted;
                _admissionOpen = false;
            }

            _lifetimeCts.Cancel();
            _attemptCts.Cancel();
            _queue.Writer.TryComplete(exception);
        }
        finally
        {
            _admissionGate.Release();
        }
    }

    private async Task PublishProgressAsync()
    {
        await foreach (var signal in _progressSignals.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            while (_progressSignals.Reader.TryRead(out _))
            {
            }

            DownloadProgressSnapshot snapshot;
            lock (_sync)
            {
                snapshot = CreateSnapshotLocked();
            }

            try
            {
                ProgressChanged?.Invoke(snapshot);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Download progress subscriber failed: {exception.Message}");
            }

            if (!snapshot.IsTerminal)
            {
                await Task.Delay(_options.ProgressInterval).ConfigureAwait(false);
            }
        }
    }

    private DownloadProgressSnapshot CreateSnapshotLocked()
    {
        var files = _items.Select(item => new DownloadFileSnapshot(
            item.Id,
            item.File.Name,
            item.File.FilePath,
            item.File.UrlPath,
            item.File.State,
            item.File.Size,
            item.BytesDownloaded,
            item.File.ErrorMessage)).ToArray();
        var knownLengths = _items.All(item => item.ContentLength.HasValue || item.File.Size >= 0);
        long? totalBytes = knownLengths
            ? _items.Sum(item => item.ContentLength ?? Math.Max(0, item.File.Size))
            : null;
        var completedBytes = _items.Sum(item => item.Status == WorkStatus.Succeeded
            ? item.ContentLength ?? item.BytesDownloaded
            : item.BytesDownloaded);
        return new DownloadProgressSnapshot(
            SessionId,
            _state,
            _items.Count,
            _pending,
            _running,
            _succeeded,
            _failed,
            _cancelled,
            totalBytes,
            completedBytes,
            files);
    }

    private DownloadResult CreateResultLocked()
    {
        var files = _items.Select(item => item.Status switch
        {
            WorkStatus.Succeeded => new DownloadFileResult(item.File, true, false, item.BytesDownloaded, null),
            WorkStatus.Cancelled => CancelledResult(item.File, item.BytesDownloaded),
            _ => FailedResult(
                item.File,
                item.BytesDownloaded,
                new DownloadErrorSummary(DownloadErrorKind.Unknown, item.File.ErrorMessage))
        }).ToArray();
        return new DownloadResult(SessionId, _state, _items.Count, _succeeded, _failed, _cancelled, files);
    }

    private void CompleteNoRunningIfNeededLocked()
    {
        if (_running == 0)
        {
            _noRunning.TrySetResult(true);
        }
    }

    private void SignalProgress() => _progressSignals.Writer.TryWrite(true);

    private static TaskCompletionSource<bool> CompletedSource()
    {
        var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.TrySetResult(true);
        return source;
    }

    private static string NormalizeTarget(string path) => Path.GetFullPath(path);

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

    private static DownloadFileResult FailedResult(DownloadFile file, long bytes, DownloadErrorSummary error) =>
        new(file, false, false, bytes, error);

    private static DownloadFileResult CancelledResult(DownloadFile file, long bytes) =>
        new(file, false, true, bytes, new DownloadErrorSummary(DownloadErrorKind.Cancelled, "The download was cancelled."));

    private enum WorkStatus
    {
        Pending,
        Running,
        Succeeded,
        Failed,
        Cancelled
    }

    private sealed class DownloadWorkItem
    {
        public DownloadWorkItem(long id, DownloadFile file, string targetKey, string allowedRoot)
        {
            Id = id;
            File = file;
            TargetKey = targetKey;
            AllowedRoot = allowedRoot;
        }

        public long Id { get; }

        public DownloadFile File { get; }

        public string TargetKey { get; }

        public string AllowedRoot { get; }

        public DownloadBatchHandle? Batch { get; set; }

        public WorkStatus Status { get; set; }

        public long BytesDownloaded { get; set; }

        public long? ContentLength { get; set; }
    }
}
