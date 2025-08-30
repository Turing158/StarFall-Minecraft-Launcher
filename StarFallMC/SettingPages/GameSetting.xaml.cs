using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.Util;
using Path = System.IO.Path;

namespace StarFallMC.SettingPages;

public partial class GameSetting : Page {

    private ViewModel viewModel = new ();
    public static Func<ViewModel> GetViewModel;
    public static Action<object,RoutedEventArgs> unloadedAction;
    
    private StringBuilder sb;
    
    public GameSetting() {
        InitializeComponent();
        DataContext = viewModel;
        GetViewModel += GetViewModelFunc;
        unloadedAction += GameSetting_OnUnloaded;
        initInfo();
    }
    
    private void initInfo() {
        var freeMemory = MinecraftUtil.GetMemoryAllInfo()[MinecraftUtil.MemoryName.FreeMemory];
        viewModel.MemoryValue = (int)(freeMemory * 2 / 3 < 656 ? 656 : freeMemory * 2 / 3);
        PropertiesUtil.LoadGameSettingArgs(ref viewModel);
        RefleshMemory();
        JvmExtraArea.ToolTip = "JVM参数\n\n"+
                               "-X、-XX参数\n" +
                               "配置JVM，如GC等：\n" +
                               "-Xmx1024m 最大堆大小为1024MB\n" +
                               "-Xmn128m 新生代堆大小为128MB\n" +
                               "-XX:+UseG1GC 开启G1\n" +
                               "-XX:-UseAdaptiveSizePolicy 自动选择年轻代区大小和相应的Survivor区比例\n" +
                               "-XX:-OmitStackTraceInFastThrow 省略异常栈信息从而快速抛出\n";
        GameTail.ToolTip = "Minecraft参数\n" +
                           "参数通常有：\n" +
                           "--username 后接用户名\n" +
                           "--version 后接游戏版本\n" +
                           "--gameDir 后接游戏路径\n" +
                           "--assetsDir 后接资源文件路径\n" +
                           "--assetIndex 后接资源索引版本\n" +
                           "--uuid 后接用户UUID\n" +
                           "--accessToken 后接登录令牌\n" +
                           "--userType 后接用户类型\n" +
                           "--versionType 后接版本类型，会显示在游戏主界面右下角\n" +
                           "--width 后接窗口宽度\n" +
                           "--height 后接窗口高度\n" +
                           "--server 后接服务器地址，游戏进入时将直接连入服务器\n" +
                           "--port 后接服务器的端口号";
    }

    private void RefleshMemory() {
        Dictionary<MinecraftUtil.MemoryName,double> memoryAllInfo = MinecraftUtil.GetMemoryAllInfo();
        var freeMemory = memoryAllInfo[MinecraftUtil.MemoryName.FreeMemory];
        MemorySlider.Maximum = freeMemory;
        MemorySlider.Minimum = freeMemory*1/9 < 656 ? 656 : freeMemory*1/9;
        MemorySlider.MinimumText = (MemorySlider.Minimum/1024).ToString("F1")+"G";
        MemorySlider.MaximumText = (MemorySlider.Maximum/1024).ToString("F1")+"G";
        if (!viewModel.AutoMemoryDisable || MemorySlider.Value > freeMemory) {
            MemorySlider.Value = freeMemory * 2 / 3;
        }
    }
    
    public class ViewModel : INotifyPropertyChanged {
        private int _currentJavaVersionIndex;
        public int CurrentJavaVersionIndex {
            get { return _currentJavaVersionIndex; }
            set {
                _currentJavaVersionIndex = value;
                OnPropertyChanged(nameof(CurrentJavaVersionIndex));
            }
        }
        
        private ObservableCollection<JavaItem> _javaVersions;
        public ObservableCollection<JavaItem> JavaVersions {
            get { return _javaVersions; }
            set {
                _javaVersions = value;
                OnPropertyChanged(nameof(JavaVersions));
            }
        }
        
        private bool _autoMemoryDisable;
        public bool AutoMemoryDisable {
            get { return _autoMemoryDisable; }
            set {
                _autoMemoryDisable = value;
                OnPropertyChanged(nameof(_autoMemoryDisable));
            }
        }
        
        private int _memoryValue;
        public int MemoryValue {
            get { return _memoryValue; }
            set {
                _memoryValue = value;
                OnPropertyChanged(nameof(MemoryValue));
            }
        }
        
        private bool _isIsolation = true;
        public bool IsIsolation {
            get { return _isIsolation; }
            set {
                _isIsolation = value;
                OnPropertyChanged(nameof(IsIsolation));
            }
        }
        
        private bool _isFullScreen;
        public bool IsFullScreen {
            get { return _isFullScreen; }
            set {
                _isFullScreen = value;
                OnPropertyChanged(nameof(IsFullScreen));
            }
        }
        
        private string _gameWidth = "854";
        public string GameWidth {
            get { return _gameWidth; }
            set {
                _gameWidth = value;
                OnPropertyChanged(nameof(GameWidth));
            }
        }
        
        private string _gameHeight = "480";
        public string GameHeight {
            get { return _gameHeight; }
            set {
                _gameHeight = value;
                OnPropertyChanged(nameof(GameHeight));
            }
        }

        private string _windowTitle;
        public string WindowTitle {
            get { return _windowTitle; }
            set {
                _windowTitle = value;
                OnPropertyChanged(nameof(WindowTitle));
            }
        }

