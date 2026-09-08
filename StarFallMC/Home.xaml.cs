using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.Entity.Enum;
using StarFallMC.Util;
using StarFallMC.Navigation;
using StarFallMC.Services;
using StarFallMC.Services.Minecraft;
using StarFallMC.Services.Resources;
using MessageBox = StarFallMC.Component.MessageBox;
using MessageBoxResult = StarFallMC.Entity.Enum.MessageBoxResult;

namespace StarFallMC;

public partial class Home : Page, IPageLifecycle {

    private ViewModel viewModel = new ViewModel();
    private readonly MinecraftServiceContainer minecraftServices;
    private readonly LauncherUiCoordinator uiCoordinator;

    private Storyboard Downloading;
    private readonly DispatcherTimer backgroundResizeTimer;
    private readonly Dictionary<string, long> resourceImageVersions = new();
    private CancellationTokenSource backgroundLoadCts = new();
    private CancellationTokenSource resourceImageLoadCts = new();
    private Task backgroundReloadTask = Task.CompletedTask;
    private Task resourceVersionChangeTask = Task.CompletedTask;
    private Task gameIconLoadTask = Task.CompletedTask;
    private Task playerSkinLoadTask = Task.CompletedTask;
    private Window? hostWindow;
    private string? loadedBackgroundRequestSource;
    private int loadedBackgroundWidth;
    private int loadedBackgroundHeight;
    private long backgroundRequestVersion;
    
    internal bool IsGameStarting { get; private set; }
    
