using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Newtonsoft.Json.Linq;
using StarFallMC.Entity;
using StarFallMC.Util;
using MessageBox = StarFallMC.Component.MessageBox;

namespace StarFallMC;

public partial class PlayerManage : Page {

    public static string DefaultSKin = "pack://application:,,,/;component/assets/steve.png";

    private ViewModel viewModel = new ViewModel();
    public static Func<ViewModel> GetViewModel;
    public static Action<object,RoutedEventArgs> unloadedAction;
    public static Action<int> SetPlayerListIndex;
    public static Action<Player> SetPlayerListItem;
    
    
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
        GetViewModel = GetViewModelFunc;
        unloadedAction = PlayerManage_OnUnloaded;
        SetLoadingText = setLoadingTextFunc;
        updatePlayerSkin = updatePlayerSkinFunc;
        SetPlayerListIndex = setPlayerListIndex;
        SetPlayerListItem = setPlayerListItem;
        
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
                            if (joInfo["error"] == null) {
                                var player = new Player(
                                    joInfo["name"].ToString(), 
                                    joInfo["skins"][0]["url"].ToString(), 
                                    true, 
                                    joInfo["id"].ToString()
                                );
                                player.RefreshToken = refreshToken;
                                player.AccessToken = joInfo["access_token"].ToString();
                                var currentPlayer = viewModel.Players.First(i => i.UUID == player.UUID && i.IsOnline);
                                if (currentPlayer == null) {
                                    viewModel.Players.Add(player);
                                    PlayerListView.SelectedIndex = viewModel.Players.Count-1;
                                    NoUser.Opacity = 0;
                                }
                                else {
                                    var index = viewModel.Players.IndexOf(currentPlayer);
                                    if (index != -1) {
                                        viewModel.Players[index] = player;
                                        PlayerListView.SelectedIndex = index;
                                    }
                                    
                                }
                                updatePlayerSkinFunc(player);
                            }
                            else {
                                MessageBox.Show("出现问题，请重新认证\n    1.您未拥有Minecraft正版。    2.前往Minecraft官网使用Microsoft重新登录一下。    \n3.请检查网络后再试！","认证失败");
                                Console.WriteLine("出现问题，请重新认证");
                            }
                        }
                        else {
                            //提示重新认证
                            MessageBox.Show("出现问题，请重新认证\n    1.您未拥有Minecraft正版。    2.前往Minecraft官网使用Microsoft重新登录一下。    \n3.请检查网络后再试！","认证失败");
                            Console.WriteLine("出现问题，请重新认证");
                        }
                        OnlinePageHide.Begin();
                        LoginPageHide.Begin();
                    }
                    else {
                        Console.WriteLine(result.ErrorMessage);
                    }
                }
            });
        }), null, 5000, 3000);
        
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
        if (player != null) {
            viewModel.CurrentPlayer = player;
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
            var item = PlayerListView.SelectedItem as Player;
            Console.WriteLine("删除:{0}",item);
            MessageBox.Show($"你确定要删除\" {item.Name} \"这个角色吗？它会消失很久的喔！",$"{item.Name} 提醒您：",MessageBox.BtnType.ConfirmAndCancel,
                r => {
                    if (r == MessageBox.Result.Confirm) {
                        var index = PlayerListView.SelectedIndex;
                        viewModel.Players.RemoveAt(index);
                        if (viewModel.Players.Count() != 0) {
                            PlayerListView.SelectedIndex = 0;
                            NoUser.Visibility = Visibility.Collapsed;
                        }
                        else {
                            PlayerListView.SelectedIndex = -1;
                            NoUser.Visibility = Visibility.Visible;
                        }
                    }
                },"","删除");
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

    private void setLoadingTextFunc(string text) {
        LoadingText.Text = text;
    }
    
    private void PlayerManage_OnUnloaded(object sender, RoutedEventArgs e) {
        PropertiesUtil.SavePlayerManageArgs();
    }

    private void SkinChangeBtn_OnClick(object sender, RoutedEventArgs e) {
        if (viewModel.CurrentPlayer.Name == "") {
            return;
        }
        if (viewModel.CurrentPlayer.IsOnline) {
            NetworkUtil.OpenUrl("https://www.minecraft.net/zh-hans/msaprofile/mygames/editskin");
        }
        else {
            
        }
    }

    private void NameChangeBtn_OnClick(object sender, RoutedEventArgs e) {
        if (viewModel.CurrentPlayer.Name == "") {
            return;
        }
        if (viewModel.CurrentPlayer.IsOnline) {
            NetworkUtil.OpenUrl("https://www.minecraft.net/zh-hans/msaprofile/mygames/editprofile");
        }
        else {
            
        }
    }

    private void RefreshPlayer_OnClick(object sender, RoutedEventArgs e) {
        if (viewModel.CurrentPlayer.IsOnline) {
            RefreshOnlinePlayer();
        }
    }

    private async void  RefreshOnlinePlayer() {
        Console.WriteLine(viewModel.CurrentPlayer);
        var box = MessageBox.Show($"正在刷新 {viewModel.CurrentPlayer.Name} 玩家信息，请稍等...","刷新玩家信息",MessageBox.BtnType.None);
        var result = await LoginUtil.RefreshMicrosoftToken(viewModel.CurrentPlayer).ConfigureAwait(true);
        if (result != null) {
            Console.WriteLine(result);
            MessageBox.Show(
                content:$"刷新完成！ {result.Name} 在启动器中的档案已更新",
                title:"刷新玩家信息",
                confirmBtnText:"确定",
                callback: r => {
                    MessageBox.Delete(box);
                });
            
        }
        else {
            MessageBox.Show("出现问题，请重新认证\n    1.您未拥有Minecraft正版。\n    2.前往Minecraft官网使用Microsoft重新登录一下。\n    3.请检查网络后再试！","认证失败");
            Console.WriteLine("出现问题，请重新认证");
        }
    }

    public void setPlayerListIndex(int index) {
        PlayerListView.SelectedIndex = index;
    }

    public void setPlayerListItem(Player player) {
        setPlayerListIndex(viewModel.Players.IndexOf(player));
    }
}