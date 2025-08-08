using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Win32;
using StarFallMC.Entity;
using StarFallMC.Util;

namespace StarFallMC.SettingPages;

public partial class GameSetting : Page {

    private ViewModel viewModel = new ViewModel();
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
        MinMemory.Text = (MemorySlider.Minimum/1024).ToString("F1")+"G";
        MaxMemory.Text = (MemorySlider.Maximum/1024).ToString("F1")+"G";
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
    

    private void Auto_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        //自动导入java列表
        refreshJavaVersions();
    }
    
    private void refreshJavaVersions() {
        var javaVersions = MinecraftUtil.GetJavaVersions();
        javaVersions.Insert(0,new JavaItem("自动选择Java","", ""));
        viewModel.JavaVersions = new ObservableCollection<JavaItem>(javaVersions);
        JavaList.SelectedIndex = 0;
    }

    private void AddJavaList_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        //添加java列表
        OpenFileDialog ofd = new OpenFileDialog();
        ofd.Filter = "Java Executable|javaw.exe";
        ofd.Title = "选择Java可执行文件";
        ofd.InitialDirectory = DirFileUtil.CurrentDirPosition;
        if (ofd.ShowDialog() == true) {
            string selectedPath = ofd.FileName;
            Console.WriteLine("选择的Java路径：" + selectedPath);
            //处理java版本和路径
            
        }
    }

    private void Memory_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        var slider = sender as Slider;
        if (slider == null) return;
        Point position = e.GetPosition(slider);
        double value = 0;
        if (slider.Orientation == Orientation.Horizontal) {
            value = (position.X / slider.ActualWidth) * (slider.Maximum - slider.Minimum) + slider.Minimum;
        }
        else {
            value = (position.Y / slider.ActualHeight) * (slider.Maximum - slider.Minimum) + slider.Minimum;
        }
        slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, value));
    }

    private void Memory_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
        CurrentMemory.Text = ((double)viewModel.MemoryValue / 1024).ToString("F1")+"G";
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

    private void IsIsolation_OnMouseDown(object sender, MouseButtonEventArgs e) {
        //切换隔离模式
    }
    

    private void IsFullScreen_OnClick(object sender, RoutedEventArgs e) {
        //切换全屏模式
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
        //限制输入数字
        if (!char.IsDigit(e.Text, 0)) {
            e.Handled = true;
        }
    }

    private void GameSetting_OnUnloaded(object sender, RoutedEventArgs e) {
        PropertiesUtil.SaveGameSettingArgs();
    }

    private void JavaList_OnDropDownOpened(object? sender, EventArgs e) {
        ScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
    }

    private void JavaList_OnDropDownClosed(object? sender, EventArgs e) {
        ScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
    }
}