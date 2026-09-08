using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.Entity.Enum;
using StarFallMC.Util;
using StarFallMC.Services;
using StarFallMC.Navigation;
using MessageBox = StarFallMC.Component.MessageBox;
using MessageBoxResult = StarFallMC.Entity.Enum.MessageBoxResult;

namespace StarFallMC;

public partial class PlayerManage : Page, IPageLifecycle {

    public static string DefaultSKin = "pack://application:,,,/StarFallMC;component/assets/steve.png";

    private readonly PlayerState viewModel = ApplicationState.Players;
    
    private Storyboard SkinBoxChange;
    private DispatcherTimer? SkinBoxChangeTimer;

    private Storyboard LoginPageShow;
    private Storyboard LoginPageHide;

    private Storyboard OutlinePageShow;
    private Storyboard OutlinePageHide;

    private Storyboard OnlinePageShow;
    private Storyboard OnlinePageHide;

    private Storyboard OnlineLoadingShow;
    
    private string tmpDeviceCode ="";
    private int retryCount = 0;
    private bool isChange = false;
    private CancellationTokenSource? loginCts;
    private CancellationTokenSource? refreshCts;
    private readonly SemaphoreSlim loginGate = new(1, 1);
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private Task activeLoginTask = Task.CompletedTask;
    private Task activeRefreshTask = Task.CompletedTask;
    private bool isActive;
    private readonly LauncherUiCoordinator uiCoordinator;
    
