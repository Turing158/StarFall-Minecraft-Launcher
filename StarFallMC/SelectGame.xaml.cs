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
using Newtonsoft.Json.Linq;
using StarFallMC.Entity;
using StarFallMC.Util;

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
        public event PropertyChangedEventHandler? PropertyChanged;
        
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
        Console.WriteLine("当前游戏版本："+GameSelect.SelectedIndex);
        var game = (MinecraftItem)GameSelect.SelectedItem;
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
        viewModel.CurrentDir = (DirItem)DirSelect.SelectedItem;
        loadGameByDir();
    }
    
    private void AddDir_OnClick(object sender, RoutedEventArgs e) {
        OpenFolderDialog ofd = new OpenFolderDialog();
        ofd.Title = "请选择.minecraft的根目录[上一级目录]或.minecraft目录";
        ofd.DefaultDirectory = DirFileUtil.CurrentDirPosition;
        if (ofd.ShowDialog() == true) {

            if (viewModel.Dirs.Any(i => i.Path == ofd.FolderName || i.Path == ofd.FolderName+"\\.minecraft")) {
                //提示存在该文件夹
                Console.WriteLine("该文件夹已存在");
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
            // DirList.Add(dirItem);
            DirSelect.SelectedIndex = viewModel.Dirs.Count - 1;
        }
        //添加文件夹
    }

    private void DelDir_OnClick(object sender, RoutedEventArgs e) {
        //删除文件夹
        if (DirSelect.SelectedIndex != 0) {
            var result = MessageBox.Show("是否要删除当前选中文件夹[只会在这里删除显示，并不会真正删除该文件夹内容]", "", MessageBoxButton.OKCancel);
            if (result.Equals(MessageBoxResult.OK)) {
                var index = DirSelect.SelectedIndex;
                DirSelect.SelectedIndex = 0;
                viewModel.Dirs.RemoveAt(index);
                // DirList.RemoveAt(index);
            }
        }
    }

    private void OpenDir_OnClick(object sender, RoutedEventArgs e) {
        //打开文件夹
        var dir = (DirItem)DirSelect.SelectedItem;
        DirFileUtil.openDirByExplorer(dir.Path);
    }

    private void RefreshDir_OnClick(object sender, RoutedEventArgs e) {
        //刷新文件夹
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
        //重新加载该地址内的Minecraft版本
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
        if (viewModel.CurrentGame != null && viewModel.CurrentGame.Name != "") {
            GameInfoMaskControl.Show();
        }
    }
    
    private void DelGame_OnClick(object sender, RoutedEventArgs e) {
        //删除版本
        if (viewModel.Games != null && viewModel.Games.Count != 0 && GameSelect.SelectedIndex != -1) {
            var result = MessageBox.Show("是否要删除当前选中Minecraft\n注意：会直接删除版本文件夹内的所有内容", "", MessageBoxButton.OKCancel);
            if (result.Equals(MessageBoxResult.OK)) {
                var index = GameSelect.SelectedIndex;
                viewModel.Games.RemoveAt(index);
                //执行删除版本文件
                
                
                //=================
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
            
        }
    }

    private void OpenVersionDir_OnClick(object sender, RoutedEventArgs e) {
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
            reloadGameByDir(viewModel.CurrentDir.Path);
        }
    }

    private void DefaultVersionIcon_OnClick(object sender, RoutedEventArgs e) {
        string path = viewModel.CurrentGame.Path + "/ico.png";
        if (File.Exists(path)) {
            File.Delete(path);
            reloadGameByDir(viewModel.CurrentDir.Path);
        }
    }

    private void OpenModDir_OnClick(object sender, RoutedEventArgs e) {
        string path = viewModel.CurrentGame.Path + "/mods";
        if (Directory.Exists(path)) {
            DirFileUtil.openDirByExplorer(path);
            return;
        }
        path = viewModel.CurrentDir.Path + "/mods";
        if (Directory.Exists(path)) {
            DirFileUtil.openDirByExplorer(path);
            return;
        }
        //提示不存在文件夹
    }

    private void OpenResourcesDir_OnClick(object sender, RoutedEventArgs e) {
        string path = viewModel.CurrentGame.Path + "/resources";
        if (Directory.Exists(path)) {
            DirFileUtil.openDirByExplorer(path);
            return;
        }
        path = viewModel.CurrentDir.Path + "/resources";
        if (Directory.Exists(path)) {
            DirFileUtil.openDirByExplorer(path);
            return;
        }
        //提示不存在文件夹
    }

    private void RenameVersion_OnKeyDown(object sender, KeyEventArgs e) {
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
                viewModel.CurrentGame = item;
                reloadGameByDir(viewModel.CurrentDir.Path);
                RenameVersion.Hide();
            }
        }
    }
    
    
    //方法备用，以免往后需要
    private void ToFixListViewRefresh(MinecraftItem item) {
        PropertiesUtil.loadJson["game"]["minecraft"] = JObject.FromObject(item);
        MainWindow.ReloadSubFrame?.Invoke("SelectGame", () => {
            globalTimer = new Timer(o => {
                Dispatcher.Invoke(() => {
                    SelectGame.GameInfoShow?.Invoke(null,null);
                    globalTimer.Dispose();
                });
            }, null, 50, 0);
        });
    }

    private void RenameVersion_OnOnClose(object sender, RoutedEventArgs e) {
        viewModel.RenameVersionText = "";
    }
    
}