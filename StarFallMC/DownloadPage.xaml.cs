using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StarFallMC.Entity;
using StarFallMC.Entity.Enum;
using StarFallMC.Util;
using MessageBox = StarFallMC.Component.MessageBox;
using MessageBoxResult = StarFallMC.Entity.Enum.MessageBoxResult;

namespace StarFallMC;

public partial class DownloadPage : Page {

    private ViewModel viewModel = new ViewModel();

    public static Action<List<DownloadFile>,bool> ProgressInit;
    public static Action<DownloadFile,int,int> ProgressUpdate;
    public static Action<bool> DownloadingAnimState;
    public static Action<string,ProcessStatus,bool> ChangeProcessStatus;
    public static Action<string,ProcessStatus,int,string> ChangeProcessStatusWithIndex;
    public static Action<string,bool> ResetProcessStatus;
    public static Func<string,List<string>,bool,string> AppendProcessProgress;
    public static Action<string, Action<ProcessProgress>, bool> ChangeProcessProgressCallback;
    
    private Storyboard DownloadingAnim;
    private Storyboard ListScrollViewerChange;

    private Timer listScrollViewerChangeTimer;
    private List<DownloadFile> TotalDownloads = new ();

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
    public DownloadPage() {
        InitializeComponent();
        DataContext = viewModel;
        DownloadingAnim = (Storyboard)FindResource("DownloadingAnim");
        ListScrollViewerChange = (Storyboard)FindResource("ListScrollViewerChange");
        
        ProgressInit = progressInit;
        ProgressUpdate = progressUpdate;
        DownloadingAnimState = downloadingAnimState;
        ChangeProcessStatus = changeProcessStatus;
        ChangeProcessStatusWithIndex = changeProcessStatusWithIndex;
        ResetProcessStatus = resetProcessStatus;
        AppendProcessProgress = appendProcessProgress;
        ChangeProcessProgressCallback = changeProcessProgressCallback;
        

        OperateBtn.Visibility = Visibility.Collapsed;
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
        
        private ObservableCollection<DownloadFile> _downloads = new ();
        public ObservableCollection<DownloadFile> Downloads {
            get => _downloads;
            set => SetField(ref _downloads, value);
        }

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

    private void progressInit(List<DownloadFile> downloadFiles, bool isClear) {
        if (isClear) {
            viewModel.ProgressText = "0%";
            viewModel.Finished = 0;
            viewModel.ErrorCount = 0;
        }
        else {
            viewModel.ProgressText =
                $"{formatDouble((DownloadUtil.FinishCount + DownloadUtil.errorDownloadFiles.Count == 0 ? 0 : (double)(DownloadUtil.FinishCount + DownloadUtil.errorDownloadFiles.Count) / viewModel.Total) * 100, 2)}%";
        }
        OperateBtn.Visibility = Visibility.Visible;
        viewModel.Total = downloadFiles.Count;
        viewModel.Remaining = downloadFiles.Count - viewModel.Finished;
        DownloadNavigationBar.SelectedIndex = 0;
        TotalDownloads = downloadFiles;
        viewModel.Downloads = new ObservableCollection<DownloadFile>(downloadFiles);
        SetOperateBtn(DownloadUtil.IsCancel);
    }
    
    private void progressUpdate(DownloadFile downloadFile,int FinishCount,int ErrorCount) {
        try {
            viewModel.Finished = FinishCount;
            viewModel.ErrorCount = ErrorCount;
            viewModel.Remaining = viewModel.Total - FinishCount;
            viewModel.ProgressText =
                $"{formatDouble((FinishCount + ErrorCount == 0 ? 0 : (double)(FinishCount + ErrorCount) / viewModel.Total) * 100, 2)}%";
            TotalDownloads[TotalDownloads.FindIndex(i => i.FilePath == downloadFile.FilePath)] = downloadFile;
            if (DownloadNavigationBar.SelectedIndex != 0) {
                var item = viewModel.Downloads.FirstOrDefault(i => i.FilePath == downloadFile.FilePath);
                switch (DownloadNavigationBar.SelectedIndex) {
                    case 1:
                        if (downloadFile.State == DownloadFile.StateType.Waiting) {
                            viewModel.Downloads.Add(downloadFile);
                        }
                        else if (item != null) {
                            viewModel.Downloads.Remove(item);
                        }
                        break;
                    case 2:
                        if (downloadFile.State == DownloadFile.StateType.Downloading) {
                            viewModel.Downloads.Add(downloadFile);
                        }
                        else if (item != null) {
                            viewModel.Downloads.Remove(item);
                        }

                        break;
                    case 3:
                        if (downloadFile.State == DownloadFile.StateType.Finished) {
                            viewModel.Downloads.Add(downloadFile);
                        }
                        else if (item != null) {
                            viewModel.Downloads.Remove(item);
                        }
                        break;
                    case 4:
                        if (downloadFile.State == DownloadFile.StateType.Error) {
                            viewModel.Downloads.Add(downloadFile);
                        }
                        break;
                }
            }
            else {
                int index = viewModel.Downloads.IndexOf(viewModel.Downloads.FirstOrDefault(i =>
                    i.FilePath == downloadFile.FilePath));
                if (index >= 0) {
                    viewModel.Downloads[index] = downloadFile;
                }
                else {
                    viewModel.Downloads.Add(downloadFile);
                }
            }
        }
        catch (Exception e){
            Console.WriteLine(e);
            throw;
        }
        if (viewModel.Downloads.Count == 0) {
            EmptyList.Opacity = 1;
        }
        else {
            EmptyList.Opacity = 0;
        }
    }

    private string formatDouble(double input,int round) {
        if (round == -1) {
            return input.ToString();
        }
        if (round == 0) {
            return Math.Floor(input).ToString();
        }

        if (round > 16) {
            round = 16;
        }
        string format = "0.";
        for (int i = 0; i < round; i++) {
            format += "#";
        }
        return Math.Round(input,round).ToString(format);
    }

    private void downloadingAnimState(bool isStart) {
        if (isStart) {
            DownloadingAnim.RepeatBehavior = RepeatBehavior.Forever;
        }
        else {
            DownloadingAnim.RepeatBehavior = new RepeatBehavior(1);
        }
        DownloadingAnim.Begin();
        
        // 借用方法调整按钮
        CancelAndCleanDownload.Content = DownloadUtil.IsFinished ? "清 空" : "取 消";
        CancelAndCleanDownload.ToolTip = DownloadUtil.IsFinished ? "清空下载列表" : "取消当前所有下载任务";
        
    }

    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        listScrollViewerChangeTimer?.Dispose();
        ListScrollViewerChange.Begin();
        listScrollViewerChangeTimer = new Timer(o => {
            this.Dispatcher.BeginInvoke(() => {
                ChangeDownloadNavi();
                listScrollViewerChangeTimer?.Dispose();
            });
        }, null, 200, 0);
    }