    public PlayerManage(LauncherUiCoordinator? uiCoordinator = null) {
        this.uiCoordinator = uiCoordinator ?? new LauncherUiCoordinator();
        InitializeComponent();
        DataContext = viewModel;
        this.uiCoordinator.Register(this);
        
        SkinBoxChange = (Storyboard) FindResource("SkinBoxChange");
        LoginPageShow = (Storyboard) FindResource("LoginPageShow");
        LoginPageHide = (Storyboard) FindResource("LoginPageHide");
        OutlinePageShow = (Storyboard) FindResource("OutlinePageShow");
        OutlinePageHide = (Storyboard) FindResource("OutlinePageHide");
        OnlinePageShow = (Storyboard) FindResource("OnlinePageShow");
        OnlinePageHide = (Storyboard) FindResource("OnlinePageHide");
        OnlineLoadingShow = (Storyboard) FindResource("OnlineLoadingShow");
        
        viewModel.VerifyCode = "加载中...";
        PlayerListView.SelectedIndex = viewModel.Players.IndexOf(viewModel.CurrentPlayer);
        NoUser.Visibility = viewModel.Players.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    

    private async void LoginBtn_OnClick(object sender, RoutedEventArgs e) {
        OnlinePageShow.Begin();
        viewModel.VerifyCode = "加载中...";
        retryCount = 0;
        await StartMicrosoftLoginAsync();
    }

    private async Task StartMicrosoftLoginAsync() {
        CancellationTokenSource? currentCts = null;
        Task<bool>? loginTask = null;
        Task currentTask = Task.CompletedTask;
        bool reloadAfterTimeout = false;
        await loginGate.WaitAsync();
        try {
            var previousCts = loginCts;
            var previousTask = activeLoginTask;
            loginCts = null;
            activeLoginTask = Task.CompletedTask;
            TryCancel(previousCts);
            await AwaitPageOperationAsync(previousTask);
            previousCts?.Dispose();

            if (!isActive) {
                return;
            }

            currentCts = new CancellationTokenSource();
            loginTask = RunMicrosoftLoginAsync(currentCts.Token);
            currentTask = loginTask;
            loginCts = currentCts;
            activeLoginTask = currentTask;
        }
        finally {
            loginGate.Release();
        }

        try {
            reloadAfterTimeout = await loginTask!;
        }
        catch (OperationCanceledException) {
        }
        catch (Exception exception) {
            Console.WriteLine(exception);
            MessageTips.Show("正版登录认证失败", MessageTips.MessageType.Error);
        }
        finally {
            await loginGate.WaitAsync();
            try {
                if (ReferenceEquals(loginCts, currentCts)) {
                    loginCts = null;
                    activeLoginTask = Task.CompletedTask;
                    currentCts?.Dispose();
                }
            }
            finally {
                loginGate.Release();
            }
        }

        if (reloadAfterTimeout && isActive) {
            await uiCoordinator.ReloadSubPageAsync("PlayerManage", null);
            MessageBox.Show("认证超时，建议在五分钟之内完成严重，请重新认证！", "登录失败");
        }
    }
    
    private async Task GetMicrosoftDeviceCodeAsync(CancellationToken cancellationToken) {
        var result = await LoginUtil.GetMicrosoftDeviceCode().ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();
        if (result.IsSuccess) {
            JObject jo = JObject.Parse(result.Content);
            var user_code = GetRequiredJsonString(jo, "user_code");
            tmpDeviceCode = GetRequiredJsonString(jo, "device_code");
            viewModel.VerifyCode = user_code;
            Console.WriteLine(user_code);
            NetworkUtil.OpenUrl("https://www.microsoft.com/link");
            Clipboard.SetText(user_code);
        }
        else {
            MessageTips.Show("获取设备码失败",MessageTips.MessageType.Error);
            Console.WriteLine(result.ErrorMessage);
        }
    }

    private async Task<bool> RunMicrosoftLoginAsync(CancellationToken cancellationToken) {
        tmpDeviceCode = string.Empty;
        await GetMicrosoftDeviceCodeAsync(cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        while (!string.IsNullOrEmpty(tmpDeviceCode)) {
            cancellationToken.ThrowIfCancellationRequested();
            retryCount++;
            var result = await LoginUtil.GetMicrosoftToken(tmpDeviceCode, cancellationToken).ConfigureAwait(true);
                    if (result.IsSuccess) {
                        JObject jo = JObject.Parse(result.Content);
                        var accessToken = GetRequiredJsonString(jo, "access_token");
                        var refreshToken = GetRequiredJsonString(jo, "refresh_token");
                        OnlineLoadingShow.Begin();
                        Loading.Visibility = Visibility.Visible;
                        var statusProgress = new Progress<string>(setLoadingTextFunc);
                        var info = await LoginUtil.GetXboxLiveToken(
                            accessToken,
                            cancellationToken,
                            statusProgress,
                            message => MessageTips.Show(message)).ConfigureAwait(true);
                        if (info != "") {
                            JObject joInfo = JObject.Parse(info);
                            Loading.Visibility = Visibility.Hidden;
                            if (joInfo["error"] == null) {
                                var player = new Player(
                                    GetRequiredJsonString(joInfo, "name"),
                                    GetRequiredSkinUrl(joInfo),
                                    true, 
                                    GetRequiredJsonString(joInfo, "id")
                                );
                                player.RefreshToken = refreshToken;
                                player.AccessToken = GetRequiredJsonString(joInfo, "access_token");
                                bool directAddPlayer = false;
                                Player? currentPlayer = null;
                                if (viewModel.Players != null && viewModel.Players.Count != 0) {
                                    try
                                    {
                                        viewModel.Players.FirstOrDefault(i => i.UUID == player.UUID && i.IsOnline);
                                    }
                                    catch(Exception e)
                                    {
                                        Console.WriteLine(e);
                                    }
                                    currentPlayer = viewModel.Players.FirstOrDefault(i => i.UUID == player.UUID && i.IsOnline);
                                    directAddPlayer = currentPlayer == null;
                                }
                                else {
                                    viewModel.Players = new ();
                                    directAddPlayer = true;
                                }
                                if (directAddPlayer) {
                                    viewModel.Players.Add(player);
                                    PlayerListView.SelectedIndex = viewModel.Players.Count-1;
                                    NoUser.Opacity = 0;
                                }
                                else if (currentPlayer is not null) {
                                    var index = viewModel.Players.IndexOf(currentPlayer);
                                    if (index != -1) {
                                        viewModel.Players[index] = player;
                                        PlayerListView.SelectedIndex = index;
                                    }
                                }
                                updatePlayerSkinFunc(player);
                                MessageTips.Show("正版登录认证成功！");
                            }
                            else {
                                MessageBox.Show("出现问题，请重新认证\n    1.您未拥有Minecraft正版。    2.前往Minecraft官网使用Microsoft重新登录一下。    \n3.请检查网络后再试！","登录失败");
                                Console.WriteLine("出现问题，请重新认证");
                            }
                        }
                        else {
                            MessageBox.Show("出现问题，请重新认证\n    1.您未拥有Minecraft正版。    2.前往Minecraft官网使用Microsoft重新登录一下。    \n3.请检查网络后再试！","登录失败");
                            Console.WriteLine("出现问题，请重新认证");
                        }
                        OnlinePageHide.Begin();
                        LoginPageHide.Begin();
                        return false;
                    }
                    else {
                        Console.WriteLine(result.ErrorMessage);
                    }
                    if (retryCount >= 300) {
                        return true;
                    }
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }
        return false;
    }

    private void OutlineBtn_OnClick(object sender, RoutedEventArgs e) {
        OutlineInput.Text = "";
        OutlinePageShow.Begin();
    }
    
    private void updatePlayerSkinTimer(Player player) {
        StopSkinChangeTimer();
        SkinBoxChange.Begin();
        pendingSkinPlayer = player;
        SkinBoxChangeTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        SkinBoxChangeTimer.Tick += SkinBoxChangeTimer_OnTick;
        SkinBoxChangeTimer.Start();
        
    }

    private void updatePlayerSkinFunc(Player player) {
        if (player != null) {
            viewModel.CurrentPlayer = player;
            uiCoordinator.SetHomePlayer(player);
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
            if (PlayerListView.SelectedItem is not Player item) {
                return;
            }
            Console.WriteLine("删除:{0}",item);
            MessageBox.Show($"你确定要删除\" {item.Name} \"这个角色吗？它会消失很久的喔！",$"{item.Name} 提醒您：",MessageBoxBtnType.ConfirmAndCancel,
                r => {
                    if (r == MessageBoxResult.Confirm) {
                        var index = PlayerListView.SelectedIndex;
                        MessageTips.Show($"成功删除该角色\n[{item.Name}]");
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
        isChange = false;
        OutlineInput.Text = "";
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
            ((Storyboard)FindResource("NameTipsShow")).Begin(this, true);
            return;
        }
        if (isChange) {
            var index = viewModel.Players.IndexOf(viewModel.CurrentPlayer);
            if (index != -1) {
                PlayerListView.SelectedIndex = -1;
                var cp = viewModel.CurrentPlayer;
                MessageTips.Show($"成功修改Player\n{cp.Name} => {OutlineInput.Text}");
                cp.Name = OutlineInput.Text;
                viewModel.Players[index] = cp;
                PlayerListView.SelectedIndex = index;
            }
        }
        else {
            var player = new Player(OutlineInput.Text,DefaultSKin,false,Guid.NewGuid().ToString().Replace("-", ""));
            MessageTips.Show($"成功添加Player\n{player.Name}");
            viewModel.Players.Add(player);
            PlayerListView.SelectedIndex = viewModel.Players.Count-1;
        }
        NoUser.Opacity = 0;
        LoginPageHide.Begin();
        OutlinePageHide.Begin();
    }

    private void OnlineBackBtn_OnClick(object sender, RoutedEventArgs e) {
        OnlinePageHide.Begin();
        TryCancel(loginCts);
        Loading.Opacity = 0;
    }

    private void VerifyCodeText_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        var code = viewModel.VerifyCode;
        if (code != "加载中") {
            Clipboard.SetText(code);
            MessageTips.Show("验证码已复制到剪贴板");
        }
    }
    
    private void LinkText_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        Clipboard.SetText("https://www.microsoft.com/link");
        MessageTips.Show("链接已复制到剪贴板");
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
        isActive = false;
        TryCancel(loginCts);
        TryCancel(refreshCts);
        StopSkinChangeTimer();
        uiCoordinator.Unregister(this);
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
            if (viewModel.CurrentPlayer.Skin == null 
                || string.IsNullOrEmpty(viewModel.CurrentPlayer.Skin) 
                || viewModel.CurrentPlayer.Skin.Equals(DefaultSKin)
            ) {
                selectAndChangeOutlineSkin();
                return;
            }
            MessageBox.Show("请选择重置成steve皮肤还是再次选择皮肤文件","更换皮肤",MessageBoxBtnType.ConfirmAndCancel,result => {
                if (result == MessageBoxResult.Confirm) {
                    selectAndChangeOutlineSkin();
                }
                else if(result == MessageBoxResult.Cancel) {
                    changeOutlineSkin(DefaultSKin);
                }
            },confirmBtnText: "选择皮肤",cancelBtnText: "重置皮肤",showCloseBtn: true);
        }
    }

    private void selectAndChangeOutlineSkin() {
        OpenFileDialog ofd = new OpenFileDialog();
        ofd.Filter = "PNG 图片|*.png";
        ofd.Title = "选择皮肤文件(.png)";
        ofd.InitialDirectory = DirFileUtil.CurrentDirPosition;
        if (ofd.ShowDialog() == true) {
            MessageTips.Show("测试换肤功能（未实装游戏效果）");
            string selectedPath = ofd.FileName;
            changeOutlineSkin(selectedPath);
        }
    }

    private void changeOutlineSkin(string path){
        PlayerListView.SelectedIndex = -1;
        var player = viewModel.CurrentPlayer;
        var index = viewModel.Players.IndexOf(player);
        player.Skin = path;
        viewModel.Players[index] = player;
        PlayerListView.SelectedIndex = index;
    }

    private void NameChangeBtn_OnClick(object sender, RoutedEventArgs e) {
        if (viewModel.CurrentPlayer.Name == "") {
            return;
        }
        if (viewModel.CurrentPlayer.IsOnline) {
            NetworkUtil.OpenUrl("https://www.minecraft.net/zh-hans/msaprofile/mygames/editprofile");
        }
        else {
            isChange = true;
            OutlineInput.Text = viewModel.CurrentPlayer.Name;
            OutlinePageShow.Begin();
        }
    }

    private async void RefreshPlayer_OnClick(object sender, RoutedEventArgs e) {
        if (viewModel.CurrentPlayer.IsOnline) {
            await RefreshOnlinePlayerAsync();
        }
    }

    private static string GetRequiredJsonString(JObject value, string propertyName) {
        return value[propertyName]?.ToString()
            ?? throw new InvalidDataException($"Authentication response is missing '{propertyName}'.");
    }

    private static string GetRequiredSkinUrl(JObject value) {
        return value["skins"]?.FirstOrDefault()?["url"]?.ToString()
            ?? throw new InvalidDataException("Authentication response is missing a skin URL.");
    }

    private async Task RefreshOnlinePlayerAsync() {
        CancellationTokenSource? currentCts = null;
        Task currentTask = Task.CompletedTask;
        await refreshGate.WaitAsync();
        try {
            var previousCts = refreshCts;
            var previousTask = activeRefreshTask;
            refreshCts = null;
            activeRefreshTask = Task.CompletedTask;
            TryCancel(previousCts);
            await AwaitPageOperationAsync(previousTask);
            previousCts?.Dispose();

            if (!isActive) {
                return;
            }

            currentCts = new CancellationTokenSource();
            var player = viewModel.CurrentPlayer;
            currentTask = RefreshOnlinePlayerCoreAsync(player, currentCts.Token);
            refreshCts = currentCts;
            activeRefreshTask = currentTask;
        }
        finally {
            refreshGate.Release();
        }

        try {
            await currentTask;
        }
        catch (OperationCanceledException) {
        }
        catch (Exception exception) {
            Console.WriteLine(exception);
            MessageTips.Show("刷新玩家信息失败", MessageTips.MessageType.Error);
        }
        finally {
            await refreshGate.WaitAsync();
            try {
                if (ReferenceEquals(refreshCts, currentCts)) {
                    refreshCts = null;
                    activeRefreshTask = Task.CompletedTask;
                    currentCts?.Dispose();
                }
            }
            finally {
                refreshGate.Release();
            }
        }
    }

    private async Task RefreshOnlinePlayerCoreAsync(Player player, CancellationToken cancellationToken) {
        Console.WriteLine(player);
        var box = MessageBox.Show($"正在刷新 {player.Name} 玩家信息，请稍等...", "刷新玩家信息", MessageBoxBtnType.None);
        try {
            var result = await LoginUtil.RefreshMicrosoftToken(
                player,
                cancellationToken,
                new Progress<string>(setLoadingTextFunc),
                message => MessageTips.Show(message)).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            if (result != null) {
                setPlayerListItem(result);
                uiCoordinator.SetHomePlayer(result);
                MessageTips.Show($"刷新 {result.Name} 玩家信息成功！");
                MessageBox.Show(
                    content: $"刷新完成！ {result.Name} 在启动器中的档案已更新",
                    title: "刷新玩家信息",
                    confirmBtnText: "确定");
            }
            else {
                MessageBox.Show("出现问题，请重新认证\n    1.您未拥有Minecraft正版。\n    2.前往Minecraft官网使用Microsoft重新登录一下。\n    3.请检查网络后再试！", "认证失败");
                Console.WriteLine("出现问题，请重新认证");
            }
        }
        finally {
            MessageBox.Delete(box);
        }
    }

    public void setPlayerListIndex(int index) {
        PlayerListView.SelectedIndex = index;
    }

    public void setPlayerListItem(Player player) {
        setPlayerListIndex(viewModel.Players.IndexOf(player));
    }

    public Task ActivateAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        isActive = true;
        uiCoordinator.Register(this);
        return Task.CompletedTask;
    }

    public async Task DeactivateAsync() {
        isActive = false;
        StopSkinChangeTimer();
        await CancelOperationAsync(loginGate, () => loginCts, source => loginCts = source, () => activeLoginTask, task => activeLoginTask = task);
        await CancelOperationAsync(refreshGate, () => refreshCts, source => refreshCts = source, () => activeRefreshTask, task => activeRefreshTask = task);
        uiCoordinator.Unregister(this);
        PropertiesUtil.SavePlayerManageArgs();
    }

    internal void SetLoadingTextFromService(string text) => setLoadingTextFunc(text);
    internal void SetPlayerListItemFromService(Player player) => setPlayerListItem(player);

    private static void TryCancel(CancellationTokenSource? source) {
        try {
            source?.Cancel();
        }
        catch (ObjectDisposedException) {
        }
    }

    private static async Task AwaitPageOperationAsync(Task task) {
        try {
            await task;
        }
        catch (OperationCanceledException) {
        }
        catch (Exception exception) {
            Console.WriteLine(exception);
        }
    }

    private static async Task CancelOperationAsync(
        SemaphoreSlim gate,
        Func<CancellationTokenSource?> getCts,
        Action<CancellationTokenSource?> setCts,
        Func<Task> getTask,
        Action<Task> setTask) {
        await gate.WaitAsync();
        try {
            var cts = getCts();
            var task = getTask();
            setCts(null);
            setTask(Task.CompletedTask);
            TryCancel(cts);
            await AwaitPageOperationAsync(task);
            cts?.Dispose();
        }
        finally {
            gate.Release();
        }
    }

    private Player? pendingSkinPlayer;

    private void SkinBoxChangeTimer_OnTick(object? sender, EventArgs e) {
        StopSkinChangeTimer();
        if (pendingSkinPlayer != null) {
            updatePlayerSkinFunc(pendingSkinPlayer);
            pendingSkinPlayer = null;
        }
    }

    private void StopSkinChangeTimer() {
        if (SkinBoxChangeTimer == null) {
            return;
        }
        SkinBoxChangeTimer.Stop();
        SkinBoxChangeTimer.Tick -= SkinBoxChangeTimer_OnTick;
        SkinBoxChangeTimer = null;
    }
}
