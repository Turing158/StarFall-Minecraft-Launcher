using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
using StarFallMC.Entity;
using StarFallMC.Util;

namespace StarFallMC;

public partial class PlayerManage : Page {

    public static string DefaultSKin = "pack://application:,,,/;component/assets/steve.png";

    private ViewModel viewModel = new ViewModel();
    public static Func<ViewModel> GetViewModel;
    public static Action<object,RoutedEventArgs> unloadedAction;
    
    
    private Storyboard SkinBoxChange;
    private Timer SkinBoxChangeTimer;

    private Storyboard LoginPageShow;
    private Storyboard LoginPageHide;

    private Storyboard OutlinePageShow;
    private Storyboard OutlinePageHide;

    private Storyboard OnlinePageShow;
    private Storyboard OnlinePageHide;

    private Storyboard OnlineLoadingShow;
    private Storyboard OnlineLoadingHide;

    public static Action<string> SetLoadingText;
    public static Action<Player> updatePlayerSkin;
    
    private string tmpDeviceCode ="";
    private Timer Logintimer;
    
     
    public PlayerManage() {
        InitializeComponent();
        DataContext = viewModel;
        
        SkinBoxChange = (Storyboard) FindResource("SkinBoxChange");
        LoginPageShow = (Storyboard) FindResource("LoginPageShow");
        LoginPageHide = (Storyboard) FindResource("LoginPageHide");
        OutlinePageShow = (Storyboard) FindResource("OutlinePageShow");
        OutlinePageHide = (Storyboard) FindResource("OutlinePageHide");
        OnlinePageShow = (Storyboard) FindResource("OnlinePageShow");
        OnlinePageHide = (Storyboard) FindResource("OnlinePageHide");
        OnlineLoadingShow = (Storyboard) FindResource("OnlineLoadingShow");

        // viewModel.Players = new ObservableCollection<Player>(players);
        viewModel.VerifyCode = "加载中...";
        SetLoadingText += SetLoadingTextFunc;
        updatePlayerSkin += updatePlayerSkinFunc;
        GetViewModel += GetViewModelFunc;
        unloadedAction += PlayerManage_OnUnloaded;
        
        PropertiesUtil.LoadPlayerManage(ref viewModel);
        PlayerListView.SelectedIndex = viewModel.Players.IndexOf(viewModel.CurrentPlayer);
        NoUser.Visibility = viewModel.Players.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    

    public class ViewModel : INotifyPropertyChanged {
        public event PropertyChangedEventHandler? PropertyChanged;
        
        private string _verifyCode;
        public string VerifyCode {
            get => _verifyCode;
            set {
                SetField(ref _verifyCode, value);
            }
        }
        
        private Player _currentPlayer;
        public Player CurrentPlayer {
            get => _currentPlayer;
            set {
                SetField(ref _currentPlayer, value);
            }
        }
        
        private ObservableCollection<Player> _players;

        public ObservableCollection<Player> Players {
            get => _players;
            set {
                SetField(ref _players,value);
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

    private ViewModel GetViewModelFunc() {
        return viewModel;
    }
    
    
    
    private async void LoginBtn_OnClick(object sender, RoutedEventArgs e) {
        OnlinePageShow.Begin();
        GetMicrosoftDeviceCodeAsync();
        viewModel.VerifyCode = "加载中...";
        loginTimerStart();

    }
    private async Task GetMicrosoftDeviceCodeAsync() {
        var result = await LoginUtil.GetMicrosoftDeviceCode().ConfigureAwait(true);
        if (result.IsSuccess) {
            JObject jo = JObject.Parse(result.Content);
            var user_code = jo["user_code"].ToString();
            tmpDeviceCode = jo["device_code"].ToString();
            viewModel.VerifyCode = user_code;
            Console.WriteLine(user_code);
            NetworkUtil.OpenUrl("https://www.microsoft.com/link");
            Clipboard.SetText(user_code);
        }
        else {
            Console.WriteLine(result.ErrorMessage);
        }
    }

    private void loginTimerStart() {
        Logintimer = new Timer(new TimerCallback( (state) => {
            Dispatcher.BeginInvoke( async () => {
                if (tmpDeviceCode != "") {
                    var result = await LoginUtil.GetMicrosoftToken(tmpDeviceCode).ConfigureAwait(true);
                    if (result.IsSuccess) {
                        JObject jo = JObject.Parse(result.Content);
                        var accessToken = jo["access_token"].ToString();
                        var refreshToken = jo["refresh_token"].ToString();
                        OnlineLoadingShow.Begin();
                        Loading.Visibility = Visibility.Visible;
                        Logintimer.DisposeAsync();
                        var info = await LoginUtil.GetXboxLiveToken(accessToken).ConfigureAwait(true);
                        if (info != "") {
                            JObject joInfo = JObject.Parse(info);
                            Loading.Visibility = Visibility.Hidden;
                            var player = new Player(
                                joInfo["name"].ToString(), 
                                joInfo["skins"][0]["url"].ToString(), 
                                true, 
                                joInfo["id"].ToString()
                            );
                            player.RefreshAddress = refreshToken;
                            bool isExist = viewModel.Players.Any(i => i.Name == player.Name && i.UUID == player.UUID);
                            if (!isExist) {
                                viewModel.Players.Add(player);
                                PlayerListView.SelectedIndex = viewModel.Players.Count-1;
                                NoUser.Opacity = 0;
                            }
                            else {
                                viewModel.Players.Where(i=> i.Name == player.Name && i.UUID == player.UUID).ToList().ForEach(i => {
                                    i.Skin = player.Skin;
                                    i.RefreshAddress = player.RefreshAddress;
                                });
                            }
                        }
                        else {
                            //提示重新认证
                            Console.WriteLine("出现错误，请重新认证");
                        }
                        OnlinePageHide.Begin();
                        LoginPageHide.Begin();
                    }
                    else {
                        Console.WriteLine(result.ErrorMessage);
                    }
                }
            });
        }), null, 2000, 3000);
        
    }

    private void OutlineBtn_OnClick(object sender, RoutedEventArgs e) {
        OutlineInput.Text = "";
        OutlinePageShow.Begin();
        
    }
    
    private void updatePlayerSkinTimer(Player player) {
        if (SkinBoxChangeTimer != null) {
            SkinBoxChangeTimer.Dispose();
        }
        SkinBoxChange.Begin();
        SkinBoxChangeTimer = new Timer(new TimerCallback(state => {
            this.Dispatcher.BeginInvoke(()=> {
                updatePlayerSkinFunc(player);
                SkinBoxChangeTimer.Dispose();
            });
        }),null,150,0);
        
    }

    private void updatePlayerSkinFunc(Player player) {
        string skin = DefaultSKin;
        if (player != null) {
            viewModel.CurrentPlayer = player;
            skin = player.Skin;
            Home.SetPlayer?.Invoke(player);
        }
        
    }

    private void AddPlayer_OnClick(object sender, RoutedEventArgs e) {
        LoginPageShow.Begin();
    }

    private void LoginBackBtn_OnClick(object sender, RoutedEventArgs e) {
        LoginPageHide.Begin();
        
    }

    private void DelPlayer_OnClick(object sender, RoutedEventArgs e) {
        if (PlayerListView.SelectedItem != null) {
            Console.WriteLine("删除:{0}",PlayerListView.SelectedItem.ToString());
            var result = MessageBox.Show("确定要删除该角色么！","",MessageBoxButton.OKCancel);
            if (result.Equals(MessageBoxResult.OK)) {
                var index = PlayerListView.SelectedIndex;
                viewModel.Players.RemoveAt(index);
                // players.RemoveAt(index);
                if (viewModel.Players.Count() != 0) {
                    PlayerListView.SelectedIndex = 0;
                    NoUser.Visibility = Visibility.Collapsed;
                }
                else {
                    PlayerListView.SelectedIndex = -1;
                    NoUser.Visibility = Visibility.Visible;
                }
            }
        }
    }

    private void OutlineBackBtn_OnClick(object sender, RoutedEventArgs e) {
        OutlinePageHide.Begin();
    }

    private void OutlineConfirm_OnClick(object sender, RoutedEventArgs e) {
        bool flag = true;
        if (OutlineInput.Text.Length < 2) {
            NameTips.Text = "游戏名至少需要3个字符";
            flag = false;
        }
        if (OutlineInput.Text == "") {
            NameTips.Text = "游戏名不能为空";
            flag = false;
        }
        string pattern = @"^\w+$";
        if (!Regex.IsMatch(OutlineInput.Text,pattern)) {
            NameTips.Text = "游戏名只能包含字母、数字和下划线";
            flag = false;
        }
        if (viewModel.Players.Any(i => i.Name == OutlineInput.Text && i.IsOnline == false)) {
            NameTips.Text = "游戏名已存在";
            flag = false;
        }
        if (!flag) {
            ((Storyboard)FindResource("NameTipsShow")).Begin();
            return;
        }
        var player = new Player(OutlineInput.Text,DefaultSKin,false,Guid.NewGuid().ToString().Replace("-", ""));
        viewModel.Players.Add(player);
        PlayerListView.SelectedIndex = viewModel.Players.Count-1;
        NoUser.Opacity = 0;
        LoginPageHide.Begin();
        OutlinePageHide.Begin();
    }

    private void OnlineBackBtn_OnClick(object sender, RoutedEventArgs e) {
        OnlinePageHide.Begin();
        HttpRequestUtil.StopRequest();
        if (Logintimer != null) {
            Logintimer.DisposeAsync();
        }

        Loading.Opacity = 0;
    }

    private void VerifyCodeText_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        var code = viewModel.VerifyCode;
        if (code != "加载中") {
            Clipboard.SetText(code);
        }
    }

    private void PlayerListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        Player player = new Player();
        if (PlayerListView.SelectedIndex != -1) {
            player = viewModel.Players[PlayerListView.SelectedIndex];
        }
        updatePlayerSkinTimer(player);
    }

    private void SetLoadingTextFunc(string text) {
        LoadingText.Text = text;
    }
    
    private void PlayerManage_OnUnloaded(object sender, RoutedEventArgs e) {
        PropertiesUtil.SavePlayerManageArgs();
    }
}