    public Home(LauncherUiCoordinator? uiCoordinator = null, MinecraftServiceContainer? services = null) {
        this.uiCoordinator = uiCoordinator ?? new LauncherUiCoordinator();
        minecraftServices = services ?? MinecraftServices.Current;
        
        InitializeComponent();
        
        DataContext = viewModel;

        backgroundResizeTimer = new DispatcherTimer(DispatcherPriority.Background) {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        backgroundResizeTimer.Tick += BackgroundResizeTimer_OnTick;

        viewModel.PlayerName = "";
        
        this.uiCoordinator.Register(this);
        
        
        Downloading = (Storyboard)FindResource("Downloading");

        DownloadBtn.Visibility =
            PropertiesUtil.launcherArgs.ShowDownloadBtn ? Visibility.Visible : Visibility.Collapsed;
        
        var savedGame = ApplicationState.GameSelection.CurrentGame;
        setGameInfo(savedGame is { Name: { Length: > 0 } } ? savedGame : null, notifyResourceChange: false);

        var (player, players) = PropertiesUtil.loadPlayers();
        setPlayerFunc(player);
        
    }
    
    public class ViewModel : INotifyPropertyChanged {
        
        private string _playerName = string.Empty;
        public string PlayerName {
            get => _playerName;
            set => SetField(ref _playerName, value);
        }

        private MinecraftItem _currentGame = new("未选择版本", MinecraftLoader.Unknown, string.Empty, "/assets/DefaultGameIcon/unknowGame.png");
        public MinecraftItem CurrentGame {
            get => _currentGame;
            set => SetField(ref _currentGame, value);
        }
        
        private Player? _currentPlayer;
        public Player? CurrentPlayer {
            get => _currentPlayer;
            set => SetField(ref _currentPlayer, value);
        }
        
        private bool _isDownloading;
        public bool IsDownloading {
            get => _isDownloading;
            set => SetField(ref _isDownloading, value);
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

    private async void CurrentGame_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        await uiCoordinator.NavigateSubPageAsync("SelectGame", "Minecraft - 我的世界");
    }

    private async void CurrentPlayer_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        await uiCoordinator.NavigateSubPageAsync("PlayerManage", "Players - 玩家");
    }
    

    private void setGameInfo(MinecraftItem? item, bool notifyResourceChange = true) {
        string iconPath = "/assets/DefaultGameIcon/unknowGame.png";
        if (item == null) {
            GameName.Text = "未选择版本";
            viewModel.CurrentGame = new MinecraftItem("未选择版本",MinecraftLoader.Unknown,"","/assets/DefaultGameIcon/unknowGame.png");
        } else {
            GameName.Text = item.Name;
            iconPath = item.Icon;
            viewModel.CurrentGame = item;
        }
        if (!iconPath.Contains(":")) {
            iconPath = "pack://application:,,,/StarFallMC;component"+iconPath;
        }
        ResourceServices.Current.Cache.Clear();
        ApplicationState.GameSelection.CurrentGame = viewModel.CurrentGame;
        if (notifyResourceChange) {
            resourceVersionChangeTask = uiCoordinator.ChangeResourceVersionAsync();
        }
        
        gameIconLoadTask = UpdateBitmapImageAsync(
            "CurrentGameIcon",
            iconPath,
            "pack://application:,,,/StarFallMC;component/assets/DefaultGameIcon/unknowGame.png");
        Console.WriteLine(item);
    }
    
    private void setPlayerFunc(Player player) {
        Console.WriteLine("当前玩家名称："+player.Name);
        string skin;
        if (string.IsNullOrEmpty(player.Name)) {
            viewModel.PlayerName = "未登录";
            skin = PlayerManage.DefaultSKin;
            viewModel.CurrentPlayer = null;
        } else {
            viewModel.PlayerName = player.Name;
            skin = player.Skin;
            viewModel.CurrentPlayer = player;
        }
        playerSkinLoadTask = UpdateBitmapImageAsync("PlayerSkin", skin, PlayerManage.DefaultSKin);
    }
    
    private async Task UpdateBitmapImageAsync(string resourceKey, string source, string fallbackSource) {
        var requestVersion = resourceImageVersions.TryGetValue(resourceKey, out var currentVersion)
            ? currentVersion + 1
            : 1;
        resourceImageVersions[resourceKey] = requestVersion;
        source = ResolveImageSource(source, fallbackSource);

        try {
            var image = await ImageLoader.LoadSourceAsync(source, 256, 256, resourceImageLoadCts.Token);
            if (ImageLoader.IsPlaceholder(image) && !string.Equals(source, fallbackSource, StringComparison.Ordinal)) {
                image = await ImageLoader.LoadSourceAsync(
                    fallbackSource,
                    256,
                    256,
                    resourceImageLoadCts.Token);
            }

            if (!resourceImageLoadCts.IsCancellationRequested &&
                resourceImageVersions.TryGetValue(resourceKey, out var latestVersion) &&
                latestVersion == requestVersion &&
                !ImageLoader.IsPlaceholder(image)) {
                Application.Current.Resources[resourceKey] = image;
            }
        }
        catch (OperationCanceledException) {
        }
    }

    private static string ResolveImageSource(string? source, string fallbackSource) {
        if (string.IsNullOrWhiteSpace(source)) {
            return fallbackSource;
        }

        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "pack" || uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) {
            return source;
        }

        return Path.IsPathRooted(source) && !File.Exists(source) ? fallbackSource : source;
    }
    
