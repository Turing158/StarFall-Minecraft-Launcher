using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using StarFallMC.Entity;
using StarFallMC.Util;

namespace StarFallMC;

public partial class Home : Page {

    private ViewModel viewModel = new ViewModel();
    
    public static Action<MinecraftItem> SetGameInfo;
    public static Action<Player> SetPlayer;
    
    public Home() {
        
        InitializeComponent();
        
        DataContext = viewModel;

        viewModel.PlayerName = "";
        //这里之后要获取配置文件中的
        
        SetGameInfo = setGameInfo;
        SetPlayer = setPlayerFunc;
        var (player, players) = PropertiesUtil.loadPlayers();
        setPlayerFunc(player);
    }
    
    public class ViewModel : INotifyPropertyChanged {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _playerName;
        private MinecraftItem _currentGame;

        public string PlayerName {
            get => _playerName;
            set {
                SetField(ref _playerName, value);
            }
        }

        public MinecraftItem CurrentGame {
            get => _currentGame;
            set {
                SetField(ref _currentGame, value);
            }
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
    

    private void CurrentGame_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        MainWindow.SubFrameNavigate?.Invoke("SelectGame", "Minecraft");
    }

    private void CurrentPlayer_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        MainWindow.SubFrameNavigate?.Invoke("PlayerManage", "Players");
    }
    

    private void setGameInfo(MinecraftItem item) {
        string iconPath = "/assets/DefaultGameIcon/unknowGame.png";
        if (item == null) {
            GameName.Text = "未选择版本";
            viewModel.CurrentGame = new MinecraftItem("未选择版本","","","/assets/DefaultGameIcon/unknowGame.png");
            

        } else {
            GameName.Text = item.Name;
            iconPath = item.Icon;
            viewModel.CurrentGame = item;
        }
        if (!iconPath.Contains(":")) {
            iconPath = "pack://application:,,,/;component"+iconPath;
        }
        updateBitmapImage( "CurrentGameIcon",iconPath);
        Console.WriteLine(item);
    }
    
    private void setPlayerFunc(Player player) {
        Console.WriteLine("当前玩家名称："+player.Name);
        string skin;
        if (string.IsNullOrEmpty(player.Name)) {
            viewModel.PlayerName = "未登录";
            skin = PlayerManage.DefaultSKin;
        } else {
            viewModel.PlayerName = player.Name;
            skin = player.Skin;
        }
        updateBitmapImage("PlayerSkin",skin);
    }
    
    private void updateBitmapImage(string resourceKey, string uri) {
        if (uri.Contains(":") && !uri.StartsWith("pack")) {
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

    private void StartGameBtn_OnClick(object sender, RoutedEventArgs e) {
        bool flag = true;
        if (viewModel.CurrentGame == null || viewModel.CurrentGame.Name == "未选择版本") {
            ((Storyboard)FindResource("GameEnter")).Begin();
            flag = false;
        }
        if (string.IsNullOrEmpty(viewModel.PlayerName) || viewModel.PlayerName == "未登录") {
            ((Storyboard)FindResource("AvatarEnter")).Begin();
            flag = false;
        }
        if (!flag) {
            return;
        }
        StartingBorder.Visibility = Visibility.Visible;
        ((Storyboard)FindResource("Starting")).Begin();
        Console.WriteLine("开始游戏");
    }

    private void StartingBtn_OnClick(object sender, RoutedEventArgs e) {
        StartingBorder.Visibility = Visibility.Collapsed;
        ((Storyboard)FindResource("Started")).Begin();
    }
}