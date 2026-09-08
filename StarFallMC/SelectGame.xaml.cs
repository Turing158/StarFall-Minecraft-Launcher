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
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.Entity.Enum;
using StarFallMC.Util;
using StarFallMC.Services;
using StarFallMC.Services.Minecraft;
using StarFallMC.Services.Download;
using StarFallMC.Navigation;
using MessageBox = StarFallMC.Component.MessageBox;
using MessageBoxResult = StarFallMC.Entity.Enum.MessageBoxResult;

namespace StarFallMC;

public partial class SelectGame : Page, IPageLifecycle {

    private readonly GameSelectionState viewModel = ApplicationState.GameSelection;
    private readonly MinecraftServiceContainer minecraftServices;
    private readonly LauncherUiCoordinator uiCoordinator;
    private readonly DownloadCoordinator downloadCoordinator;

    private Storyboard GameListChangeAnim;
    private DispatcherTimer? GameSelectChangeTimer;
    private string? pendingDirectoryPath;
    private bool lifecycleActive = true;
    
    public SelectGame(
        MinecraftServiceContainer? services = null,
        LauncherUiCoordinator? uiCoordinator = null,
        DownloadCoordinator? downloadCoordinator = null) {
        minecraftServices = services ?? MinecraftServices.Current;
        this.uiCoordinator = uiCoordinator ?? new LauncherUiCoordinator();
        this.downloadCoordinator = downloadCoordinator ?? new DownloadCoordinator(this.uiCoordinator);
        InitializeComponent();
        DataContext = viewModel;
        this.uiCoordinator.Register(this);
        GameListChangeAnim = (Storyboard) FindResource("GameListChangeAnim");
        DirSelect.SelectedIndex = viewModel.Dirs.IndexOf(viewModel.CurrentDir);
        reloadGameByDir(viewModel.CurrentDir.Path);
    }
    private void GameSelect_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        var game = (MinecraftItem)GameSelect.SelectedItem;
        if (game!= null && !minecraftServices.Paths.IsVersionPresent(game)) {
            reloadGameByDir(viewModel.CurrentDir.Path);
        }
        Console.WriteLine("当前游戏版本："+GameSelect.SelectedIndex);
        if (game == null || game.Name == "") {
            uiCoordinator.SetHomeGame(null);
        }
        else {
            uiCoordinator.SetHomeGame(game);
        }
        viewModel.CurrentGame = game ?? new MinecraftItem();
    }
    
    private void DirSelect_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        Console.WriteLine("当前文件夹:"+DirSelect.SelectedIndex);
        var item = (DirItem)DirSelect.SelectedItem;
        viewModel.CurrentDir = (DirItem)DirSelect.SelectedItem;
        loadGameByDir();
    }
    
    private void AddDir_OnClick(object sender, RoutedEventArgs e) {
        OpenFolderDialog ofd = new OpenFolderDialog();
        ofd.Title = "请选择.minecraft的根目录[上一级目录]或.minecraft目录";
        ofd.DefaultDirectory = DirFileUtil.CurrentDirPosition;
        if (ofd.ShowDialog() == true) {
            if (viewModel.Dirs.Any(i => i.Path == ofd.FolderName || i.Path == ofd.FolderName+"\\.minecraft")) {
                MessageTips.Show($"该文件夹已存在");
                return;
            }
            string check = Path.GetFileName(ofd.FolderName);
            DirItem dirItem = new DirItem();
            if (check == ".minecraft") {
                dirItem.Name = DirFileUtil.GetParentDirName(ofd.FolderName);
                dirItem.Path = ofd.FolderName;
            }
            else {
                dirItem.Name = Path.GetFileName(ofd.FolderName);
                dirItem.Path = Path.GetFullPath(ofd.FolderName+"/.minecraft");
            }
            viewModel.Dirs.Add(dirItem);
            MessageTips.Show($"成功添加文件夹 {dirItem.Name}");
            DirSelect.SelectedIndex = viewModel.Dirs.Count - 1;
        }
    }

    private void DelDir_OnClick(object sender, RoutedEventArgs e) {
        if (DirSelect.SelectedIndex != 0) {
            if (DirSelect.SelectedItem is not DirItem dirItem) {
                return;
            }
            MessageBox.Show($"是否要删除当前选中文件夹[{dirItem.Name}]\n[tips:只会在这里删除显示，并不会真正删除该文件夹内容]", "提示", MessageBoxBtnType.ConfirmAndCancel, result => {
                if (result == MessageBoxResult.Confirm) {
                    var index = DirSelect.SelectedIndex;
                    DirSelect.SelectedIndex = 0;
                    MessageTips.Show($"成功移除列表中文件夹 {dirItem.Name}");
                    viewModel.Dirs.RemoveAt(index);
                }
            },"","删除");
        }
    }

    private void OpenDir_OnClick(object sender, RoutedEventArgs e) {
        var dir = (DirItem)DirSelect.SelectedItem;
        MessageTips.Show($"已打开文件夹 {dir.Name}");
        DirFileUtil.openDirByExplorer(dir.Path);
    }

    private void RefreshDir_OnClick(object sender, RoutedEventArgs e) {
        MessageTips.Show($"已刷新文件夹 {viewModel.CurrentDir.Name}");
        loadGameByDir();
    }
    
    private void loadGameByDir() {
        var dir = (DirItem)DirSelect.SelectedItem;
        GameListChangeAnim.Begin(this, true);
        StopGameSelectTimer();
        pendingDirectoryPath = dir.Path;
        GameSelectChangeTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        GameSelectChangeTimer.Tick += GameSelectChangeTimer_OnTick;
        GameSelectChangeTimer.Start();
    }

    private void reloadGameByDir(string path) {
        var item = viewModel.CurrentGame;
        viewModel.Games = new ObservableCollection<MinecraftItem>(minecraftServices.Paths.ScanGames(path, minecraftServices.Resolver));
        if (viewModel.Games == null || viewModel.Games.Count == 0) {
            GameSelect.SelectedIndex = -1;
            NoGame.Visibility = Visibility.Visible;
            uiCoordinator.SetHomeGame(null);
        }
        else {
            if (item != null && viewModel.Games.Any(i=>i.Name == item.Name)) {
                var currentGame = viewModel.Games.FirstOrDefault(i => i.Name == item.Name);
                if (currentGame is not null) {
                    GameSelect.SelectedIndex = viewModel.Games.IndexOf(currentGame);
                }
            }
            else {
                GameSelect.SelectedIndex = 0;
            }
            NoGame.Visibility = Visibility.Hidden;
        }
    }

    private void SelectGame_OnUnloaded(object sender, RoutedEventArgs e) {
        StopGameSelectTimer();
        uiCoordinator.Unregister(this);
        PropertiesUtil.SaveSelectGameArgs();
    }

    public void GameInfo_OnClick(object sender, RoutedEventArgs e) {
        if (viewModel.CurrentGame != null && viewModel.CurrentGame.Name != "" && viewModel.Games.Count != 0) {
            GameInfoMaskControl.Show();
        }
    }

    public Task ActivateAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        lifecycleActive = true;
        uiCoordinator.Register(this);
        return Task.CompletedTask;
    }

    public async Task DeactivateAsync() {
        lifecycleActive = false;
        var currentCts = fixResourceCts;
        var currentTask = activeFixResourceTask;
        fixResourceCts = null;
        activeFixResourceTask = Task.CompletedTask;
        currentCts?.Cancel();
        try {
            await currentTask;
        }
        catch (OperationCanceledException) {
        }
        currentCts?.Dispose();
        StopGameSelectTimer();
        uiCoordinator.Unregister(this);
        PropertiesUtil.SaveSelectGameArgs();
    }

    private void GameSelectChangeTimer_OnTick(object? sender, EventArgs e) {
        StopGameSelectTimer();
        if (lifecycleActive && !string.IsNullOrEmpty(pendingDirectoryPath)) {
            reloadGameByDir(pendingDirectoryPath);
        }
        pendingDirectoryPath = null;
    }

    private void StopGameSelectTimer() {
        if (GameSelectChangeTimer == null) {
            return;
        }
        GameSelectChangeTimer.Stop();
        GameSelectChangeTimer.Tick -= GameSelectChangeTimer_OnTick;
        GameSelectChangeTimer = null;
    }
    
    private void DelGame_OnClick(object sender, RoutedEventArgs e) {
        if (viewModel.Games != null && viewModel.Games.Count != 0 && GameSelect.SelectedIndex != -1) {
            MessageBox.Show($"是否要删除当前选中Minecraft版本[{viewModel.CurrentGame.Name}]，真的会消失很久的喔！\n注意：会直接删除版本文件夹内的所有内容", "提示", MessageBoxBtnType.ConfirmAndCancel, result => {
                if (result == MessageBoxResult.Confirm) {
                    DirFileUtil.DeleteDirAllContent(viewModel.CurrentGame.Path);
                    var index = GameSelect.SelectedIndex;
                    MessageTips.Show($"成功删除Minecraft版本 {viewModel.CurrentGame.Name}");
                    viewModel.Games.RemoveAt(index);
                    GameInfoMaskControl.Hide();
                    if (viewModel.Games.Count != 0) {
                        if (index == 0) {
                            GameSelect.SelectedIndex = 0;
                        }
                        else {
                            GameSelect.SelectedIndex = index-1;
                        }
                    }
                    else {
                        GameSelect.SelectedIndex = -1;
                    }
                }
            }, "", "删除");
        }
    }

    private void OpenVersionDir_OnClick(object sender, RoutedEventArgs e) {
        MessageTips.Show($"已打开 {viewModel.CurrentGame.Name} 版本文件夹");
        DirFileUtil.openDirByExplorer(viewModel.CurrentGame.Path);
    }

    private void SettingVersionName_OnClick(object sender, RoutedEventArgs e) {
        viewModel.RenameVersionText = viewModel.CurrentGame.Name;
        RenameVersion.Show();
    }

    
    private void SettingVersionIcon_OnClick(object sender, RoutedEventArgs e) {
        OpenFileDialog ofd = new OpenFileDialog();
        ofd.Filter = "图片文件(*.png)|*.png";
        ofd.Title = "请选择一个图片作为Minecraft版本图标，建议使用正方形且较小的图片";
        ofd.DefaultDirectory = DirFileUtil.CurrentDirPosition;
        if (ofd.ShowDialog() == true) {
            DefaultVersionIcon_OnClick(sender, e);
            File.Copy(ofd.FileName, viewModel.CurrentGame.Path + "/ico.png",true);
            MessageTips.Show($"已修改 {viewModel.CurrentGame.Name} 的版本图标");
            reloadGameByDir(viewModel.CurrentDir.Path);
        }
    }

    private void DefaultVersionIcon_OnClick(object sender, RoutedEventArgs e) {
        string path = viewModel.CurrentGame.Path + "/ico.png";
        if (File.Exists(path)) {
            File.Delete(path);
            MessageTips.Show($"已重置 {viewModel.CurrentGame.Name} 的版本图标");
            reloadGameByDir(viewModel.CurrentDir.Path);
        }
    }

    private bool isFixing = false;
    private string isFixingVersion = string.Empty;
    private CancellationTokenSource? fixResourceCts;
    private Task activeFixResourceTask = Task.CompletedTask;
    private async void FixResourceFile_OnClick(object sender, RoutedEventArgs e) {
        if (!isFixing && string.IsNullOrEmpty(isFixingVersion)) {
            var currentCts = new CancellationTokenSource();
            fixResourceCts = currentCts;
            activeFixResourceTask = FixResource(currentCts.Token);
            try {
                await activeFixResourceTask;
            }
            finally {
                if (ReferenceEquals(fixResourceCts, currentCts)) {
                    fixResourceCts = null;
                    activeFixResourceTask = Task.CompletedTask;
                    currentCts.Dispose();
                }
            }
        }
        else {
            MessageTips.Show($"当前正在补全 {isFixingVersion} 的资源文件");
        }
    }

    private async Task FixResource(CancellationToken cancellationToken) {
        MinecraftItem minecraftItem = viewModel.CurrentGame.Clone();
        isFixingVersion = minecraftItem.Name;
        isFixing = true;
        try {
            string currentDir = DirFileUtil.GetParentPath(DirFileUtil.GetParentPath(minecraftItem.Path));
            string json = File.ReadAllText($"{minecraftItem.Path}/{minecraftItem.Name}.json");
            List<Lib> libs = minecraftServices.Resolver.GetLibs(json);
            MessageTips.Show("检查文件完整性...");
            var libFiles = minecraftServices.Loader.GetNeedLibrariesFile(libs, currentDir);
            var forgeFmlFile = minecraftServices.Loader.GetForgeFmlDownloadFile(json, currentDir);
            if (forgeFmlFile != null && minecraftItem.Loader == MinecraftLoader.Forge) {
                libFiles.Add(forgeFmlFile);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var assetFiles = await minecraftServices.Loader.GetAssetsFileAsync(json, currentDir, cancellationToken: cancellationToken);
            var needDownloadFiles = LoaderInstallService.GetNeedDownloadFile(assetFiles.Concat(libFiles));
            
            if (needDownloadFiles.Count != 0) {
                MessageTips.Show("补全文件中...");
                var downloadResult = await downloadCoordinator.StartDownload(needDownloadFiles, cancellationToken: cancellationToken);
                var errorFiles = downloadResult.Files.Where(result => !result.Success && !result.Cancelled).Select(result => result.File).ToList();
                while (errorFiles.Count != 0) {
                    bool retry = false;
                    await MessageBox.ShowAsync(
                        $"下载文件出现问题，共 {errorFiles.Count} 个文件出现错误。\n可能是网络波动问题，可选择重新下载 或 尝试重新启动 以及 前往 “版本属性” 处重新补全下载。",
                        "下载失败", MessageBoxBtnType.ConfirmAndCancel, r => { retry = r == MessageBoxResult.Confirm; },
                        confirmBtnText: "重新下载", cancelBtnText: "跳过");
                    if (retry) {
                        downloadResult = await downloadCoordinator.StartDownload(errorFiles, cancellationToken: cancellationToken);
                        errorFiles = downloadResult.Files.Where(result => !result.Success && !result.Cancelled).Select(result => result.File).ToList();
                    }
                    else {
                        MessageTips.Show($"补全 {minecraftItem.Name} 的资源文件时出现问题");
                        isFixingVersion = string.Empty;
                        isFixing = false;
                        return;
                    }
                }
            }

            if (minecraftItem.Loader == MinecraftLoader.Optifine) {
                MessageTips.Show("补全OptiFine文件中...");
                var OptiFineLib = libs.FirstOrDefault(i => i.name.Contains("OptiFine"));
                var launchwrapperLib = libs.FirstOrDefault(i => i.name.Contains("launchwrapper"));
                if ((launchwrapperLib != null && OptiFineLib != null) &&
                    (!File.Exists($"{currentDir}/libraries/{OptiFineLib.path}") ||
                     !File.Exists($"{currentDir}/libraries/{launchwrapperLib.path}"))) {
                    await minecraftServices.Loader.RepairOptifineAsync(OptiFineLib, currentDir, minecraftItem.Name, ApplicationState.GameSettings.IsIsolation, cancellationToken);
                }
            }

            if (forgeFmlFile != null && minecraftItem.Loader == MinecraftLoader.Forge) {
                MessageTips.Show("补全Forge文件中...");
                await minecraftServices.Loader.RepairForgeAsync(json, forgeFmlFile.FilePath, currentDir, minecraftItem.Name, cancellationToken);
            }

            await Task.Delay(500, cancellationToken);
            MessageTips.Show($"补全 {minecraftItem.Name} 的资源文件完成");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
        }
        catch (Exception e){
            Console.WriteLine(e);
            MessageTips.Show($"补全 {minecraftItem.Name} 的资源文件时出现问题");
        }
        isFixing = false;
        isFixingVersion = string.Empty;
    }

    private void OpenNativeDir_OnClick(object sender, RoutedEventArgs e) {
        string path = $"{viewModel.CurrentGame.Path}/{viewModel.CurrentGame.Name}-natives";
        Console.WriteLine($"Native 文件夹路径：{path}");
        if (Directory.Exists(path)) {
            DirFileUtil.openDirByExplorer(path);
            return;
        }
        MessageTips.Show($"不存在 {viewModel.CurrentGame.Name}-natives 文件夹");
    }

    private void RenameVersion_OnKeyDown(object sender, KeyEventArgs e) {
        if (isFixing && isFixingVersion == viewModel.CurrentGame.Name) {
            MessageTips.Show($"当前正在补全 {isFixingVersion} 的资源文件，不能修改版本名称");
            return;
        }
        if (sender is not TextBox tb) {
            return;
        }
        viewModel.RenameVersionText = tb.Text;
        if (tb.Text.Length == 0) {
            viewModel.RenameVersionTips = "名称不能为空";
        }
        else if (tb.Text.StartsWith(' ')) {
            viewModel.RenameVersionTips = "名称不能以'空格'开头";
        }
        else if (tb.Text.EndsWith(" ") || tb.Text.EndsWith(".")) {
            viewModel.RenameVersionTips = "名称不能以'空格或.'结尾";
        }
        else if (!Regex.IsMatch(tb.Text,@"^[^\\/:*?""<>|]+$")) {
            viewModel.RenameVersionTips = "不能包含[\\ / : * ? \" < > |]";
        }
        else {
            viewModel.RenameVersionTips = "";
            if (e.Key == Key.Enter) {
                var item = minecraftServices.Paths.RenameVersion(viewModel.CurrentGame, tb.Text);
                if (item == null) {
                    viewModel.RenameVersionTips = "已存在该名称文件夹或版本，请删除后重试";
                    return;
                }
                MessageTips.Show($"版本名称已修改为\n{item.Name}");
                viewModel.CurrentGame = item;
                reloadGameByDir(viewModel.CurrentDir.Path);
                RenameVersion.Hide();
            }
        }
    }

    private void RenameVersion_OnOnClose(object sender, RoutedEventArgs e) {
        viewModel.RenameVersionText = "";
    }
}
