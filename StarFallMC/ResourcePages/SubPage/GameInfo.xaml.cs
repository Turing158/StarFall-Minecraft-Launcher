using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.Entity.Loader;
using StarFallMC.Entity.Resource;
using StarFallMC.Util;
using ComboBox = StarFallMC.Component.ComboBox;

namespace StarFallMC.ResourcePages.SubPage;



public partial class GameInfo : Page {

    private ViewModel viewModel = new();
    
    public static Action<MinecraftDownloader> SetMinecraftDownloader;
    public static Action CancelLoading;
    private CancellationTokenSource cts;
    
    public GameInfo() {
        InitializeComponent();
        DataContext = viewModel;
        cts = new CancellationTokenSource();
        CancelLoading = cancelLoading;
        SetMinecraftDownloader = setMinecraftDownloader;
    }
    
    public class ViewModel : INotifyPropertyChanged {
        
        private MinecraftDownloader _downloader;
        public MinecraftDownloader Downloader {
            get => _downloader;
            set => SetField(ref _downloader, value);
        }

        private List<ForgeLoader> _forgeLoader;
        public List<ForgeLoader> ForgeLoader {
            get => _forgeLoader;
            set => SetField(ref _forgeLoader, value);
        }
        
        private List<LiteLoader> _liteLoader;
        public List<LiteLoader> LiteLoader {
            get => _liteLoader;
            set => SetField(ref _liteLoader, value);
        }
        
        private List<NeoForgeLoader> _neoForgeLoader;
        public List<NeoForgeLoader> NeoForgeLoader {
            get => _neoForgeLoader;
            set => SetField(ref _neoForgeLoader, value);
        }
        
        private List<OptifineLoader> _optifineLoader;
        public List<OptifineLoader> OptifineLoader {
            get => _optifineLoader;
            set => SetField(ref _optifineLoader, value);
        }
        
        private List<FabricLoader> _fabricLoader;
        public List<FabricLoader> FabricLoader {
            get => _fabricLoader;
            set => SetField(ref _fabricLoader, value);
        }
        
        private List<ModResource> _fabricApiVersions;
        public List<ModResource> FabricApiVersions {
            get => _fabricApiVersions;
            set => SetField(ref _fabricApiVersions, value);
        }
        
        private List<QuiltLoader> _quiltLoader;
        public List<QuiltLoader> QuiltLoader {
            get => _quiltLoader;
            set => SetField(ref _quiltLoader, value);
        }
        
        private ObservableCollection<ForgeLoader> _enabledForgeLoader;
        public ObservableCollection<ForgeLoader> EnabledForgeLoader {
            get => _enabledForgeLoader;
            set => SetField(ref _enabledForgeLoader, value);
        }

        private ObservableCollection<OptifineLoader> _enabledOptifineLoader;
        public ObservableCollection<OptifineLoader> EnabledOptifineLoader {
            get => _enabledOptifineLoader;
            set => SetField(ref _enabledOptifineLoader, value);
        }
        
        private ForgeLoader _selectedForgeLoader;
        public ForgeLoader SelectedForgeLoader {
            get => _selectedForgeLoader;
            set => SetField(ref _selectedForgeLoader, value);
        }
        
        private LiteLoader _selectedLiteLoader;
        public LiteLoader SelectedLiteLoader {
            get => _selectedLiteLoader;
            set => SetField(ref _selectedLiteLoader, value);
        }
        
        private NeoForgeLoader _selectedNeoForgeLoader;
        public NeoForgeLoader SelectedNeoForgeLoader {
            get => _selectedNeoForgeLoader;
            set => SetField(ref _selectedNeoForgeLoader, value);
        }
        
        private OptifineLoader _selectedOptifineLoader;
        public OptifineLoader SelectedOptifineLoader {
            get => _selectedOptifineLoader;
            set => SetField(ref _selectedOptifineLoader, value);
        }
        
        private FabricLoader _selectedFabricLoader = new ();
        public FabricLoader SelectedFabricLoader {
            get => _selectedFabricLoader;
            set => SetField(ref _selectedFabricLoader, value);
        }
        
        private QuiltLoader _selectedQuiltLoader;
        public QuiltLoader SelectedQuiltLoader {
            get => _selectedQuiltLoader;
            set => SetField(ref _selectedQuiltLoader, value);
        }
        
