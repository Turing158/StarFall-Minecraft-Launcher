using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.ResourcePages.SubPage;
using StarFallMC.Util;
using StarFallMC.Navigation;
using StarFallMC.Entity.Resource;
using StarFallMC.Services.Download;
using StarFallMC.Services.Resources;
using StarFallMC.Services.Minecraft;

namespace StarFallMC;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window {
    
    private Storyboard SettingEnter;
    private Storyboard SettingLeave;
    private Storyboard DownloadGameEnter;
    private Storyboard DownloadGameLeave;
    private DispatcherTimer? SettingFrameTimer;
    private DispatcherTimer? DownloadGameFrameTimer;
    
    private Storyboard SubFrameShow;
    private Storyboard SubFrameHide;

    private Storyboard DownloadShow;
    private Storyboard DownloadOnlyHide;
    private Storyboard DownloadHide;
    private DispatcherTimer? DownloadFrameTimer;
    
    
    private bool isDraging = false;
    private DoubleAnimation showDrag = new() {
        To = 1,
        Duration = TimeSpan.FromSeconds(0.2),
        EasingFunction = new CubicEase()
    };
    private DoubleAnimation hideDrag = new() {
        To = 0,
        Duration = TimeSpan.FromSeconds(0.2),
        EasingFunction = new CubicEase()
    };
    private CancellationTokenSource? installModPackCts;
    private Task activeModPackInstallTask = Task.CompletedTask;
    private ViewModel viewModel = new ViewModel();
    private readonly Home homePage;
    private readonly Setting settingPage;
    private readonly ResourcePage resourcePage;
    private readonly DownloadPage downloadPage;
    private readonly LauncherUiCoordinator uiCoordinator;
    private readonly DownloadCoordinator downloadCoordinator;
    private readonly DownloadManager? ownedDownloadManager;
    private readonly ResourceWorkflowService resourceWorkflow;
    private readonly ContentNavigationHost subNavigationHost;
    private readonly SemaphoreSlim topLevelNavigationLock = new(1, 1);
    private CancellationTokenSource? topLevelNavigationCts;
    private int currentTopLevelIndex;
    private bool suppressTopLevelSelectionChanged;
    private bool lifecycleCleared;
    private bool closingRequested;
    private bool closingAfterCleanup;

    public MainWindow() : this(new LauncherUiCoordinator(), null) {
    }

    internal MainWindow(
        LauncherUiCoordinator uiCoordinator,
        DownloadCoordinator? downloadCoordinator) {
        
        InitializeComponent();
        this.uiCoordinator = uiCoordinator ?? throw new ArgumentNullException(nameof(uiCoordinator));
        if (downloadCoordinator == null) {
            ownedDownloadManager = new DownloadManager(new DownloadManagerOptions {
                Concurrency = 8,
                RetryCount = 10
            });
            downloadCoordinator = new DownloadCoordinator(ownedDownloadManager, uiCoordinator);
        }
        this.downloadCoordinator = downloadCoordinator;
        resourceWorkflow = new ResourceWorkflowService(uiCoordinator, MinecraftServices.Current);
        uiCoordinator.Register(this);

        DataContext = viewModel;
        homePage = new Home(uiCoordinator);
        settingPage = new Setting(uiCoordinator, downloadCoordinator);
        resourcePage = new ResourcePage(uiCoordinator, downloadCoordinator, resourceWorkflow);
        downloadPage = new DownloadPage(uiCoordinator, downloadCoordinator);
        MainFrame.Content = ContentNavigationHost.CreatePagePresenter(homePage);
        SettingFrame.Content = ContentNavigationHost.CreatePagePresenter(settingPage);
        DownloadGameFrame.Content = ContentNavigationHost.CreatePagePresenter(resourcePage);
        DownloadFrame.Content = ContentNavigationHost.CreatePagePresenter(downloadPage);
        subNavigationHost = new ContentNavigationHost(SubFrame);
        
        SettingEnter = (Storyboard)FindResource("SettingEnter");
        SettingLeave = (Storyboard)FindResource("SettingLeave");
        DownloadGameEnter = (Storyboard)FindResource("DownloadGameEnter");
        DownloadGameLeave = (Storyboard)FindResource("DownloadGameLeave");
        
        SubFrameShow = (Storyboard)FindResource("SubFrameShow");
        SubFrameHide = (Storyboard)FindResource("SubFrameHide");

        DownloadShow = (Storyboard)FindResource("DownloadShow");
        DownloadOnlyHide = (Storyboard)FindResource("DownloadOnlyHide");
        DownloadHide = (Storyboard)FindResource("DownloadHide");
        
        hideDrag.Completed += (_, _) => {
            if (!isDraging) {
                DragFileGrid.IsHitTestVisible = false;
                DragFileGrid.Visibility = Visibility.Collapsed;
            }
        };
    }
    