    private CancellationTokenSource? minecraftStartCts;
    private Task<MinecraftLaunchResult> minecraftStartTask = Task.FromResult(new MinecraftLaunchResult(true, false));
    private async void StartGameBtn_OnClick(object sender, RoutedEventArgs e) {
        if (!IsGameStarting) {
            bool flag = true;
            StringBuilder tips = new StringBuilder();
            if (viewModel.CurrentGame == null || viewModel.CurrentGame.Name == "未选择版本") {
                tips.Append("未选择Minecraft版本");
                ((Storyboard)FindResource("GameEnter")).Begin(this, true);
                flag = false;
            }
            if (string.IsNullOrEmpty(viewModel.PlayerName) || viewModel.PlayerName == "未登录") {
                if (!string.IsNullOrEmpty(tips.ToString())) {
                    tips.Append("\n");
                }
                tips.Append("未选择Player角色");
                ((Storyboard)FindResource("AvatarEnter")).Begin(this, true);
                flag = false;
            }
            if (!string.IsNullOrEmpty(tips.ToString())) {
                MessageTips.Show(tips.ToString(), MessageTips.MessageType.Warning);
            }
            if (minecraftServices.Java.DiscoverInstalled().Count == 0 && ApplicationState.GameSettings.JavaVersions.Count <= 1) {
                MessageBox.Show("未检测到系统安装的Java版本。\n    1.请前往设置或Oracle官网下载！\n    2.前往设置自行添加Java版本", "未检测到Java版本");
                flag = false;
            }
            if (!flag) {
                return;
            }
            if (viewModel.CurrentPlayer is not { } currentPlayer) {
                return;
            }
            if (viewModel.CurrentGame is not { } currentGame) {
                return;
            }
            await CancelMinecraftStartAsync();
            var currentCts = new CancellationTokenSource();
            minecraftStartCts = currentCts;
            IsGameStarting = true;
            StartingBorder.Visibility = Visibility.Visible;
            ((Storyboard)FindResource("Starting")).Begin(this, true);
            HomeTips.Show();
            Console.WriteLine("开始游戏");
            currentPlayer.AccessToken = "00000FFFFFFFFFFFFFFFFFFFFFF1414F";
            string currentDir = DirFileUtil.GetParentPath(DirFileUtil.GetParentPath(currentGame.Path));
            minecraftStartTask = minecraftServices.Launch.StartAsync(
                new MinecraftLaunchRequest(
                    currentGame,
                    currentPlayer,
                    ApplicationState.GameSettings,
                    currentDir,
                    PropertiesUtil.LauncherName,
                    PropertiesUtil.LauncherVersion,
                    AuthenticateAsync: async (player, token) => (Player?)await LoginUtil.RefreshMicrosoftToken(player, token)),
                currentCts.Token);
            try {
                var result = await minecraftStartTask;
                if (!result.Success && !result.Cancelled) await HideLaunchingAsync(false);
            }
            catch (Exception exception) {
                Console.WriteLine(exception);
                await HideLaunchingAsync(false);
            }
            finally {
                if (ReferenceEquals(minecraftStartCts, currentCts)) {
                    minecraftStartCts = null;
            minecraftStartTask = Task.FromResult(new MinecraftLaunchResult(true, false));
                    currentCts.Dispose();
                }
            }
        }
    }

    private async void StartingBtn_OnClick(object sender, RoutedEventArgs e) {
        await HideLaunchingAsync(true);
    }

    private async Task HideLaunchingAsync(bool isStop = false) {
        if (isStop) {
            await CancelMinecraftStartAsync();
        }

        await Dispatcher.InvokeAsync(() => {
            try {
                IsGameStarting = false;
                StartingBorder.Visibility = Visibility.Collapsed;
                HomeTips.Hide();
                ((Storyboard)FindResource("Started")).Begin(this, true);
            }
            catch (Exception e){
                Console.WriteLine(e);
            }
        });

        if (isStop) {
            await minecraftServices.Launch.StopAsync();
        }
    }

    private void errorLaunch(MinecraftItem item) {
        Dispatcher.Invoke(() => {
            MessageBox.Show(
                content:
                $"当前版本：{item.Name} 出现游戏崩溃！无法正常运行，崩溃可能由多种原因引起，以下为常见解决方案：\n    1.检查Minecraft内存分配是否合理\n    2.检查Java版本是否能够当前Minecraft的启动\n    3.检查Minecraft模组中是否存在模组冲突\n    4.查看崩溃日志文件，若有需要，建议保存",
                title: "Minecraft 运行失败",
                btnType: MessageBoxBtnType.ConfirmAndCustom,
                customBtnText: "查看日志",
                callback: r => {
                    if (r == MessageBoxResult.Custom) {
                        DirFileUtil.openDirByExplorer(DirFileUtil.GetParentPath(DirFileUtil.GetParentPath(item.Path)));
                    }
                });
        });
    }