        private string _percentText;
        public string PercentText {
            get => _percentText;
            set => SetField(ref _percentText, value);
        }
        
        private bool _enableForge = true;
        public bool EnableForge {
            get => _enableForge;
            set => SetField(ref _enableForge, value);
        }
        
        private bool _enableLiteLoader = true;
        public bool EnableLiteLoader {
            get => _enableLiteLoader;
            set => SetField(ref _enableLiteLoader, value);
        }
        
        private bool _enableNeoForge = true;
        public bool EnableNeoForge {
            get => _enableNeoForge;
            set => SetField(ref _enableNeoForge, value);
        }
        
        private bool _enableOptifine = true;
        public bool EnableOptifine {
            get => _enableOptifine;
            set => SetField(ref _enableOptifine, value);
        }
        
        private bool _enableFabric = true;
        public bool EnableFabric {
            get => _enableFabric;
            set => SetField(ref _enableFabric, value);
        }
        
        private bool _enableQuilt = true;
        public bool EnableQuilt {
            get => _enableQuilt;
            set => SetField(ref _enableQuilt, value);
        }
        
        private Visibility _fabricApiVisibility = Visibility.Visible;
        public Visibility FabricApiVisibility {
            get => _fabricApiVisibility;
            set => SetField(ref _fabricApiVisibility, value);
        }
        
        private int _selectedFabricApiIndex = -1;
        public int SelectedFabricApiIndex {
            get => _selectedFabricApiIndex;
            set => SetField(ref _selectedFabricApiIndex, value);
        }

        private string _versionName;
        public string VersionName {
            get => _versionName;
            set => SetField(ref _versionName, value);
        }

        private bool _isGoodName = true;
        public bool IsGoodName {
            get => _isGoodName;
            set => SetField(ref _isGoodName, value);
        }
        
        private string _versionTips;
        public string VersionTips {
            get => _versionTips;
            set => SetField(ref _versionTips, value);
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
    
    private void setMinecraftDownloader(MinecraftDownloader downloader) {
        Dispatcher.BeginInvoke(() => {
            if (downloader == null) {
                return;
            }
            cts = new CancellationTokenSource();
            viewModel.Downloader = downloader;
            viewModel.VersionName = $"{viewModel.Downloader.Name}";
            viewModel.VersionTips = "";
            viewModel.IsGoodName = true;
            InitLoader().ConfigureAwait(false);
            Console.WriteLine($"选择{downloader.Name}");
        });
    }
    
    private async Task InitLoader() {
        if (viewModel.Downloader == null) {
            return;
        }
        
        viewModel.SelectedFabricLoader = null;
        viewModel.SelectedForgeLoader = null;
        viewModel.SelectedLiteLoader = null;
        viewModel.SelectedOptifineLoader = null;
        viewModel.SelectedQuiltLoader = null;
        viewModel.SelectedNeoForgeLoader = null;
        viewModel.SelectedFabricApiIndex = -1;
        
        viewModel.ForgeLoader?.Clear();
        viewModel.LiteLoader?.Clear();
        viewModel.NeoForgeLoader?.Clear();
        viewModel.OptifineLoader?.Clear();
        viewModel.FabricLoader?.Clear();
        viewModel.FabricApiVersions?.Clear();
        viewModel.QuiltLoader?.Clear();

        viewModel.EnableFabric = true;
        viewModel.EnableForge = true;
        viewModel.EnableLiteLoader = true;
        viewModel.EnableNeoForge = true;
        viewModel.EnableOptifine = true;
        viewModel.EnableQuilt = true;
        
        ResourcePageExtension.ReloadList(ScrollViewer,LoadingBorder);
        var progress = new Progress<int>(percent => {
            if (percent > 96) {
                viewModel.PercentText = $"整理中... {percent}%";
            }
            else if (percent > 81) {
                viewModel.PercentText = $"加载quiltmc... {percent}%";
            }
            else if (percent > 65) {
                viewModel.PercentText = $"加载fabric... {percent}%";
            }
            else if (percent > 49) {
                viewModel.PercentText = $"加载optifine... {percent}%";
            }
            else if (percent > 33) {
                viewModel.PercentText = $"加载neoforge... {percent}%";
            }
            else if (percent > 17) {
                viewModel.PercentText = $"加载liteloader... {percent}%";
            }
            else if (percent > 1) {
                viewModel.PercentText = $"加载forge... {percent}%";
            }
            else {
                viewModel.PercentText = $"加载中... {percent}%";
            }
        });
        var loader = await ResourceUtil.GetAllLoaderByMinecraftDownloader(viewModel.Downloader.Name,cts.Token,progress);
        
        viewModel.SelectedFabricLoader = null;
        viewModel.ForgeLoader = loader.Item1;
        viewModel.EnabledForgeLoader = new ObservableCollection<ForgeLoader>(loader.Item1);
        viewModel.LiteLoader = loader.Item2;
        viewModel.NeoForgeLoader = loader.Item3;
        viewModel.OptifineLoader = loader.Item4;
        viewModel.EnabledOptifineLoader = new ObservableCollection<OptifineLoader>(loader.Item4);
        viewModel.FabricLoader = loader.Item5;
        viewModel.FabricApiVersions = loader.Item6;
        viewModel.QuiltLoader = loader.Item7;
        viewModel.FabricApiVisibility = Visibility.Collapsed;
        ResourcePageExtension.AlreadyLoaded(this,ScrollViewer,LoadingBorder);
    }

    private void GameInfo_OnUnloaded(object sender, RoutedEventArgs e) {
        cts.Cancel();
    }

    private void FabricComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        var item = sender as ComboBox;
        if (item == null) {
            return;
        }

        if (item.CurrentItem is FabricLoader) {
            viewModel.FabricApiVisibility = Visibility.Visible;
        }
        else {
            viewModel.FabricApiVisibility = Visibility.Collapsed;
            viewModel.SelectedFabricApiIndex = -1;
        }
        LoaderComboBox_OnSelectionChanged(sender, e);
    }