    public class ViewModel : INotifyPropertyChanged{
        private ObservableCollection<NavigationItem> _tabs = new () {
            new NavigationItem("主 页"),
            new NavigationItem("资 源"),
            new NavigationItem("设 置"),
        };
        public ObservableCollection<NavigationItem> Tabs {
            get => _tabs;
            set => SetField(ref _tabs, value);
        }
        
        private HorizontalAlignment _naviHorizontalAlignment = HorizontalAlignment.Center;
        public HorizontalAlignment NaviHorizontalAlignment {
            get => _naviHorizontalAlignment;
            set {
                NaviMargin = value == HorizontalAlignment.Center
                    ? new Thickness(0, 0, 0, 0)
                    : new Thickness(50, 0, 120, 0);
                SetField(ref _naviHorizontalAlignment, value);
            }
        }
        
        private Thickness _naviMargin = new Thickness(0, 0, 0, 0);
        public Thickness NaviMargin {
            get => _naviMargin;
            set => SetField(ref _naviMargin, value);
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;

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

    private void ShowHome() {
        HideSetting();
        HideDownloadGame();
    }

    private void ShowSetting() {
        SettingFrame.IsHitTestVisible = true;
        StopFrameTimer(ref SettingFrameTimer, SettingFrameTimer_OnTick);
        SettingEnter.Begin(this, true);
        HideDownloadGame();
    }

    private void ShowResourcePage() {
        DownloadGameFrame.IsHitTestVisible = true;
        StopFrameTimer(ref DownloadGameFrameTimer, DownloadGameFrameTimer_OnTick);
        DownloadGameEnter.Begin(this, true);
        HideSetting();
    }

    private async Task NavigateTopLevelAsync(int index) {
        if (closingRequested || lifecycleCleared) {
            return;
        }

        var navigationCts = new CancellationTokenSource();
        var previousCts = Interlocked.Exchange(ref topLevelNavigationCts, navigationCts);
        previousCts?.Cancel();
        var lockAcquired = false;
        try {
            await topLevelNavigationLock.WaitAsync(navigationCts.Token);
            lockAcquired = true;
            navigationCts.Token.ThrowIfCancellationRequested();
            if (closingRequested || lifecycleCleared) {
                return;
            }
            if (index == currentTopLevelIndex) {
                return;
            }

            switch (currentTopLevelIndex) {
                case 0:
                    await homePage.DeactivateAsync();
                    break;
                case 1:
                    await resourcePage.DeactivateAsync();
                    break;
                case 2:
                    await settingPage.DeactivateAsync();
                    break;
            }
            currentTopLevelIndex = -1;

            navigationCts.Token.ThrowIfCancellationRequested();
            if (closingRequested || lifecycleCleared) {
                return;
            }
            switch (index) {
                case 0:
                    await homePage.ActivateAsync(navigationCts.Token);
                    ShowHome();
                    break;
                case 1:
                    await resourcePage.ActivateAsync(navigationCts.Token);
                    ShowResourcePage();
                    break;
                case 2:
                    await settingPage.ActivateAsync(navigationCts.Token);
                    ShowSetting();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index), index, "Unknown top-level page index.");
            }

            currentTopLevelIndex = index;
        }
        catch (OperationCanceledException) when (navigationCts.IsCancellationRequested) {
        }
        finally {
            if (lockAcquired) {
                topLevelNavigationLock.Release();
            }
            Interlocked.CompareExchange(ref topLevelNavigationCts, null, navigationCts);
            navigationCts.Dispose();
        }
    }

    private void HideSetting() {
        SettingFrame.IsHitTestVisible = false;
        SettingLeave.Begin(this, true);
        StartFrameTimer(ref SettingFrameTimer, SettingFrameTimer_OnTick);
    }

    private void HideDownloadGame() {
        DownloadGameFrame.IsHitTestVisible = false;
        DownloadGameLeave.Begin(this, true);
        StartFrameTimer(ref DownloadGameFrameTimer, DownloadGameFrameTimer_OnTick);
    }
    
    private void MiniBtn_OnClick(object sender, RoutedEventArgs e) {
        WindowState = WindowState.Minimized;
    }

    private void CloseBtn_OnClick(object sender, RoutedEventArgs e) {
        PropertiesUtil.Save();
        DoubleAnimation closeAnimation = new DoubleAnimation {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromSeconds(0.2),
            EasingFunction = new CubicEase()
        };
        closeAnimation.Completed += (s, e) => Close();
        MainWindowPage.BeginAnimation(OpacityProperty, closeAnimation);
    }
    
