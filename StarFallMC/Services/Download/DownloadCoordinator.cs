using System.Windows;
using System.Windows.Threading;
using StarFallMC.Entity;
using StarFallMC.Entity.Enum;
using StarFallMC.Navigation;
using StarFallMC.Util;
using MessageBox = StarFallMC.Component.MessageBox;
using MessageBoxResult = StarFallMC.Entity.Enum.MessageBoxResult;

namespace StarFallMC.Services.Download;

/// <summary>
/// Owns the launcher-specific download submission prompts and download-button timer.
/// Queue, worker, cancellation, and transfer state remain owned by <see cref="DownloadManager"/>.
/// </summary>
public sealed class DownloadCoordinator : IDisposable
{
    private readonly object _sync = new();
    private readonly DownloadManager? _manager;
    private readonly LauncherUiCoordinator _uiCoordinator;
    private DispatcherTimer? _hideDownloadButtonTimer;

    public DownloadCoordinator(DownloadManager manager, LauncherUiCoordinator uiCoordinator)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _uiCoordinator = uiCoordinator ?? throw new ArgumentNullException(nameof(uiCoordinator));
    }

    public DownloadCoordinator(LauncherUiCoordinator uiCoordinator)
    {
        _uiCoordinator = uiCoordinator ?? throw new ArgumentNullException(nameof(uiCoordinator));
    }

    public event Action<DownloadProgressSnapshot>? ProgressChanged
    {
        add
        {
            if (_manager != null) _manager.ProgressChanged += value;
        }
        remove
        {
            if (_manager != null) _manager.ProgressChanged -= value;
        }
    }

    public DownloadProgressSnapshot? CurrentSnapshot => _manager?.CurrentSnapshot;

    public IReadOnlyList<DownloadFile> ErrorDownloadFiles => _manager?.FailedFiles ?? Array.Empty<DownloadFile>();

    public bool IsCancel => CurrentSnapshot?.State is DownloadState.Paused or DownloadState.Pausing;

    public bool IsFinished => CurrentSnapshot?.IsTerminal == true;

    public async Task<DownloadBatchResult> StartDownload(
        IReadOnlyCollection<DownloadFile> files,
        CancellationToken cancellationToken = default)
    {
        DownloadBatchHandle? handle = await StartDownloadBatch(files, cancellationToken).ConfigureAwait(false);
        if (handle == null)
        {
            return new DownloadBatchResult(0, 0, 0, 0, Array.Empty<DownloadFileResult>());
        }

        return await handle.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<DownloadBatchHandle?> StartDownloadBatch(
        IReadOnlyCollection<DownloadFile> files,
        CancellationToken cancellationToken = default)
    {
        var mode = DownloadSubmissionMode.Replace;
        var shouldSubmit = true;
        DownloadProgressSnapshot? snapshot = CurrentSnapshot;
        if (snapshot != null &&
            !snapshot.IsTerminal &&
            snapshot.Pending + snapshot.Running > 0 &&
            Application.Current?.MainWindow != null)
        {
            await MessageBox.ShowAsync(
                "下载队列不为空，请选择你想要的操作。\n    1.单独下载：将当前的下载队列取消，只下载当前任务。\n    2.下载：将下载任务追加到正在下载的队列中。\n    3.取消：暂时不下载",
                "提示",
                MessageBoxBtnType.ConfirmAndCancelAndCustom,
                result =>
                {
                    mode = result == MessageBoxResult.Custom
                        ? DownloadSubmissionMode.Append
                        : DownloadSubmissionMode.Replace;
                    shouldSubmit = result is MessageBoxResult.Confirm or MessageBoxResult.Custom;
                },
                customBtnText: "下载",
                confirmBtnText: "单独下载").ConfigureAwait(true);
        }

        if (!shouldSubmit)
        {
            return null;
        }

        return await RequireManager().StartAsync(files, mode, cancellationToken).ConfigureAwait(false);
    }

    public Task<DownloadBatchHandle> AppendDownload(
        IReadOnlyCollection<DownloadFile> files,
        CancellationToken cancellationToken = default) =>
        RequireManager().StartAsync(files, DownloadSubmissionMode.Append, cancellationToken);

    public async Task<DownloadBatchResult> RetryAsync(CancellationToken cancellationToken = default)
    {
        DownloadBatchHandle handle = await RequireManager().RetryFailedAsync(cancellationToken).ConfigureAwait(false);
        return await handle.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task CancelDownload(CancellationToken cancellationToken = default) =>
        RequireManager().PauseAsync(cancellationToken);

    public Task ContinueDownload(CancellationToken cancellationToken = default) =>
        RequireManager().ResumeAsync(cancellationToken);

    public Task ClearDownload(CancellationToken cancellationToken = default) =>
        RequireManager().ClearAsync(cancellationToken);

    public Task<bool> DownloadSingle(DownloadFile file, CancellationToken cancellationToken = default) =>
        RequireManager().DownloadSingleAsync(file, cancellationToken);

    private DownloadManager RequireManager() =>
        _manager ?? throw new InvalidOperationException("The download manager is not configured for this detached view.");

    public void SetTimerToHideDownloadButton(bool hide)
    {
        if (!PropertiesUtil.launcherArgs.ShowDownloadBtn && hide)
        {
            lock (_sync)
            {
                if (_hideDownloadButtonTimer == null)
                {
                    Dispatcher dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
                    _hideDownloadButtonTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
                    {
                        Interval = TimeSpan.FromSeconds(6)
                    };
                    _hideDownloadButtonTimer.Tick += HideDownloadButtonTimerOnTick;
                    _hideDownloadButtonTimer.Start();
                }
            }

            return;
        }

        lock (_sync)
        {
            StopHideDownloadButtonTimer();
        }

        _uiCoordinator.ShowHomeDownloadButton(true);
    }

    private void HideDownloadButtonTimerOnTick(object? sender, EventArgs e)
    {
        lock (_sync)
        {
            StopHideDownloadButtonTimer();
        }

        _uiCoordinator.ShowHomeDownloadButton(false);
    }

    private void StopHideDownloadButtonTimer()
    {
        if (_hideDownloadButtonTimer == null)
        {
            return;
        }

        _hideDownloadButtonTimer.Stop();
        _hideDownloadButtonTimer.Tick -= HideDownloadButtonTimerOnTick;
        _hideDownloadButtonTimer = null;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            StopHideDownloadButtonTimer();
        }
    }
}
