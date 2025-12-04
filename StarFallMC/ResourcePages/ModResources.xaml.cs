using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using StarFallMC.Entity;
using StarFallMC.Entity.Enum;
using StarFallMC.Entity.Resource;
using StarFallMC.ResourcePages.SubPage;
using StarFallMC.Util;
using StarFallMC.Util.Extension;

namespace StarFallMC.ResourcePages;

public partial class ModResources : Page {

    private ViewModel viewModel = new();

    private CancellationTokenSource cancellationTokenSource;
    public static Action<ResourceType> InitResourcePage;
    public ModResources() {
        InitializeComponent();
        DataContext = viewModel;
        cancellationTokenSource = new CancellationTokenSource();
        viewModel.PercentText = "正在加载Mod列表... 0%";
        InitResourcePage = initResourcePage;
    }
    
    
    private async Task InitResource(CancellationToken ct) {
        try {
            ct.ThrowIfCancellationRequested();
            VirtualizingStackPanel.SetIsVirtualizing(ListView, true);
            VirtualizingStackPanel.SetVirtualizationMode(ListView, VirtualizationMode.Recycling);
            ResourcePageExtension.ReloadList(ResourceContent, LoadingBorder, NotExist);
            viewModel.Mods = new ObservableCollection<MinecraftResource>();
            viewModel.UseCurseForge = false;
            ct.ThrowIfCancellationRequested();
            if (ResourceUtil.ModResourceCache != null &&
                ResourceUtil.ModResourceCache.ContainsKey(viewModel.ResourceType)) {
                ct.ThrowIfCancellationRequested();
                var cache = ResourceUtil.ModResourceCache[viewModel.ResourceType];
                viewModel.UseCurseForge = cache.UseCurseForge;
                Pagination.CurrentPage = cache.CurrentPage;
                Pagination.TotalCount = cache.TotalCount;
                viewModel.SearchText = cache.SearchText;
                viewModel.SelectedLoader = cache.SelectedLoader;
                viewModel.SelectedVersion = cache.SelectedVersion;
                viewModel.SelectedCategory = cache.SelectedCategory;
                viewModel.Mods = new ObservableCollection<MinecraftResource>(cache.List);
                Console.WriteLine(cache.TotalCount);
            }
            else {
                ct.ThrowIfCancellationRequested();
                Dispatcher.BeginInvoke(() => { Pagination.CurrentPage = 1; });
                await GetModResource(ct).ConfigureAwait(false);
            }
            ct.ThrowIfCancellationRequested();
            ResourcePageExtension.AlreadyLoaded(this, ResourceContent, LoadingBorder, NotExist, viewModel.Mods.Count == 0);
        }
        catch (OperationCanceledException) {
            Console.WriteLine("取消加载列表");
        }
        catch (Exception e){
            Console.WriteLine(e);
        }
    }

    private async Task GetModResource(CancellationToken ct) {
        try {
            ct.ThrowIfCancellationRequested();
            //获取网络mod资源，分为Modrinth和curseforge
            var tmp = new List<MinecraftResource>();
            int totalCount = 0;
            ct.ThrowIfCancellationRequested();
            if (viewModel.UseCurseForge) {
                ct.ThrowIfCancellationRequested();
                (tmp , totalCount) = await ResourceUtil.GetCurseForgeModResources(
                    token: ct,
                    page: Pagination.CurrentPage,
                    query: viewModel.SearchText,
                    loader: viewModel.SelectedLoader,
                    version: viewModel.SelectedVersion,
                    category: ResourceCategory.CurseForgeCategoriesToInt(viewModel.SelectedCategory, viewModel.ResourceType),
                    resourceType: viewModel.ResourceType
                );
            }
            else {
                (tmp , totalCount) = await ResourceUtil.GetModrinthModResources(
                    ct: ct,
                    page: Pagination.CurrentPage,
                    query: viewModel.SearchText,
                    loader: viewModel.SelectedLoader,
                    version: viewModel.SelectedVersion,
                    category: ResourceCategory.ModrinthCategoryToString(viewModel.SelectedCategory),
                    resourceType: viewModel.ResourceType
                );
            }
            ct.ThrowIfCancellationRequested();
            if (ResourceUtil.ModResourceCache == null) {
                ResourceUtil.ModResourceCache = new Dictionary<ResourceType, ModResourceCache>();
            }
            if (!ResourceUtil.ModResourceCache.ContainsKey(viewModel.ResourceType)) {
                ct.ThrowIfCancellationRequested();
                ResourceUtil.ModResourceCache[viewModel.ResourceType] = new ModResourceCache {
                    UseCurseForge = false,
                    List = tmp,
                    TotalCount = totalCount,
                    CurrentPage = 1
                };
                Console.WriteLine($"存入cache 共{totalCount}条");
            }
            ct.ThrowIfCancellationRequested();
            viewModel.Mods = new ObservableCollection<MinecraftResource>(tmp);
            Pagination.TotalCount = totalCount;
            Console.WriteLine(totalCount);
            ct.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) {
            Console.WriteLine("取消加载Mod列表");
        }
        catch (Exception e){
            Console.WriteLine(e);
        }
    }
    