    private void LoaderComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        var item = sender as ComboBox;
        if (item == null) {
            return;
        }
        
        if (item.CurrentItem is ForgeLoader forgeLoader) {
            viewModel.EnableLiteLoader = false;
            viewModel.EnableNeoForge = false;
            viewModel.EnableFabric = false;
            viewModel.EnableQuilt = false;
            viewModel.VersionName = $"{viewModel.Downloader.Name}-{secureVersionName(viewModel.SelectedForgeLoader.DisplayName)}";
            var enableOptifineLoader = new List<OptifineLoader>();
            enableOptifineLoader.AddRange(viewModel.OptifineLoader.Where(x => x.NeedForge == null || x.NeedForge.Build == forgeLoader.Build));
            viewModel.EnabledOptifineLoader = new ObservableCollection<OptifineLoader>(enableOptifineLoader);
        }
        else if (item.CurrentItem is LiteLoader liteLoader) {
            viewModel.EnableForge = false;
            viewModel.EnableNeoForge = false;
            viewModel.EnableOptifine = false;
            viewModel.EnableFabric = false;
            viewModel.EnableQuilt = false;
            viewModel.VersionName = $"{viewModel.Downloader.Name}-{secureVersionName(viewModel.SelectedLiteLoader.DisplayName)}";
        }
        else if (item.CurrentItem is NeoForgeLoader neoForgeLoader) {
            viewModel.EnableForge = false;
            viewModel.EnableLiteLoader = false;
            viewModel.EnableOptifine = false;
            viewModel.EnableFabric = false;
            viewModel.EnableQuilt = false;
            viewModel.VersionName = $"{viewModel.Downloader.Name}-{secureVersionName(viewModel.SelectedNeoForgeLoader.DisplayName)}";
        }
        else if (item.CurrentItem is OptifineLoader optifineLoader) {
            viewModel.EnableLiteLoader = false;
            viewModel.EnableNeoForge = false;
            viewModel.EnableFabric = false;
            viewModel.EnableQuilt = false;
            if (viewModel.SelectedForgeLoader == null) {
                viewModel.VersionName =  $"{viewModel.Downloader.Name}-{secureVersionName(viewModel.SelectedOptifineLoader.DisplayName)}";
            }
            var enableForgeLoader = new List<ForgeLoader>();
            enableForgeLoader.AddRange(viewModel.ForgeLoader.Where(x => optifineLoader.NeedForge == null || optifineLoader.NeedForge.Build == x.Build));
            viewModel.EnabledForgeLoader = new ObservableCollection<ForgeLoader>(enableForgeLoader);
        }
        else if (item.CurrentItem is FabricLoader fabricLoader) {
            viewModel.EnableForge = false;
            viewModel.EnableLiteLoader = false;
            viewModel.EnableNeoForge = false;
            viewModel.EnableOptifine = false;
            viewModel.EnableQuilt = false;
            viewModel.VersionName = $"{viewModel.Downloader.Name}-{secureVersionName(viewModel.SelectedFabricLoader.DisplayName)}";
        }
        else if (item.CurrentItem is QuiltLoader quiltLoader) {
            viewModel.EnableForge = false;
            viewModel.EnableLiteLoader = false;
            viewModel.EnableNeoForge = false;
            viewModel.EnableOptifine = false;
            viewModel.EnableFabric = false;
            viewModel.VersionName = $"{viewModel.Downloader.Name}-{secureVersionName(viewModel.SelectedQuiltLoader.DisplayName)}";
        }
        else {
            if (viewModel.SelectedOptifineLoader != null) {
                viewModel.VersionName =  $"{viewModel.Downloader.Name}-{secureVersionName(viewModel.SelectedOptifineLoader.DisplayName)}";
                viewModel.EnabledOptifineLoader = new ObservableCollection<OptifineLoader>(viewModel.OptifineLoader);
            }
            else if (viewModel.SelectedForgeLoader != null) {
                viewModel.VersionName = $"{viewModel.Downloader.Name}-{secureVersionName(viewModel.SelectedForgeLoader.DisplayName)}";
                viewModel.EnabledForgeLoader = new ObservableCollection<ForgeLoader>(viewModel.ForgeLoader);
            }
            else {
                viewModel.EnableForge = true;
                viewModel.EnableLiteLoader = true;
                viewModel.EnableNeoForge = true;
                viewModel.EnableOptifine = true;
                viewModel.EnableFabric = true;
                viewModel.EnableQuilt = true;
                viewModel.VersionName = $"{viewModel.Downloader.Name}";
                viewModel.EnabledOptifineLoader = new ObservableCollection<OptifineLoader>(viewModel.OptifineLoader);
                viewModel.EnabledForgeLoader = new ObservableCollection<ForgeLoader>(viewModel.ForgeLoader);    
            }
            
        }
    }

