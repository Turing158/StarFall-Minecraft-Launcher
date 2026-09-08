using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using StarFallMC.Entity;

namespace StarFallMC.Util;

internal sealed class DownloadFilterController : IDisposable {
    private readonly Dispatcher dispatcher;
    private readonly DispatcherTimer refreshTimer;
    private int filterIndex;
    private bool disposed;

    public DownloadFilterController(
        ObservableCollection<DownloadListItem> source,
        Dispatcher dispatcher,
        bool enableLiveFiltering = true,
        TimeSpan? fallbackDelay = null) {
        Source = source;
        this.dispatcher = dispatcher;
        View = CollectionViewSource.GetDefaultView(source);
        View.Filter = MatchesFilter;
        View.CollectionChanged += View_OnCollectionChanged;
        refreshTimer = new DispatcherTimer(DispatcherPriority.DataBind, dispatcher) {
            Interval = fallbackDelay ?? TimeSpan.FromMilliseconds(150)
        };
        refreshTimer.Tick += RefreshTimer_OnTick;

        if (enableLiveFiltering &&
            View is ICollectionViewLiveShaping liveView &&
            liveView.CanChangeLiveFiltering) {
            if (!liveView.LiveFilteringProperties.Contains(nameof(DownloadListItem.State))) {
                liveView.LiveFilteringProperties.Add(nameof(DownloadListItem.State));
            }
            liveView.IsLiveFiltering = true;
            UsesLiveFiltering = liveView.IsLiveFiltering == true;
        }
    }

    public ObservableCollection<DownloadListItem> Source { get; }
    public ICollectionView View { get; }
    public bool UsesLiveFiltering { get; }
    internal int BatchedRefreshCount { get; private set; }

    public event EventHandler? ViewChanged;

    public void SetFilterIndex(int selectedIndex) {
        EnsureDispatcherAccess();
        filterIndex = Math.Clamp(selectedIndex, 0, 4);
        CancelPendingRefresh();
        View.Refresh();
    }

    public void NotifyStateChanged() {
        if (UsesLiveFiltering || disposed) {
            return;
        }

        if (!dispatcher.CheckAccess()) {
            dispatcher.BeginInvoke(NotifyStateChanged, DispatcherPriority.DataBind);
            return;
        }

        refreshTimer.Stop();
        refreshTimer.Start();
    }

    public void CancelPendingRefresh() {
        if (dispatcher.CheckAccess()) {
            refreshTimer.Stop();
        }
        else {
            dispatcher.BeginInvoke(refreshTimer.Stop, DispatcherPriority.DataBind);
        }
    }

    public void Dispose() {
        if (disposed) {
            return;
        }

        disposed = true;
        CancelPendingRefresh();
        View.CollectionChanged -= View_OnCollectionChanged;
        refreshTimer.Tick -= RefreshTimer_OnTick;
    }

    private bool MatchesFilter(object item) {
        if (item is not DownloadListItem download) {
            return false;
        }

        return filterIndex switch {
            1 => download.State == DownloadFile.StateType.Waiting,
            2 => download.State == DownloadFile.StateType.Downloading,
            3 => download.State == DownloadFile.StateType.Finished,
            4 => download.State == DownloadFile.StateType.Error,
            _ => true
        };
    }

    private void RefreshTimer_OnTick(object? sender, EventArgs e) {
        refreshTimer.Stop();
        BatchedRefreshCount++;
        View.Refresh();
    }

    private void View_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureDispatcherAccess() {
        if (!dispatcher.CheckAccess()) {
            throw new InvalidOperationException("Download filters must be changed on the UI dispatcher.");
        }
    }
}
