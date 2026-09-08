using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using StarFallMC.Entity;
using StarFallMC.Entity.Enum;
using StarFallMC.Navigation;
using StarFallMC.Services.Download;
using StarFallMC.Util;
using MessageBox = StarFallMC.Component.MessageBox;
using MessageBoxResult = StarFallMC.Entity.Enum.MessageBoxResult;

namespace StarFallMC;

public partial class DownloadPage : Page, IPageLifecycle {

    private ViewModel viewModel = new ViewModel();

    private Storyboard DownloadingAnim;
    private Storyboard ListScrollViewerChange;

    private DispatcherTimer? listScrollViewerChangeTimer;
    private readonly DownloadListStore downloadListStore;
    private readonly DownloadFilterController downloadFilterController;
    private readonly LauncherUiCoordinator uiCoordinator;
    private readonly DownloadCoordinator downloadCoordinator;
    private bool _isDownloadSubscribed;
    private bool _isInitialized;

    private DoubleAnimation ValueTo1 = new() {
        To = 1,
        Duration = TimeSpan.FromSeconds(0.2),
        EasingFunction = new CubicEase()
    };
    private DoubleAnimation ValueTo0 = new() {
        To = 0,
        Duration = TimeSpan.FromSeconds(0.2),
        EasingFunction = new CubicEase()
    };
    public DownloadPage(
        LauncherUiCoordinator? uiCoordinator = null,
        DownloadCoordinator? downloadCoordinator = null) {
        this.uiCoordinator = uiCoordinator ?? new LauncherUiCoordinator();
        this.downloadCoordinator = downloadCoordinator ?? new DownloadCoordinator(this.uiCoordinator);
        InitializeComponent();
        DataContext = viewModel;
        downloadListStore = new DownloadListStore(viewModel.Downloads);
        downloadFilterController = new DownloadFilterController(viewModel.Downloads, Dispatcher);
        downloadFilterController.ViewChanged += DownloadFilterController_OnViewChanged;
        DownloadingAnim = (Storyboard)FindResource("DownloadingAnim");
        ListScrollViewerChange = (Storyboard)FindResource("ListScrollViewerChange");
        this.downloadCoordinator.ProgressChanged += OnDownloadProgressChanged;
        _isDownloadSubscribed = true;
        this.uiCoordinator.Register(this);

        OperateBtn.Visibility = Visibility.Collapsed;
        _isInitialized = true;
        ChangeDownloadNavi();
    }

    public class ViewModel : INotifyPropertyChanged {
        public event PropertyChangedEventHandler? PropertyChanged;

        private ObservableCollection<NavigationItem> _downloadStates = new () {
            new NavigationItem("默认"),
            new NavigationItem("待下载"),
            new NavigationItem("下载中"),
            new NavigationItem("下载完成"),
            new NavigationItem("下载失败"),
        };
        public ObservableCollection<NavigationItem> DownloadStates {
            get => _downloadStates;
            set => SetField(ref _downloadStates, value);
        }
        
        public ObservableCollection<DownloadListItem> Downloads { get; } = new();

        private ObservableCollection<ProcessProgress> _progresses = new();
        public ObservableCollection<ProcessProgress> Progresses {
            get => _progresses;
            set => SetField(ref _progresses, value);
        }
        
        private int _speed;
        public int Speed {
            get => _speed;
            set => SetField(ref _speed, value);
        }

        private int _total;
        public int Total {
            get => _total;
            set => SetField(ref _total, value);
        }
        
        private int _finished;
        public int Finished {
            get => _finished;
            set => SetField(ref _finished, value);
        }
        
        private int _errorCount;
        public int ErrorCount {
            get => _errorCount;
            set => SetField(ref _errorCount, value);
        }

        private int _remaining;
        public int Remaining {
            get => _remaining;
            set => SetField(ref _remaining, value);
        }
        
        private string _progressText = "0%";
        public string ProgressText {
            get => _progressText;
            set => SetField(ref _progressText, value);
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    private void ProgressGrid_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        if (ProgressGrid.Opacity == 0) {
            ProgressGrid.BeginAnimation(OpacityProperty, ValueTo1);
        }
        else {
            ProgressGrid.BeginAnimation(OpacityProperty, ValueTo0);
        }
    }

    private void OnDownloadProgressChanged(DownloadProgressSnapshot snapshot) {
        Dispatcher.BeginInvoke(() => ApplySnapshot(snapshot));
    }