    private string secureVersionName(string versionName) {
        return versionName.TrimStart(' ').TrimEnd(' ').TrimEnd('.')
            .Replace("\\","-").Replace("/","-").Replace(":", "-").Replace("*","-")
            .Replace("?","_").Replace("\"","_").Replace("<","_").Replace(">","_").Replace("|","_");
    }

    private void RefreshBtn_OnClick(object sender, RoutedEventArgs e) {
        cts?.Cancel();
        cts = new CancellationTokenSource();
        InitLoader();
    }
    private void VersionNameInput_OnLostFocus(object sender, RoutedEventArgs e) {
        var tb = sender as TextInput;
        viewModel.IsGoodName = true;
        if (string.IsNullOrEmpty(tb.Text) || tb.Text.Length == 0) {
            viewModel.VersionTips = "版本名称不能为空";
            viewModel.IsGoodName = false;
        }
        else if (tb.Text.StartsWith(' ')) {
            viewModel.VersionTips = "版本名称不能以'空格'开头";
            viewModel.IsGoodName = false;
        }
        else if (tb.Text.EndsWith(" ") || tb.Text.EndsWith(".")) {
            viewModel.VersionTips = "版本名称不能以'空格或.'结尾";
            viewModel.IsGoodName = false;
        }
        else if (!Regex.IsMatch(tb.Text,@"^[^\\/:*?""<>|]+$")) {
            viewModel.VersionTips = "版本名称不能包含[\\ / : * ? \" < > |]";
            viewModel.IsGoodName = false;
        }
        else {
            var sgvm = SelectGame.GetViewModel?.Invoke();
            if (sgvm != null) {
                var path = Path.Combine(sgvm.CurrentDir.Path, "versions", tb.Text);
                Console.WriteLine(path);
                if (Directory.Exists(path)) {
                    viewModel.VersionTips = "版本名称已存在";
                    viewModel.IsGoodName = false;
                }
            }
            else {
                var dir = PropertiesUtil.loadJson["game"]?["dir"]?.ToObject<DirItem>();
                var path = Path.Combine(dir.Path, "versions", tb.Text);
                Console.WriteLine(path);
                if (Directory.Exists(path)) {
                    viewModel.VersionTips = "版本名称已存在";
                    viewModel.IsGoodName = false;
                }
            }
        }
        if (viewModel.IsGoodName) {
            viewModel.VersionTips = "";
        }
    }
    