        private string _customInfo = "StarFallMC";
        public string CustomInfo {
            get { return _customInfo; }
            set {
                _customInfo = value;
                OnPropertyChanged(nameof(CustomInfo));
            }
        }
        
        private bool _jvmExtraAreaEnable;
        public bool JvmExtraAreaEnable {
            get { return _jvmExtraAreaEnable; }
            set {
                _jvmExtraAreaEnable = value;
                OnPropertyChanged(nameof(JvmExtraAreaEnable));
            }
        }
        
        private string _jvmExtra;
        public string JvmExtra {
            get { return _jvmExtra; }
            set {
                _jvmExtra = value;
                OnPropertyChanged(nameof(JvmExtra));
            }
        }
        
        private string _gameTailArgs;
        public string GameTailArgs {
            get { return _gameTailArgs; }
            set {
                _gameTailArgs = value;
                OnPropertyChanged(nameof(GameTailArgs));
            }
        }
        
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    private ViewModel GetViewModelFunc() {
        return viewModel;
    }
    
    private void Auto_OnClick(object sender, RoutedEventArgs routedEventArgs) {
        refreshJavaVersions();
        MessageTips.Show("自动导入Java版本成功");
    }
    
    private void refreshJavaVersions() {
        var javaVersions = MinecraftUtil.GetJavaVersions();
        javaVersions.Insert(0,new JavaItem("自动选择Java","", ""));
        viewModel.JavaVersions = new ObservableCollection<JavaItem>(javaVersions);
        JavaList.SelectedIndex = 0;
    }

    private async void AddJavaList_OnClick(object sender, RoutedEventArgs routedEventArgs) {
        MinecraftUtil.GetJavaVersion("E:\\Programmer");
        OpenFileDialog ofd = new OpenFileDialog();
        ofd.Filter = "Java Executable|javaw.exe;java.exe";
        ofd.Title = "选择Java可执行文件";
        ofd.InitialDirectory = DirFileUtil.CurrentDirPosition;
        if (ofd.ShowDialog() == true) {
            string selectedPath = ofd.FileName;
            string path = DirFileUtil.GetParentPath(DirFileUtil.GetParentPath(selectedPath));
            if (viewModel.JavaVersions.Any(i => i.Path == path)) {
                MessageTips.Show("该Java已存在",MessageTips.MessageType.Warning);
                return;
            }
            string version = await MinecraftUtil.GetJavaVersion(path).ConfigureAwait(false);
            if (version != null) {
                Dispatcher.BeginInvoke(() => {
                    viewModel.JavaVersions.Add(new JavaItem(Path.GetFileName(path), path, version));
                    MessageTips.Show("添加Java成功");
                });
            }
            else {
                MessageTips.Show("添加Java失败",MessageTips.MessageType.Error);
            }
        }
    }

    private void Memory_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
        MemorySlider.ValueText = ((double)viewModel.MemoryValue / 1024).ToString("F1")+"G";
        MemorySlider.Interval = 100;
    }


    private void MemoryMode_OnClick(object sender, RoutedEventArgs e) {
        if (MemoryMode.IsChecked == true) {
            MemorySlider.IsEnabled = true;
        }
        else {
            MemorySlider.IsEnabled = false;
        }
        RefleshMemory();
    }
    
    private void IsFullScreen_OnClick(object sender, RoutedEventArgs e) {
        if (IsFullScreen.IsChecked == true) {
            GameWidth.IsEnabled = false;
            GameHeight.IsEnabled = false;
        }
        else {
            GameWidth.IsEnabled = true;
            GameHeight.IsEnabled = true;
        }
    }

    private void GameHeight_OnLostFocus(object sender, RoutedEventArgs e) {
        if (!matchTextBoxNumber(sender)) {
            viewModel.GameHeight = "480";
        }
        viewModel.GameHeight = matchTextBoxNumberBegin(GameHeight.Text);
        Console.WriteLine("GameHeight :"+viewModel.GameHeight);
    }

    private void GameWidth_OnLostFocus(object sender, RoutedEventArgs e) {
        if (!matchTextBoxNumber(sender)) {
            viewModel.GameWidth = "854";
        }
        viewModel.GameWidth = matchTextBoxNumberBegin(GameWidth.Text);
        Console.WriteLine("GameHeight :"+viewModel.GameWidth);
    }

    private bool matchTextBoxNumber(Object sender) {
        var comp = sender as TextBox;
        Regex regex = new Regex("^\\d+$");
        if (comp == null || !regex.IsMatch(comp.Text) || comp.Text == "") {
            Console.WriteLine("只能为数字");
            return false;
        }
        return true;
    }
    
    private string matchTextBoxNumberBegin(string numStr) {
        Regex regex = new Regex("^[1-9]\\d*$");
        if (regex.IsMatch(numStr)) {
            return numStr;
        }

        string trimmed  = numStr.TrimStart('0');
        return string.IsNullOrEmpty(trimmed) ? "0" : trimmed;
    }
    
    private void JvmExtraAreaEnable_OnClick(object sender, RoutedEventArgs e) {
        if (JvmExtraAreaEnable.IsChecked == true) {
            JvmExtraArea.IsEnabled = true;
        }
        else {
            JvmExtraArea.IsEnabled = false;
        }
    }

    private void GameWidth_OnPreviewTextInput(object sender, TextCompositionEventArgs e) {
        if (!char.IsDigit(e.Text, 0)) {
            e.Handled = true;
        }
    }

    private void GameSetting_OnUnloaded(object sender, RoutedEventArgs e) {
        PropertiesUtil.SaveGameSettingArgs();
    }
}