    private void ApplySnapshot(DownloadProgressSnapshot snapshot) {
        if (downloadCoordinator.CurrentSnapshot?.SessionId != snapshot.SessionId) {
            return;
        }

        var stateChanged = downloadListStore.Apply(snapshot);
        viewModel.Total = snapshot.Total;
        viewModel.Finished = snapshot.Succeeded;
        viewModel.ErrorCount = snapshot.Failed;
        viewModel.Remaining = snapshot.Pending + snapshot.Running;
        var settled = snapshot.Succeeded + snapshot.Failed + snapshot.Cancelled;
        viewModel.ProgressText = snapshot.Total == 0 ? "0%" : $"{Math.Round(100d * settled / snapshot.Total, 2):0.##}%";
        OperateBtn.Visibility = snapshot.Total == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (stateChanged) {
            downloadFilterController.NotifyStateChanged();
        }
        UpdateEmptyState();
        var paused = snapshot.State is DownloadState.Paused or DownloadState.Pausing;
        SetOperateBtn(paused);
        SetDownloadingAnimation(snapshot.State is DownloadState.Running or DownloadState.Resuming or DownloadState.Pausing);
    }

    private void SetDownloadingAnimation(bool isStart) {
        if (isStart) {
            DownloadingAnim.RepeatBehavior = RepeatBehavior.Forever;
        }
        else {
            DownloadingAnim.RepeatBehavior = new RepeatBehavior(1);
        }
        DownloadingAnim.Begin(this, true);

        // 借用方法调整按钮
        var canClear = downloadCoordinator.IsFinished || downloadCoordinator.IsCancel;
        CancelAndCleanDownload.Content = canClear ? "清 空" : "取 消";
        CancelAndCleanDownload.ToolTip = canClear ? "清空下载列表" : "取消当前所有下载任务";

    }

    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (!_isInitialized) {
            return;
        }

        StopListScrollViewerChangeTimer();
        ListScrollViewerChange.Begin(this, true);
        listScrollViewerChangeTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        listScrollViewerChangeTimer.Tick += ListScrollViewerChangeTimer_OnTick;
        listScrollViewerChangeTimer.Start();
    }

    private void ChangeDownloadNavi() {
        downloadFilterController.SetFilterIndex(DownloadNavigationBar.SelectedIndex);
        UpdateEmptyState();
    }

    private void DownloadFilterController_OnViewChanged(object? sender, EventArgs e) {
        UpdateEmptyState();
    }

    private void UpdateEmptyState() {
        EmptyList.Opacity = downloadFilterController.View.IsEmpty ? 1 : 0;
    }

    private async void CancelAndCleanDownload_OnClick(object sender, RoutedEventArgs e) {
        if (downloadCoordinator.IsCancel || downloadCoordinator.IsFinished) {
            Console.WriteLine("清空下载列表");
            try {
                MessageBox.Show("确定要清除当前的所有下载任务嘛！可能会造成某些事情的出现。", "清除当前下载任务", MessageBoxBtnType.ConfirmAndCancel, async r => {
                    if (r == MessageBoxResult.Confirm) {
                        await downloadCoordinator.ClearDownload();
                        downloadListStore.Clear();
                        viewModel.Total = 0;
                        viewModel.Remaining = 0;
                        viewModel.Finished = 0;
                        viewModel.ErrorCount = 0;
                        // viewModel.Speed = 0;
                        viewModel.ProgressText = "0%";
                        OperateBtn.Visibility = Visibility.Collapsed;
                        SetOperateBtn(downloadCoordinator.IsCancel);
                    }
                });
            }
            catch (Exception exception){
                Console.WriteLine(exception);
            }
        }
        else {
            Console.WriteLine("取消下载");
            try {
                await downloadCoordinator.CancelDownload();
                SetOperateBtn(downloadCoordinator.IsCancel);
            }
            catch (Exception exception){
                Console.WriteLine(exception);
            }
        }
    }

    private async void RetryAndContinueDownload_OnClick(object sender, RoutedEventArgs e) {
        Console.WriteLine("重试或继续下载");
        if (downloadCoordinator.IsCancel) {
            Console.WriteLine("继续下载");
            try {
                await downloadCoordinator.ContinueDownload();
            }
            catch (Exception exception){
                Console.WriteLine(exception);
            }
        }
        else {
            if (downloadCoordinator.ErrorDownloadFiles.Count != 0) {
                Console.WriteLine("重试失败任务");
                try {
                    await downloadCoordinator.RetryAsync();
                }
                catch (Exception exception){
                    Console.WriteLine(exception);
                }
            }
        }
        SetOperateBtn(downloadCoordinator.IsCancel);
        SetDownloadingAnimation(true);
    }