    private void DownloadBtn_OnClick(object sender, RoutedEventArgs e) {
        uiCoordinator.ShowDownloadPage();
        DownloadBtn.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty,mouseUpAnimation);
        DownloadBtn.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty,mouseUpAnimation);
    }
    
    private void downloadState(bool isDownloading) {
        if (isDownloading) {
            Downloading.RepeatBehavior = RepeatBehavior.Forever;
        }
        else {
            Downloading.RepeatBehavior = new RepeatBehavior(1);
        }
        Downloading.Begin(this, true);
    }

    private void startingState(string state) {
        Dispatcher.BeginInvoke(() => {
            StatusText.Text = state;
        });
    }

    private async void Home_OnLoaded(object sender, RoutedEventArgs e) {
        AttachHostWindow();
        backgroundReloadTask = ReloadBackgroundAsync(force: false);
        await backgroundReloadTask;
    }

    private void Home_OnSizeChanged(object sender, SizeChangedEventArgs e) {
        if (IsLoaded) {
            ScheduleBackgroundResize();
        }
    }

    private void HomeWindow_OnDpiChanged(object sender, DpiChangedEventArgs e) {
        ScheduleBackgroundResize();
    }

    private async void BackgroundResizeTimer_OnTick(object? sender, EventArgs e) {
        backgroundResizeTimer.Stop();
        backgroundReloadTask = ReloadBackgroundAsync(force: false);
        await backgroundReloadTask;
    }

    private void ScheduleBackgroundResize() {
        backgroundResizeTimer.Stop();
        backgroundResizeTimer.Start();
    }

    private async Task ReloadBackgroundAsync(bool force) {
        var requestedSource = ResolveConfiguredBackgroundSource();
        if (string.IsNullOrWhiteSpace(requestedSource)) {
            CancelBackgroundLoad();
            Bg.Background = null;
            loadedBackgroundRequestSource = null;
            loadedBackgroundWidth = 0;
            loadedBackgroundHeight = 0;
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var target = CalculateBackgroundDecodeSize(
            Bg.ActualWidth > 0 ? Bg.ActualWidth : ActualWidth,
            Bg.ActualHeight > 0 ? Bg.ActualHeight : ActualHeight,
            dpi.DpiScaleX,
            dpi.DpiScaleY);
        if (target.Width <= 0 || target.Height <= 0) {
            return;
        }

        if (!force &&
            string.Equals(requestedSource, loadedBackgroundRequestSource, StringComparison.Ordinal) &&
            !ShouldReloadBackground(
                loadedBackgroundWidth,
                loadedBackgroundHeight,
                target.Width,
                target.Height)) {
            return;
        }

        CancelBackgroundLoad();
        var token = backgroundLoadCts.Token;
        var requestVersion = Interlocked.Increment(ref backgroundRequestVersion);
        try {
            var image = await ImageLoader.LoadBackgroundAsync(
                requestedSource,
                target.Width,
                target.Height,
                Math.Max(dpi.DpiScaleX, dpi.DpiScaleY),
                token);

            if (ImageLoader.IsPlaceholder(image)) {
                var defaultSource = FindDefaultBackgroundSource();
                if (!string.IsNullOrWhiteSpace(defaultSource) &&
                    !string.Equals(defaultSource, requestedSource, StringComparison.OrdinalIgnoreCase)) {
                    image = await ImageLoader.LoadBackgroundAsync(
                        defaultSource,
                        target.Width,
                        target.Height,
                        Math.Max(dpi.DpiScaleX, dpi.DpiScaleY),
                        token);
                }
            }

            if (token.IsCancellationRequested ||
                requestVersion != Volatile.Read(ref backgroundRequestVersion) ||
                ImageLoader.IsPlaceholder(image)) {
                if (!token.IsCancellationRequested && ImageLoader.IsPlaceholder(image)) {
                    Debug.WriteLine("Background image load failed; keeping the current background.");
                }
                return;
            }

            var brush = new ImageBrush(image);
            brush.Freeze();
            Bg.Background = brush;
            loadedBackgroundRequestSource = requestedSource;
            loadedBackgroundWidth = target.Width;
            loadedBackgroundHeight = target.Height;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
        }
    }

    private string? ResolveConfiguredBackgroundSource() {
        var defaultSource = FindDefaultBackgroundSource();
        var backgroundType = PropertiesUtil.launcherArgs.BgType;
        var backgroundPath = PropertiesUtil.launcherArgs.BgPath;
        if (backgroundType == "default") {
            return defaultSource;
        }

        if (backgroundType == "local") {
            return !string.IsNullOrWhiteSpace(backgroundPath) && File.Exists(backgroundPath)
                ? backgroundPath
                : defaultSource;
        }

        if (backgroundType == "network" &&
            Uri.TryCreate(backgroundPath, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) {
            return backgroundPath;
        }

        return defaultSource;
    }

    private static string? FindDefaultBackgroundSource() {
        var defaultPath = Path.Combine(DirFileUtil.LauncherSettingsDir, "bg");
        foreach (var extension in new[] { ".jpg", ".jpeg", ".png" }) {
            var path = defaultPath + extension;
            if (File.Exists(path)) {
                return path;
            }
        }

        return null;
    }

    private void CancelBackgroundLoad() {
        backgroundLoadCts.Cancel();
        backgroundLoadCts.Dispose();
        backgroundLoadCts = new CancellationTokenSource();
    }

    private void AttachHostWindow() {
        var window = Window.GetWindow(this);
        if (ReferenceEquals(window, hostWindow)) {
            return;
        }

        DetachHostWindow();
        hostWindow = window;
        if (hostWindow != null) {
            hostWindow.DpiChanged += HomeWindow_OnDpiChanged;
        }
    }

    private void DetachHostWindow() {
        if (hostWindow != null) {
            hostWindow.DpiChanged -= HomeWindow_OnDpiChanged;
            hostWindow = null;
        }
    }

    internal static BackgroundDecodeSize CalculateBackgroundDecodeSize(
        double widthInDips,
        double heightInDips,
        double dpiScaleX,
        double dpiScaleY) {
        if (widthInDips <= 0 || heightInDips <= 0) {
            return new BackgroundDecodeSize(0, 0);
        }

        var width = Math.Max(1, (int)Math.Ceiling(widthInDips * Math.Max(1, dpiScaleX)));
        var height = Math.Max(1, (int)Math.Ceiling(heightInDips * Math.Max(1, dpiScaleY)));
        const int maximumEdge = 4096;
        const long maximumPixels = 16_000_000;
        var scale = Math.Min(1d, (double)maximumEdge / Math.Max(width, height));
        if ((long)width * height * scale * scale > maximumPixels) {
            scale = Math.Sqrt((double)maximumPixels / ((long)width * height));
        }

        return new BackgroundDecodeSize(
            Math.Max(1, (int)Math.Floor(width * scale)),
            Math.Max(1, (int)Math.Floor(height * scale)));
    }

    internal static bool ShouldReloadBackground(
        int currentWidth,
        int currentHeight,
        int requestedWidth,
        int requestedHeight) {
        if (currentWidth <= 0 || currentHeight <= 0) {
            return true;
        }

        return Math.Abs(requestedWidth - currentWidth) / (double)currentWidth >= 0.25 ||
               Math.Abs(requestedHeight - currentHeight) / (double)currentHeight >= 0.25;
    }

    internal readonly record struct BackgroundDecodeSize(int Width, int Height);
    
    private DoubleAnimation mouseDownAnimation = new() {
        To = 0.9,
        Duration = TimeSpan.FromMilliseconds(200),
        EasingFunction = new CubicEase()
    };
    private DoubleAnimation mouseUpAnimation = new() {
        To = 1,
        Duration = TimeSpan.FromMilliseconds(200),
        EasingFunction = new CubicEase()
    };
    private void HomeBtn_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        var btn = sender as FrameworkElement;
        btn?.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty,mouseDownAnimation);
        btn?.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty,mouseDownAnimation);
    }

    private void HomeBtn_OnMouseLeave(object sender, MouseEventArgs e) {
        var btn = sender as FrameworkElement;
        btn?.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty,mouseUpAnimation);
        btn?.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty,mouseUpAnimation);
    }

    private void switchHomeNotice(bool flag){
        HomeNotices.Visibility = flag ? Visibility.Visible : Visibility.Collapsed;
    }

    private DoubleAnimation ToOneAnimation = new() {
        To = 1,
        Duration = TimeSpan.FromMilliseconds(200),
    };
    private DoubleAnimation ToZeroAnimation = new() {
        To = 0,
        Duration = TimeSpan.FromMilliseconds(200),
    };

    private DispatcherTimer? DownloadBtnShowTimer;
    private void switchDownloadBtnShow(bool flag) {
        this.Dispatcher.BeginInvoke(() => {
            DownloadBtn.BeginAnimation(OpacityProperty,flag ? ToOneAnimation : ToZeroAnimation);
            if (DownloadBtnShowTimer != null) {
                DownloadBtnShowTimer.Stop();
                DownloadBtnShowTimer.Tick -= DownloadBtnShowTimer_OnTick;
                DownloadBtnShowTimer = null;
            }

            if (flag) {
                DownloadBtn.Visibility = Visibility.Visible;
                
            }
            else {
                DownloadBtnShowTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) {
                    Interval = TimeSpan.FromMilliseconds(300)
                };
                DownloadBtnShowTimer.Tick += DownloadBtnShowTimer_OnTick;
                DownloadBtnShowTimer.Start();
            }
        });
    }

    private void DownloadBtnShowTimer_OnTick(object? sender, EventArgs e) {
        if (DownloadBtnShowTimer != null) {
            DownloadBtnShowTimer.Stop();
            DownloadBtnShowTimer.Tick -= DownloadBtnShowTimer_OnTick;
            DownloadBtnShowTimer = null;
        }
        DownloadBtn.Visibility = Visibility.Collapsed;
    }

    internal void SetGameInfoFromService(MinecraftItem? item) => setGameInfo(item);
    internal void SetPlayerFromService(Player player) => setPlayerFunc(player);
    internal Task HideLaunchingFromServiceAsync(bool isStop) => HideLaunchingAsync(isStop);
    internal void ErrorLaunchFromService(MinecraftItem item) => errorLaunch(item);
    internal void SetDownloadStateFromService(bool downloading) => downloadState(downloading);
    internal void SetStartingStateFromService(string state) => startingState(state);
    internal void SettingBackgroundFromService() => backgroundReloadTask = ReloadBackgroundAsync(force: true);
    internal void SwitchHomeNoticeFromService(bool visible) => switchHomeNotice(visible);
    internal void SwitchDownloadButtonFromService(bool visible) => switchDownloadBtnShow(visible);

    public async Task ActivateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (resourceImageLoadCts.IsCancellationRequested) {
            resourceImageLoadCts.Dispose();
            resourceImageLoadCts = new CancellationTokenSource();
        }
        uiCoordinator.Register(this);
        AttachHostWindow();
        backgroundReloadTask = ReloadBackgroundAsync(force: false);
        await backgroundReloadTask;
    }

    public async Task DeactivateAsync()
    {
        await CancelMinecraftStartAsync();
        if (DownloadBtnShowTimer != null) {
            DownloadBtnShowTimer.Stop();
            DownloadBtnShowTimer.Tick -= DownloadBtnShowTimer_OnTick;
            DownloadBtnShowTimer = null;
        }
        backgroundResizeTimer.Stop();
        CancelBackgroundLoad();
        resourceImageLoadCts.Cancel();
        await AwaitOwnedTaskAsync(backgroundReloadTask);
        await AwaitOwnedTaskAsync(resourceVersionChangeTask);
        await AwaitOwnedTaskAsync(gameIconLoadTask);
        await AwaitOwnedTaskAsync(playerSkinLoadTask);
        DetachHostWindow();
        uiCoordinator.Unregister(this);
    }

    private async Task CancelMinecraftStartAsync() {
        var cts = minecraftStartCts;
        var task = minecraftStartTask;
        minecraftStartCts = null;
                    minecraftStartTask = Task.FromResult(new MinecraftLaunchResult(true, false));
        cts?.Cancel();
        try {
            await task;
        }
        catch (OperationCanceledException) {
        }
        catch (Exception exception) {
            Console.WriteLine(exception);
        }
        finally {
            cts?.Dispose();
        }
    }

    private static async Task AwaitOwnedTaskAsync(Task task) {
        try {
            await task;
        }
        catch (OperationCanceledException) {
        }
        catch (Exception exception) {
            Console.WriteLine(exception);
        }
    }
}