    private void ChangeDownloadNavi() {
        switch (DownloadNavigationBar.SelectedIndex) {
            case 0:
                viewModel.Downloads = new ObservableCollection<DownloadFile>(TotalDownloads);
                break;
            case 1:
                viewModel.Downloads = new ObservableCollection<DownloadFile>(
                    TotalDownloads.Where(i => i.State == DownloadFile.StateType.Waiting));
                break;
            case 2:
                viewModel.Downloads = new ObservableCollection<DownloadFile>(
                    TotalDownloads.Where(i => i.State == DownloadFile.StateType.Downloading));
                break;
            case 3:
                viewModel.Downloads = new ObservableCollection<DownloadFile>(
                    TotalDownloads.Where(i => i.State == DownloadFile.StateType.Finished));
                break;
            case 4:
                viewModel.Downloads = new ObservableCollection<DownloadFile>(
                    DownloadUtil.errorDownloadFiles);
                break;
        }
        if (viewModel.Downloads.Count == 0) {
            EmptyList.Opacity = 1;
        }
        else {
            EmptyList.Opacity = 0;
        }
    }

    private void CancelAndCleanDownload_OnClick(object sender, RoutedEventArgs e) {
        if (DownloadUtil.IsCancel || DownloadUtil.IsFinished) {
            Console.WriteLine("清空下载列表");
            try {
                MessageBox.Show("确定要清除当前的所有下载任务嘛！可能会造成某些事情的出现。", "清除当前下载任务", MessageBoxBtnType.ConfirmAndCancel, r => {
                    if (r == MessageBoxResult.Confirm) {
                        DownloadUtil.ClearDownload();
                        viewModel.Downloads.Clear();
                        viewModel.Total = 0;
                        viewModel.Remaining = 0;
                        viewModel.Finished = 0;
                        viewModel.ErrorCount = 0;
                        // viewModel.Speed = 0;
                        viewModel.ProgressText = "0%";
                        TotalDownloads.Clear();
                        OperateBtn.Visibility = Visibility.Collapsed;
                        SetOperateBtn(DownloadUtil.IsCancel);
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
                DownloadUtil.CancelDownload();
                if (DownloadNavigationBar.SelectedIndex == 2) {
                    viewModel.Downloads.Clear();
                }
                SetOperateBtn(DownloadUtil.IsCancel);
            }
            catch (Exception exception){
                Console.WriteLine(exception);
            }
        }
    }

    private void RetryAndContinueDownload_OnClick(object sender, RoutedEventArgs e) {
        if (DownloadUtil.IsCancel) {
            Console.WriteLine("继续下载");
            try {
                DownloadUtil.ContinueDownload();
            }
            catch (Exception exception){
                Console.WriteLine(exception);
            }
        }
        else {
            if (DownloadUtil.errorDownloadFiles.Count != 0) {
                Console.WriteLine("重试失败任务");
                try {
                    DownloadUtil.RetryDownload();
                    if (DownloadNavigationBar.SelectedIndex == 4) {
                        viewModel.Downloads.Clear();
                    }
                }
                catch (Exception exception){
                    Console.WriteLine(exception);
                }
            }
        }
        SetOperateBtn(DownloadUtil.IsCancel);
        downloadingAnimState(true);
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
    
    public void changeProcessStatus(string key,ProcessStatus status,bool ChangeNextStep) {
        ProcessProgresses.ChangeProcessStatus(key,status,ChangeNextStep);
    }

    public void changeProcessStatusWithIndex(string key,ProcessStatus status,int progressIndex, string progressName = null) {
        ProcessProgresses.ChangeProcessStatusWithIndex(key,status,progressIndex,progressName);
    }
    
    public void resetProcessStatus(string key, bool autoDoingFirst = false) {
        ProcessProgresses.ResetProcessStatus(key,autoDoingFirst);
    }
    
    public string appendProcessProgress(string name, List<string> progressNames,bool autoDoingFirst = false) {
        return ProcessProgresses.AppendProcessProgress(name,progressNames,autoDoingFirst);
    }
    
    public void changeProcessProgressCallback(string key, Action<ProcessProgress> callback, bool isOnDelete = false) {
        ProcessProgresses.ChangeProcessProgressCallback(key,callback, isOnDelete);
    }
    
}