    private void SetOperateBtn(bool isCancel) {
        if (isCancel) {
            CancelAndCleanDownload.Content = "清 空";
            CancelAndCleanDownload.ToolTip = "清空下载列表";
            RetryAndContinueDownload.Content = "继 续";
            RetryAndContinueDownload.ToolTip = "继续剩余的下载任务";
        }
        else {
            CancelAndCleanDownload.Content = "取 消";
            CancelAndCleanDownload.ToolTip = "取消当前所有下载任务";
            RetryAndContinueDownload.Content = "重 试";
            RetryAndContinueDownload.ToolTip = "重试下载失败的任务";
        }
    }
    
    

    private void Download_OnClick(object sender, RoutedEventArgs e) {
        ProcessInfo.BeginAnimation(OpacityProperty, ValueTo0);
        ProcessAllInfo.BeginAnimation(OpacityProperty, ValueTo0);
        ProcessInfo.RenderTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(ProcessInfo.ActualWidth, TimeSpan.FromSeconds(0.2)));
        ProcessAllInfo.RenderTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-ProcessAllInfo.ActualWidth, TimeSpan.FromSeconds(0.2)));
        ProcessInfo.IsHitTestVisible = false;
        ProcessAllInfo.IsHitTestVisible = false;
    }

    private void Progress_OnClick(object sender, RoutedEventArgs e) {
        ProcessInfo.BeginAnimation(OpacityProperty, ValueTo1);
        ProcessAllInfo.BeginAnimation(OpacityProperty, ValueTo1);
        ProcessInfo.RenderTransform.BeginAnimation(TranslateTransform.XProperty, ValueTo0);
        ProcessAllInfo.RenderTransform.BeginAnimation(TranslateTransform.XProperty, ValueTo0);
        ProcessInfo.IsHitTestVisible = true;
        ProcessAllInfo.IsHitTestVisible = true;
    }

    // 关于流程进度的方法
    
    public void ChangeProcessStatus(string key,ProcessStatus status,bool changeNextStep) {
        ProcessProgresses.ChangeProcessStatus(key,status,changeNextStep);
    }

    public void ChangeProcessStatusWithIndex(string key,ProcessStatus status,int progressIndex, string? progressName = null) {
        ProcessProgresses.ChangeProcessStatusWithIndex(key,status,progressIndex,progressName);
    }
    
    public void ResetProcessStatus(string key, bool autoDoingFirst = false) {
        ProcessProgresses.ResetProcessStatus(key,autoDoingFirst);
    }
    
    public string AppendProcessProgress(string name, List<string> progressNames,bool autoDoingFirst = false) {
        return ProcessProgresses.AppendProcessProgress(name,progressNames,autoDoingFirst);
    }
    
    public void ChangeProcessProgressCallback(string key, Action<ProcessProgress> callback, bool isOnDelete = false) {
        ProcessProgresses.ChangeProcessProgressCallback(key,callback, isOnDelete);
    }

    public bool HasProcessDoing() {
        return ProcessProgresses.HasProcessDoing();
    }

    public Task ActivateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_isDownloadSubscribed) {
            downloadCoordinator.ProgressChanged += OnDownloadProgressChanged;
            _isDownloadSubscribed = true;
        }
        uiCoordinator.Register(this);
        var snapshot = downloadCoordinator.CurrentSnapshot;
        if (snapshot != null) {
            ApplySnapshot(snapshot);
        }
        return Task.CompletedTask;
    }

    public Task DeactivateAsync()
    {
        StopListScrollViewerChangeTimer();
        downloadFilterController.CancelPendingRefresh();
        if (_isDownloadSubscribed) {
            downloadCoordinator.ProgressChanged -= OnDownloadProgressChanged;
            _isDownloadSubscribed = false;
        }
        uiCoordinator.Unregister(this);

        return Task.CompletedTask;
    }

    private void ListScrollViewerChangeTimer_OnTick(object? sender, EventArgs e) {
        StopListScrollViewerChangeTimer();
        ChangeDownloadNavi();
    }

    private void StopListScrollViewerChangeTimer() {
        if (listScrollViewerChangeTimer == null) {
            return;
        }
        listScrollViewerChangeTimer.Stop();
        listScrollViewerChangeTimer.Tick -= ListScrollViewerChangeTimer_OnTick;
        listScrollViewerChangeTimer = null;
    }
}