    private bool isInstalling = false;
    public static CancellationTokenSource installCts;
    
    private void StartInstall_OnClick(object sender, RoutedEventArgs e) {
        if (viewModel.IsGoodName) {
            if (!isInstalling) {
                if (viewModel.SelectedOptifineLoader != null && viewModel.SelectedOptifineLoader.NeedForge != null && viewModel.SelectedForgeLoader == null) {
                    MessageTips.Show("Optifine版本需要Forge支持，请选择对应的Forge版本后再安装", MessageTips.MessageType.Error);
                    return;
                }

                if (viewModel.SelectedFabricLoader != null && viewModel.SelectedFabricApiIndex == -1) {
                    MessageTips.Show("请选择Fabric API版本", MessageTips.MessageType.Error);
                    return;
                }
                isInstalling = true;
                PrepareInstall();
            }
            else {
                MessageTips.Show("请先完成当前安装任务", MessageTips.MessageType.Error);
            }
        }
        else {
            MessageTips.Show(viewModel.VersionTips, MessageTips.MessageType.Error);
        }
    }

    public async void PrepareInstall() {
        installCts = new CancellationTokenSource();
        string currentDir;
        var sgvm = SelectGame.GetViewModel?.Invoke();
        if (sgvm != null) {
            currentDir = sgvm.CurrentDir.Path;
        }
        else {
            var dir = PropertiesUtil.loadJson["game"]?["dir"]?.ToObject<DirItem>();
            currentDir = dir.Path;
        }

        if (viewModel.SelectedQuiltLoader != null) {
            MessageTips.Show("暂不支持安装QuiltLoader");
            return;
        }
        MainWindow.BackHandle.Invoke();
        MainWindow.DownloadPageShow.Invoke();
        if (viewModel.SelectedForgeLoader != null) {
            string forgeDownloadUrl = $"https://bmclapi2.bangbang93.com/forge/download/{viewModel.SelectedForgeLoader.Build}";
            await MinecraftUtil.StartDownloadInstallForge(
                viewModel.Downloader.Name,
                viewModel.VersionName,
                currentDir, 
                forgeDownloadUrl,
                viewModel.SelectedOptifineLoader);
        }
        else if (viewModel.SelectedOptifineLoader != null) {
            await MinecraftUtil.StartDownloadInstallOptiFine(
                viewModel.Downloader.Name,
                viewModel.VersionName,
                currentDir, 
                viewModel.SelectedOptifineLoader,
                installCts.Token);
        }
        else if (viewModel.SelectedLiteLoader != null) {
            await MinecraftUtil.StartDownloadInstallLiteloader(
                viewModel.Downloader.Name,
                viewModel.VersionName,
                currentDir, 
                viewModel.SelectedLiteLoader,
                installCts.Token);
        }
        else if (viewModel.SelectedNeoForgeLoader != null) {
            await MinecraftUtil.StartDownloadInstallNeoForge(
                viewModel.Downloader.Name,
                viewModel.VersionName,
                currentDir, 
                viewModel.SelectedNeoForgeLoader,
                installCts.Token);
        }                                                  
        else if (viewModel.SelectedFabricLoader != null) {
            await MinecraftUtil.StartDownloadInstallFabric(
                viewModel.Downloader.Name,
                viewModel.VersionName,
                currentDir, 
                viewModel.SelectedFabricLoader,
                viewModel.FabricApiVersions[viewModel.SelectedFabricApiIndex],
                installCts.Token);
        }
        else {
            await MinecraftUtil.StartDownloadInstallMinecraft(viewModel.Downloader.Name, viewModel.VersionName,currentDir, installCts.Token);
        }
        isInstalling = false;
        installCts.Cancel();
    }

    private void NeoForgeSelectComboBox_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
        if (viewModel.Downloader.Name == "1.20.1" && (sender as ComboBox).IsOpened) {
            MessageTips.Show("请你别怀疑，这真的是NeoForge的加载器版本列表！");
        }
    }

    private void cancelLoading() {
        cts?.Cancel();
    }
}