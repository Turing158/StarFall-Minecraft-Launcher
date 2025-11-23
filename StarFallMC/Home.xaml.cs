using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.Entity.Enum;
using StarFallMC.Util;
using MessageBox = StarFallMC.Component.MessageBox;
using MessageBoxResult = StarFallMC.Entity.Enum.MessageBoxResult;

namespace StarFallMC;

public partial class Home : Page {

    private ViewModel viewModel = new ViewModel();

    private Storyboard Downloading;
    
    public static Action<MinecraftItem> SetGameInfo;
    public static Action<Player> SetPlayer;
    public static Action<bool> HideLaunching;
    public static Action<MinecraftItem> ErrorLaunch;
    public static Action<bool> DownloadState;
    public static Action<string> StartingState;
    public static Action SettingBackground;
    public static Func<ViewModel> GetViewModel;
    public static bool GameStarting = false;
    
    public Home() {
        
        InitializeComponent();
        
        DataContext = viewModel;

        viewModel.PlayerName = "";
        
        SetGameInfo = setGameInfo;
        SetPlayer = setPlayerFunc;
        HideLaunching = hideLaunching;
        ErrorLaunch = errorLaunch;
        DownloadState = downloadState;
        StartingState = startingState;
        SettingBackground = settingBackground;
        GetViewModel = getViewModel;
        
        Downloading = (Storyboard)FindResource("Downloading");
        
        var (player, players) = PropertiesUtil.loadPlayers();
        setPlayerFunc(player);
        settingBackground();
    }
    
    public class ViewModel : INotifyPropertyChanged {
        
        private string _playerName;
        public string PlayerName {
            get => _playerName;
            set => SetField(ref _playerName, value);
        }

        private MinecraftItem _currentGame;
        public MinecraftItem CurrentGame {
            get => _currentGame;
            set => SetField(ref _currentGame, value);
        }
        
        private Player _currentPlayer;
        public Player CurrentPlayer {
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

    private ViewModel getViewModel() {
        return viewModel;
    }
    

    private void CurrentGame_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        MainWindow.SubFrameNavigate?.Invoke("SelectGame", "Minecraft - 我的世界");
    }

    private void CurrentPlayer_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        MainWindow.SubFrameNavigate?.Invoke("PlayerManage", "Players - 玩家");
    }
    

    private void setGameInfo(MinecraftItem item) {
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
            iconPath = "pack://application:,,,/;component"+iconPath;
        }
        ResourceUtil.ClearLocalResources();
        ResourcePage.ChangeVersionAction?.Invoke();
        
        updateBitmapImage( "CurrentGameIcon",iconPath);
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
        updateBitmapImage("PlayerSkin",skin);
    }
    
    private void updateBitmapImage(string resourceKey, string uri) {
        if (uri.Contains(":") && !uri.StartsWith("pack") && !uri.StartsWith("http")) {
            if (!File.Exists(uri)) {
                uri = "pack://application:,,,/;component/assets/DefaultGameIcon/unknowGame.png";
            }
        }
        BitmapImage newImage = new BitmapImage();
        newImage.BeginInit();
        newImage.UriSource = new Uri(uri, UriKind.RelativeOrAbsolute);
        newImage.CacheOption = BitmapCacheOption.OnLoad;
        newImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        newImage.EndInit();
        Application.Current.Resources[resourceKey] = newImage;
    }
    
    private CancellationTokenSource minecraftStartCts;
    private void StartGameBtn_OnClick(object sender, RoutedEventArgs e) {
        if (!GameStarting) {
            bool flag = true;
            StringBuilder tips = new StringBuilder();
            if (viewModel.CurrentGame == null || viewModel.CurrentGame.Name == "未选择版本") {
                tips.Append("未选择Minecraft版本");
                ((Storyboard)FindResource("GameEnter")).Begin();
                flag = false;
            }
            if (string.IsNullOrEmpty(viewModel.PlayerName) || viewModel.PlayerName == "未登录") {
                if (!string.IsNullOrEmpty(tips.ToString())) {
                    tips.Append("\n");
                }
                tips.Append("未选择Player角色");
                ((Storyboard)FindResource("AvatarEnter")).Begin();
                flag = false;
            }
            if (!string.IsNullOrEmpty(tips.ToString())) {
                MessageTips.Show(tips.ToString(), MessageTips.MessageType.Warning);
            }
            if (MinecraftUtil.GetJavaVersions().Count == 0) {
                MessageBox.Show("未检测到系统安装的Java版本。\n    1.请前往设置或Oracle官网下载！\n    2.前往设置自行添加Java版本", "未检测到Java版本");
                flag = false;
            }
            if (!flag) {
                return;
            }
            minecraftStartCts = new CancellationTokenSource();
            GameStarting = true;
            StartingBorder.Visibility = Visibility.Visible;
            ((Storyboard)FindResource("Starting")).Begin();
            HomeTips.Show();
            Console.WriteLine("开始游戏");
            MinecraftUtil.StartMinecraft(viewModel.CurrentGame, viewModel.CurrentPlayer,cancellationToken:minecraftStartCts.Token);
        }
    }

    private void StartingBtn_OnClick(object sender, RoutedEventArgs e) {
        hideLaunching(true);
    }

    private void hideLaunching(bool isStop = false) {
        Dispatcher.Invoke(() => {
            try {
                GameStarting = false;
                StartingBorder.Visibility = Visibility.Collapsed;
                HomeTips.Hide();
                ((Storyboard)FindResource("Started")).Begin();
                if (isStop) {
                    minecraftStartCts?.Cancel();
                    MinecraftUtil.StopMinecraft();
                }
            }
            catch (Exception e){
                Console.WriteLine(e);
            }
        });
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
        MainWindow.DownloadPageShow?.Invoke();
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
        Downloading.Begin();
    }

    private void startingState(string state) {
        Dispatcher.BeginInvoke(() => {
            StatusText.Text = state;
        });
    }

    private void Home_OnLoaded(object sender, RoutedEventArgs e) {
        settingBackground();
    }

    private void settingBackground() {
        var bgPath = PropertiesUtil.launcherArgs.BgPath;
        var bgType = PropertiesUtil.launcherArgs.BgType;
        BitmapImage bgImage = new BitmapImage();
        bgImage.BeginInit();
        if (bgType == "default") {
            string defaultPath = $"{DirFileUtil.LauncherSettingsDir}/bg";
            string[] bgSuffix = { ".jpg", ".jpeg", ".png" };
            foreach (var i in bgSuffix) {
                string path = $"{defaultPath}{i}";
                if (File.Exists(path)) {
                    bgImage.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                    break;
                }
            }
        }
        else if (bgType == "local") {
            if (File.Exists(bgPath)) {
                bgImage.UriSource = new Uri(bgPath, UriKind.RelativeOrAbsolute);
            }
        }
        else if (bgType == "network") {
            if (!string.IsNullOrEmpty(bgPath)) {
                bgImage.UriSource = new Uri(bgPath, UriKind.RelativeOrAbsolute);
            }
        }
        
        if (bgImage.UriSource != null) {
            bgImage.CacheOption = BitmapCacheOption.OnLoad;
            bgImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bgImage.EndInit();
            Bg.Background = new ImageBrush(bgImage);
        }
        else {
            Bg.Background = null;
        }
    }
    
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
}