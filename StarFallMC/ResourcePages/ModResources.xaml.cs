using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using StarFallMC.Entity;
using StarFallMC.Entity.Resource;
using StarFallMC.ResourcePages.SubPage;
using StarFallMC.Util;
using StarFallMC.Util.Extension;

namespace StarFallMC.ResourcePages;

public partial class ModResources : Page {

    private ViewModel viewModel = new();

    private CancellationTokenSource cancellationTokenSource;
    public ModResources() {
        InitializeComponent();
        DataContext = viewModel;
        cancellationTokenSource = new CancellationTokenSource();
        viewModel.PercentText = "正在加载Mod列表... 0%";
    }
    
    
    private async Task InitResource() {
        VirtualizingStackPanel.SetIsVirtualizing(ListView, true);
        VirtualizingStackPanel.SetVirtualizationMode(ListView, VirtualizationMode.Recycling);
        ResourcePageExtension.ReloadList(ResourceContent,LoadingBorder,NotExist);
        if (ResourceUtil.ModResourceCache != null) {
            viewModel.UseCurseForge = ResourceUtil.ModResourceCache.UseCurseForge;
            Pagination.CurrentPage = ResourceUtil.ModResourceCache.CurrentPage;
            Pagination.TotalCount = ResourceUtil.ModResourceCache.TotalCount;
            viewModel.SearchText = ResourceUtil.ModResourceCache.SearchText;
            viewModel.SelectedLoader = ResourceUtil.ModResourceCache.SelectedLoader;
            viewModel.SelectedVersion = ResourceUtil.ModResourceCache.SelectedVersion;
            viewModel.SelectedCategory = ResourceUtil.ModResourceCache.SelectedCategory;
            viewModel.Mods = new ObservableCollection<ModResource>(ResourceUtil.ModResourceCache.List);
        }
        else {
            if (ResourceUtil.ModResourceCache == null || ResourceUtil.ModResourceCache.List.Count == 0) {
                await GetModResource();
            }
            else {
                if (ResourceUtil.ModResourceCache != null) {
                    viewModel.Mods = new ObservableCollection<ModResource>(ResourceUtil.ModResourceCache.List);
                }
            }
            Dispatcher.BeginInvoke(() => {
                Pagination.CurrentPage = 1;
            });
        }
    }

    private async Task GetModResource() {
        try {
            //获取网络mod资源，分为Modrinth和curseforge
            var tmp = new List<ModResource>();
            if (viewModel.UseCurseForge) {
                (tmp , Pagination.TotalCount) = await ResourceUtil.GetCurseForgeModResources(
                    cancellationTokenSource.Token,
                    Pagination.CurrentPage,
                    viewModel.SearchText,
                    viewModel.SelectedLoader,
                    viewModel.SelectedVersion,
                    ResourceCategory.CurseForgeCategoriesToInt(viewModel.SelectedCategory)
                );
            }
            else {
                (tmp , Pagination.TotalCount) = await ResourceUtil.GetModrinthModResources(
                    cancellationTokenSource.Token,
                    Pagination.CurrentPage,
                    viewModel.SearchText,
                    viewModel.SelectedLoader,
                    viewModel.SelectedVersion,
                    ResourceCategory.ModrinthCategoryToString(viewModel.SelectedCategory)
                );
            }

            if (ResourceUtil.ModResourceCache == null) {
                ResourceUtil.ModResourceCache = new ModResourceCache {
                    UseCurseForge = false,
                    List = tmp,
                    TotalCount = Pagination.TotalCount,
                    CurrentPage = Pagination.CurrentPage
                };
            }
            viewModel.Mods = new ObservableCollection<ModResource>(tmp);
        }
        catch (OperationCanceledException) {
            Console.WriteLine("取消加载Mod列表");
        }
        catch (Exception e){
            Console.WriteLine(e);
        }
    }
    
    public class ViewModel : INotifyPropertyChanged {

        private ObservableCollection<ModResource> _mods;

        public ObservableCollection<ModResource> Mods {
            get => _mods;
            set => SetField(ref _mods, value);
        }
        
        private string _percentText;
        
        public string PercentText {
            get => _percentText;
            set => SetField(ref _percentText, value);
        }
        
        private string _selectedLoader = "全部";
        public string SelectedLoader {
            get => _selectedLoader;
            set => SetField(ref _selectedLoader, value);
        }
        
        private string _selectedVersion = "全部";
        public string SelectedVersion {
            get => _selectedVersion;
            set => SetField(ref _selectedVersion, value);
        }
        private string _selectedCategory = "全部";
        public string SelectedCategory {
            get => _selectedCategory;
            set => SetField(ref _selectedCategory, value);
        }
        