    internal Task SubFrameNavigateAsync(string pageName, string pageTitle) =>
        ShowNamedSubPageAsync(pageName, pageTitle);

    internal async Task ShowNamedSubPageAsync(string pageName, string? pageTitle) {
        SubFrame.RenderTransform = new TranslateTransform(0, 0);
        Title.Text = pageTitle;
        Func<FrameworkElement> pageFactory = pageName.TrimStart('/') switch {
            "SelectGame" => () => new SelectGame(uiCoordinator: uiCoordinator, downloadCoordinator: downloadCoordinator),
            "PlayerManage" => () => new PlayerManage(uiCoordinator),
            _ => throw new ArgumentOutOfRangeException(nameof(pageName), pageName, "Unknown sub page.")
        };
        await subNavigationHost.ShowAsync(pageName, pageFactory);
        SubFrameShow.Begin(this, true);
        SubFrame.IsHitTestVisible = true;
    }

    internal Task ShowGameInfoAsync(MinecraftDownloader downloader, bool loadRemoteData = true) =>
        ShowDetailAsync($"GameInfo:{downloader.Name}", downloader.Name, () => new GameInfo(
            downloader,
            loadRemoteData,
            uiCoordinator: uiCoordinator,
            resourceWorkflow: resourceWorkflow));

    internal Task ShowModInfoAsync(MinecraftResource resource, bool loadRemoteData = true) =>
        ShowDetailAsync($"ModInfo:{resource.DisplayName}", resource.DisplayName, () => new ModInfo(resource, loadRemoteData, uiCoordinator, downloadCoordinator));

    internal Task ShowSaveInfoAsync(SavesResource resource) =>
        ShowDetailAsync($"SaveInfo:{resource.Path}", resource.WorldName, () => new SaveInfo(resource));

    private async Task ShowDetailAsync(string key, string title, Func<FrameworkElement> pageFactory) {
        try {
            SubFrame.RenderTransform = new TranslateTransform(0, 0);
            Title.Text = title;
            await subNavigationHost.ShowAsync(key, pageFactory);
            SubFrameShow.Begin(this, true);
            SubFrame.IsHitTestVisible = true;
        }
        catch (Exception exception) {
            Console.WriteLine(exception);
        }
    }

    private async void BackBtn_OnClick(object sender, RoutedEventArgs e) {
        await BackHandleAsync();
    }

    private async Task BackHandleAsync() {
        if (DownloadFrame.Opacity == 0) {
            try {
                await subNavigationHost.ClearAsync();
            }
            catch (Exception exception) {
                Console.WriteLine(exception);
            }
            SubFrameHide.Completed -= SubFrameHideOnCompleted;
            SubFrameHide.Completed += SubFrameHideOnCompleted;
            SubFrameHide.Begin(this, true);
            SubFrame.IsHitTestVisible = false;
        }
        else {
            if (SubFrame.Opacity == 0) {
                DownloadHide.Begin(this, true);
            }
            else {
                DownloadOnlyHide.Begin(this, true);
            }
            
            if (PropertiesUtil.launcherArgs.ShowDownloadBtn) {
                downloadCoordinator.SetTimerToHideDownloadButton(false);
            }
            else {
                if (downloadCoordinator.IsFinished || uiCoordinator.HasProcessDoing() != true) {
                    downloadCoordinator.SetTimerToHideDownloadButton(true);
                }
                else {
                    downloadCoordinator.SetTimerToHideDownloadButton(false);
                    
                }
            }
            StartFrameTimer(ref DownloadFrameTimer, DownloadFrameTimer_OnTick, TimeSpan.FromMilliseconds(300));
        }
    }
    

    private void SubFrameHideOnCompleted(object? sender, EventArgs e) {
        this.Dispatcher.BeginInvoke(() => {
            SubFrame.RenderTransform = new TranslateTransform(0, SubFrame.ActualHeight + 10);
            SubFrameHide.Completed -= SubFrameHideOnCompleted;
        });
    }

    private void downloadPageShow() {
        DownloadShow.Begin(this, true);
        DownloadFrame.IsHitTestVisible = true;
        Title.Text = "下载 - Download";
        downloadCoordinator.SetTimerToHideDownloadButton(false);
        uiCoordinator.ShowHomeDownloadButton(true);
    }

