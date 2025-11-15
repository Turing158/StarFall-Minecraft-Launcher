using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.Util;
using MessageBox = StarFallMC.Component.MessageBox;

namespace StarFallMC;

public partial class SelectGame : Page {

    private ViewModel viewModel = new ViewModel();
    public static Func<ViewModel> GetViewModel;
    public static Action<object,RoutedEventArgs> unloadedAction;
    public static Action<Object, RoutedEventArgs> GameInfoShow;

    private Storyboard GameListChangeAnim;
    Timer GameSelectChangeTimer;
    Timer globalTimer;
    
    public SelectGame() {
        InitializeComponent();
        DataContext = viewModel;
        GetViewModel = GetViewModelFunc;
        unloadedAction = SelectGame_OnUnloaded;
        GameInfoShow = GameInfo_OnClick;
        PropertiesUtil.LoadSelectGameArgs(ref viewModel);
        GameListChangeAnim = (Storyboard) FindResource("GameListChangeAnim");
        DirSelect.SelectedIndex = viewModel.Dirs.IndexOf(viewModel.CurrentDir);
        reloadGameByDir(viewModel.CurrentDir.Path);
    }
    public class ViewModel : INotifyPropertyChanged {
        private MinecraftItem _currentGame;
        public MinecraftItem CurrentGame {
            get => _currentGame;
            set => SetField(ref _currentGame, value);
        }
        private DirItem _currentDir;
        public DirItem CurrentDir {
            get => _currentDir;
            set => SetField(ref _currentDir, value);
        }
        
        private ObservableCollection<MinecraftItem> _games;
        public ObservableCollection<MinecraftItem> Games {
            get => _games;
            set => SetField(ref _games, value);
        }

        private ObservableCollection<DirItem> _dirs;

        public ObservableCollection<DirItem> Dirs {
            get => _dirs;
            set => SetField(ref _dirs, value);
        }

        private string _renameVersionText;
        public string RenameVersionText {
            get=> _renameVersionText;
            set => SetField(ref _renameVersionText, value);
        }
        
        private string _renameVersionTips;
        public string RenameVersionTips {
            get=> _renameVersionTips;
            set => SetField(ref _renameVersionTips, value);
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
    
    private ViewModel GetViewModelFunc() {
        return viewModel;
    }
    
    private void GameSelect_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        var game = (MinecraftItem)GameSelect.SelectedItem;
        if (game!= null && !MinecraftUtil.GetMinecraftVersionExists(game)) {
            reloadGameByDir(viewModel.CurrentDir.Path);
        }
        Console.WriteLine("当前游戏版本："+GameSelect.SelectedIndex);
        if (game == null || game.Name == "") {
            Home.SetGameInfo?.Invoke(null);
        }
        else {
            Home.SetGameInfo?.Invoke(game);
        }
        viewModel.CurrentGame = game;
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
            var dirItem = DirSelect.SelectedItem as DirItem;
            MessageBox.Show($"是否要删除当前选中文件夹[{dirItem.Name}]\n[tips:只会在这里删除显示，并不会真正删除该文件夹内容]", "提示", MessageBox.BtnType.ConfirmAndCancel, result => {
                if (result == MessageBox.Result.Confirm) {
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
        GameListChangeAnim.Begin();
        if (GameSelectChangeTimer != null) {
            GameSelectChangeTimer.Dispose();
        }
        GameSelectChangeTimer = new Timer(new TimerCallback(state => {
            this.Dispatcher.BeginInvoke(() => {
                reloadGameByDir(dir.Path);
                GameSelectChangeTimer.Dispose();
            });
        }), null, 250, 0);
    }

    private void reloadGameByDir(string path) {
        var item = viewModel.CurrentGame;
        viewModel.Games = new ObservableCollection<MinecraftItem>(MinecraftUtil.GetMinecraft(path));
        if (viewModel.Games == null || viewModel.Games.Count == 0) {
            GameSelect.SelectedIndex = -1;
            NoGame.Visibility = Visibility.Visible;
            Home.SetGameInfo?.Invoke(null);
        }
        else {
            if (item != null && viewModel.Games.Any(i=>i.Name == item.Name)) {
                var currentGame = viewModel.Games.FirstOrDefault(i => i.Name == item.Name);
                GameSelect.SelectedIndex = viewModel.Games.IndexOf(currentGame);
            }
            else {
                GameSelect.SelectedIndex = 0;
            }
            NoGame.Visibility = Visibility.Hidden;
        }
    }

    private void SelectGame_OnUnloaded(object sender, RoutedEventArgs e) {
        PropertiesUtil.SaveSelectGameArgs();
    }

    public void GameInfo_OnClick(object sender, RoutedEventArgs e) {
        if (viewModel.CurrentGame != null && viewModel.CurrentGame.Name != "" && viewModel.Games.Count != 0) {
            GameInfoMaskControl.Show();
        }
    }
    
    private void DelGame_OnClick(object sender, RoutedEventArgs e) {
        if (viewModel.Games != null && viewModel.Games.Count != 0 && GameSelect.SelectedIndex != -1) {
            MessageBox.Show($"是否要删除当前选中Minecraft版本[{viewModel.CurrentGame.Name}]，真的会消失很久的喔！\n注意：会直接删除版本文件夹内的所有内容", "提示", MessageBox.BtnType.ConfirmAndCancel, result => {
                if (result == MessageBox.Result.Confirm) {
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

    private void OpenModDir_OnClick(object sender, RoutedEventArgs e) {
        string path = MinecraftUtil.GetMinecraftGameDir(viewModel.CurrentDir.Path,viewModel.CurrentGame.Name) == Path.GetFullPath(viewModel.CurrentGame.Path) ?
            $"{viewModel.CurrentGame.Path}/mods" :
            $"{viewModel.CurrentDir.Path}/resourcepacks";
        if (Directory.Exists(path)) {
            DirFileUtil.openDirByExplorer(path);
            return;
        }
        MessageTips.Show($"不存在 resources 文件夹");
    }
    private bool isFixing = false;
    private string isFixingVersion = string.Empty;

    private void OpenResourcesDir_OnClick(object sender, RoutedEventArgs e) {
        string path = MinecraftUtil.GetMinecraftGameDir(viewModel.CurrentDir.Path,viewModel.CurrentGame.Name) == Path.GetFullPath(viewModel.CurrentGame.Path) ?
            $"{viewModel.CurrentGame.Path}/resourcepacks" :
            $"{viewModel.CurrentDir.Path}/resourcepacks";
        if (Directory.Exists(path)) {
            DirFileUtil.openDirByExplorer(path);
            return;
        }
        MessageTips.Show($"不存在 resources 文件夹");
    }

    private void RenameVersion_OnKeyDown(object sender, KeyEventArgs e) {
        if (isFixing && isFixingVersion == viewModel.CurrentGame.Name) {
            MessageTips.Show($"当前正在补全 {isFixingVersion} 的资源文件，不能修改版本名称");
            return;
        }
        var tb = sender as TextBox;
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
                var item = MinecraftUtil.RenameVersion(viewModel.CurrentGame, tb.Text);
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