        private bool _useCurseForge = false;
        public bool UseCurseForge {
            get => _useCurseForge;
            set => SetField(ref _useCurseForge, value);
        }
        
        private string _searchText = string.Empty;
        public string SearchText {
            get => _searchText;
            set => SetField(ref _searchText, value);
        }
        
        public List<string> Loaders { get; set; } = new List<string> {
            "全部",
            "Forge",
            "Fabric",
            "LiteLoader",
            "NeoForge",
            "Quilt"
        };
        
        public List<string> Categories { get; set; } = new List<string> {
            "全部",
            "美食",
            "装饰", 
            "生物",
            "魔法",
            "支持库",
            "科技",
            "装备",
            "运输",
            "世界元素",
            "服务器",
            "存储",
            "实用",
            "冒险"
        };
        
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
    
    private void ModResources_OnUnloaded(object sender, RoutedEventArgs e) {
        cancellationTokenSource.Cancel();
        if(ResourceUtil.ModResourceCache != null) {
            ResourceUtil.ModResourceCache.SearchText = viewModel.SearchText;
            ResourceUtil.ModResourceCache.SelectedLoader = viewModel.SelectedLoader;
            ResourceUtil.ModResourceCache.SelectedVersion = viewModel.SelectedVersion;
            ResourceUtil.ModResourceCache.SelectedCategory = viewModel.SelectedCategory;
            ResourceUtil.ModResourceCache.List = viewModel.Mods?.ToList() ?? new List<ModResource>();
            ResourceUtil.ModResourceCache.CurrentPage = Pagination.CurrentPage;
            ResourceUtil.ModResourceCache.UseCurseForge = viewModel.UseCurseForge;
        }
    }

    private void ModResources_OnLoaded(object sender, RoutedEventArgs e) {
        InitResource();
    }
    
    private void Pagination_OnPageChanged(object sender, SelectionChangedEventArgs e) {
        ChangePage().ConfigureAwait(false);
    }

    private async Task ChangePage() {
        viewModel.Mods = new ObservableCollection<ModResource>();
        ScrollViewerExtensions.AnimateScroll(MainScrollViewer,0);
        ResourcePageExtension.ReloadList(ResourceContent,LoadingBorder,NotExist);
        await GetModResource();
        ResourcePageExtension.AlreadyLoaded(this,ResourceContent,LoadingBorder,NotExist, viewModel.Mods.Count == 0);
    }

    private void Platform_OnClick(object sender, RoutedEventArgs e) {
        Pagination.CurrentPage = 1;
        ChangePage().ConfigureAwait(false);
    }

    private void ComboBox_OnDropDownOpened(object? sender, EventArgs e) {
        ScrollViewerExtensions.ScrollEnabled(MainScrollViewer, false);
    }
    
    private void ComboBox_OnDropDownClosed(object? sender, EventArgs e) {
        ScrollViewerExtensions.ScrollEnabled(MainScrollViewer, true);
    }

    private void Search_OnClick(object sender, RoutedEventArgs e) {
        SearchResource().ConfigureAwait(false);
    }

    private async Task SearchResource() {
        Pagination.CurrentPage = 1;
        viewModel.Mods = new ObservableCollection<ModResource>();
        if (string.IsNullOrEmpty(viewModel.SelectedVersion)) {
            viewModel.SelectedVersion = "全部";
        }
        ScrollViewerExtensions.AnimateScroll(MainScrollViewer,0);
        ResourcePageExtension.ReloadList(ResourceContent,LoadingBorder,NotExist);
        await GetModResource().ConfigureAwait(false);
        ResourcePageExtension.AlreadyLoaded(this,ResourceContent,LoadingBorder,NotExist, viewModel.Mods.Count == 0);
    }

    private void Reset_OnClick(object sender, RoutedEventArgs e) {
        viewModel.SearchText = string.Empty;
        viewModel.SelectedCategory = "全部";
        viewModel.SelectedLoader = "全部";
        viewModel.SelectedVersion = "全部";
    }

    private void ListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        var listView = sender as ListView;
        if (listView == null) {
            return;
        }
        if (listView.SelectedIndex < 0) {
            return;
        }
        
        var resource = listView.SelectedItem as ModResource;
        (sender as ListView).SelectedIndex = -1;
        if (resource == null) {
            return;
        }
        
        MainWindow.SubFrameNavigate.Invoke("/ResourcePages/SubPage/ModInfo",resource.DisplayName);
        Dispatcher.BeginInvoke(() => {
            ModInfo.SetResource?.Invoke(resource);
        });
    }
}