    public class ViewModel : INotifyPropertyChanged {

        private ObservableCollection<MinecraftResource> _mods;

        public ObservableCollection<MinecraftResource> Mods {
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

        public List<string> _categories;
        public List<string> Categories {
            get => _categories;
            set => SetField(ref _categories, value);
        }

        public ResourceType _resourceType = ResourceType.Mod;
        public ResourceType ResourceType {
            get => _resourceType;
            set {
                SetField(ref _resourceType, value);
                if (value == ResourceType.ModPack) {
                    Categories = UseCurseForge
                        ? ResourceCategory.ModPackCategoriesInCurseForge
                        : ResourceCategory.ModPackCategoriesInModrinth;
                }
                else if (value == ResourceType.DataPack) {
                    Categories = UseCurseForge
                        ? ResourceCategory.DataPackCategoriesInCurseForge
                        : ResourceCategory.DataPackCategoriesInModrinth;
                }
                else if (value == ResourceType.TexturePack) {
                    Categories = UseCurseForge
                        ? ResourceCategory.TexturePackCategoriesInCurseForge
                        : ResourceCategory.TexturePackCategoriesInModrinth;
                }
                else if (value == ResourceType.ShaderPack) {
                    Categories = UseCurseForge
                        ? ResourceCategory.ShaderPackCategoriesInCurseForge
                        : ResourceCategory.ShaderPackCategoriesInModrinth;
                }
                else {
                    Categories = ResourceCategory.ModCategories;
                }
            }
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
    

    private void initResourcePage(ResourceType resourceType) {
        // ModResources_OnUnloaded(null, null);
        cancellationTokenSource.Cancel();
        cancellationTokenSource = new CancellationTokenSource();
        string resourceTypeString;
        switch (resourceType) {
            case ResourceType.ModPack:
                resourceTypeString = "整合包";
                break;
            case ResourceType.DataPack:
                resourceTypeString = "数据包";
                break;
            case ResourceType.TexturePack:
                resourceTypeString = "材质包";
                break;
            case ResourceType.ShaderPack:
                resourceTypeString = "光影";
                break;
            default:
                resourceTypeString = "模组";
                break;
        }
        viewModel.PercentText = $"正在加载{resourceTypeString}列表...";
        viewModel.ResourceType = resourceType;
        InitResource(cancellationTokenSource.Token).ConfigureAwait(false);
    }
    
    private void Pagination_OnPageChanged(object sender, SelectionChangedEventArgs e) {
        ChangePage().ConfigureAwait(false);
    }

    private async Task ChangePage() {
        Dispatcher.BeginInvoke(async () => {
            viewModel.Mods = new ObservableCollection<MinecraftResource>();
            ScrollViewerExtensions.AnimateScroll(MainScrollViewer, 0);
            ResourcePageExtension.ReloadList(ResourceContent, LoadingBorder, NotExist);
            await GetModResource(cancellationTokenSource.Token);
            ResourcePageExtension.AlreadyLoaded(this, ResourceContent, LoadingBorder, NotExist,
                viewModel.Mods.Count == 0);
        });
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
        viewModel.Mods = new ObservableCollection<MinecraftResource>();
        if (string.IsNullOrEmpty(viewModel.SelectedVersion)) {
            viewModel.SelectedVersion = "全部";
        }
        ScrollViewerExtensions.AnimateScroll(MainScrollViewer,0);
        ResourcePageExtension.ReloadList(ResourceContent,LoadingBorder,NotExist);
        await GetModResource(cancellationTokenSource.Token);
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
        
        var resource = listView.SelectedItem as MinecraftResource;
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