    private void TopFrame_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        if (WindowState == WindowState.Maximized) {
            WindowState = WindowState.Normal;
        }
        DragMove();
    }
    
    internal async Task ReloadSubFrameAsync(string pageName, Action? action) {
        try {
            await subNavigationHost.ClearAsync();
            await ShowNamedSubPageAsync(pageName, Title.Text);
            action?.Invoke();
        }
        catch (Exception exception) {
            Console.WriteLine(exception);
        }
    }

    private async void OperateGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (suppressTopLevelSelectionChanged) {
            return;
        }

        var index = OperateGrid.SelectedIndex;
        if (homePage.IsGameStarting && index != 0) {
            MessageTips.Show(index == 2 ? "游戏正在启动中，无法进入设置！" : "游戏正在启动中，无法进入资源！", MessageTips.MessageType.Error);
            suppressTopLevelSelectionChanged = true;
            OperateGrid.SelectedIndex = 0;
            suppressTopLevelSelectionChanged = false;
            return;
        }

        try {
            await NavigateTopLevelAsync(index);
        }
        catch (Exception exception) {
            Console.WriteLine(exception);
        }
    }
    
    private void MainWindow_OnPreviewDragOver(object sender, DragEventArgs e) {
        if (!isDraging) {
            isDraging = true;
            DragFileGrid.Visibility = Visibility.Visible;
            DragFileGrid.IsHitTestVisible = true;
            DragFileGrid.BeginAnimation(OpacityProperty, showDrag);
        }
        e.Handled = true;
    }
    
    private async void MainWindow_OnPreviewDrop(object sender, DragEventArgs e) {
        hideDragHandle();
        if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 0 || files.Length > 1) {
                MessageTips.Show("请拖拽单个文件!");
                return;
            }
            if (!DirFileUtil.IsCanCompressFile(files[0])) {
                MessageTips.Show("请拖拽整合包压缩包格式的文件!");
                return;
            }
            
            try {
                await PerpareModPackInstall(files[0]);
            }
            catch (OperationCanceledException) {
            }
            catch (Exception exception) {
                Console.WriteLine(exception);
                MessageTips.Show("整合包安装失败", MessageTips.MessageType.Error);
            }
        }
    }
    
    private async Task PerpareModPackInstall(string filePath) {
        var previousCts = installModPackCts;
        var previousTask = activeModPackInstallTask;
        installModPackCts = null;
        activeModPackInstallTask = Task.CompletedTask;
        previousCts?.Cancel();
        await AwaitShutdownTaskAsync(previousTask);
        previousCts?.Dispose();

        var currentCts = new CancellationTokenSource();
        installModPackCts = currentCts;
        MessageTips.Show("正在校验整合包...");
        var installTask = resourceWorkflow.InstallModPack(filePath, currentCts.Token);
        activeModPackInstallTask = installTask;
        try {
            var result = await installTask;
            Console.WriteLine(result);
        }
        finally {
            if (ReferenceEquals(installModPackCts, currentCts)) {
                installModPackCts = null;
                activeModPackInstallTask = Task.CompletedTask;
                currentCts.Dispose();
            }
        }
    }

    private void MainWindow_OnDragLeave(object sender, DragEventArgs e) {
        hideDragHandle();
        e.Handled = true;
    }

    private async void MainWindow_OnPreviewKeyDown(object sender, KeyEventArgs e) {
        await HandlePreviewKeyDownAsync(e);
    }

    private async Task HandlePreviewKeyDownAsync(KeyEventArgs e) {
        if (e.Key != Key.Escape || (subNavigationHost.CurrentPage == null && DownloadFrame.Opacity == 0)) {
            return;
        }

        e.Handled = true;
        await BackHandleAsync();
    }

    private void hideDragHandle() {
        if (isDraging) {
            isDraging = false;
            DragFileGrid.BeginAnimation(OpacityProperty, hideDrag);
        }
    }

    internal void ShowDownloadPageFromService() => downloadPageShow();

    internal Task BackHandleFromServiceAsync() => BackHandleAsync();

    internal async Task NavigateTopLevelForTestAsync(int index) {
        suppressTopLevelSelectionChanged = true;
        try {
            OperateGrid.SelectedIndex = index;
        }
        finally {
            suppressTopLevelSelectionChanged = false;
        }
        await NavigateTopLevelAsync(index);
    }

    internal Task BackForTestAsync() => BackHandleAsync();

    internal async Task EscapeForTestAsync() {
        await HandlePreviewKeyDownAsync(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this), 0, Key.Escape)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        });
        for (var attempt = 0; attempt < 100 && subNavigationHost.CurrentPage != null; attempt++) {
            await Task.Delay(10);
        }
    }

    internal int CurrentTopLevelIndex => currentTopLevelIndex;
    internal FrameworkElement? CurrentSubPage => subNavigationHost.CurrentPage;
    internal string CurrentPageTitle => Title.Text;
    internal bool IsSubPageVisible => SubFrame.IsHitTestVisible && subNavigationHost.CurrentPage != null;
    internal Setting SettingPage => settingPage;
    internal ResourcePage ResourcePage => resourcePage;

    internal Task ChangeResourceVersionFromServiceAsync() => resourcePage.ChangeVersionAsync();

    private async Task ClearPageHostsAsync() {
        var modPackCts = installModPackCts;
        var modPackTask = activeModPackInstallTask;
        installModPackCts = null;
        activeModPackInstallTask = Task.CompletedTask;
        modPackCts?.Cancel();
        await AwaitShutdownTaskAsync(modPackTask);
        modPackCts?.Dispose();
        topLevelNavigationCts?.Cancel();
        await subNavigationHost.ClearAsync();
        await settingPage.ClearAsync();
        await resourcePage.ClearAsync();
        await downloadPage.DeactivateAsync();
        await homePage.DeactivateAsync();
        if (ownedDownloadManager != null) {
            await ownedDownloadManager.DisposeAsync();
        }
        else if (Application.Current is App app) {
            await app.DisposeDownloadManagerAsync();
        }
        MainFrame.Content = null;
        SettingFrame.Content = null;
        DownloadGameFrame.Content = null;
        DownloadFrame.Content = null;
    }

    private static async Task AwaitShutdownTaskAsync(Task task) {
        try {
            await task;
        }
        catch (OperationCanceledException) {
        }
        catch (Exception exception) {
            Console.WriteLine(exception);
        }
    }

    protected override async void OnClosing(CancelEventArgs e) {
        base.OnClosing(e);
        if (e.Cancel || lifecycleCleared) {
            return;
        }

        e.Cancel = true;
        closingRequested = true;
        if (closingAfterCleanup) {
            return;
        }

        closingAfterCleanup = true;
        try {
            var cleanupTask = ClearPageHostsAsync();
            var completed = await Task.WhenAny(cleanupTask, Task.Delay(TimeSpan.FromSeconds(7)));
            if (completed == cleanupTask) {
                await cleanupTask;
            }
            else {
                Console.WriteLine("Timed out while stopping page work during application shutdown.");
            }
            lifecycleCleared = true;
            await Dispatcher.InvokeAsync(Close, DispatcherPriority.Send);
        }
        catch (Exception exception) {
            closingAfterCleanup = false;
            Console.WriteLine(exception);
        }
    }

    protected override void OnClosed(EventArgs e) {
        installModPackCts?.Cancel();
        StopFrameTimer(ref SettingFrameTimer, SettingFrameTimer_OnTick);
        StopFrameTimer(ref DownloadGameFrameTimer, DownloadGameFrameTimer_OnTick);
        StopFrameTimer(ref DownloadFrameTimer, DownloadFrameTimer_OnTick);
        topLevelNavigationCts?.Cancel();
        topLevelNavigationCts?.Dispose();
        topLevelNavigationLock.Dispose();
        uiCoordinator.Unregister(this);
        downloadCoordinator.Dispose();
        base.OnClosed(e);
    }

    private void StartFrameTimer(ref DispatcherTimer? timer, EventHandler handler, TimeSpan? interval = null) {
        StopFrameTimer(ref timer, handler);
        timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) {
            Interval = interval ?? TimeSpan.FromMilliseconds(200)
        };
        timer.Tick += handler;
        timer.Start();
    }

    private static void StopFrameTimer(ref DispatcherTimer? timer, EventHandler handler) {
        if (timer == null) {
            return;
        }
        timer.Stop();
        timer.Tick -= handler;
        timer = null;
    }

    private void SettingFrameTimer_OnTick(object? sender, EventArgs e) {
        StopFrameTimer(ref SettingFrameTimer, SettingFrameTimer_OnTick);
        SettingFrame.RenderTransform = new TranslateTransform(SettingFrame.ActualWidth + 10, 0);
    }

    private void DownloadGameFrameTimer_OnTick(object? sender, EventArgs e) {
        StopFrameTimer(ref DownloadGameFrameTimer, DownloadGameFrameTimer_OnTick);
        DownloadGameFrame.RenderTransform = new TranslateTransform(DownloadGameFrame.ActualWidth + 10, 0);
    }

    private void DownloadFrameTimer_OnTick(object? sender, EventArgs e) {
        StopFrameTimer(ref DownloadFrameTimer, DownloadFrameTimer_OnTick);
        DownloadFrame.IsHitTestVisible = false;
    }
}
