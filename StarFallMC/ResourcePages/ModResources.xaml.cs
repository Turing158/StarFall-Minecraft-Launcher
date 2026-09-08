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
using StarFallMC.Navigation;
using StarFallMC.Util.Extension;
using StarFallMC.Services;
using StarFallMC.Services.Resources;

namespace StarFallMC.ResourcePages;

public partial class ModResources : Page, IPageLifecycle {
    private readonly LauncherUiCoordinator uiCoordinator;

    private ViewModel viewModel = new();

    private CancellationTokenSource? pageCancellationTokenSource;
    private CancellationTokenSource? activeLoadCancellationTokenSource;
    private CancellationTokenSource? thumbnailCancellationTokenSource = new();
    private readonly SemaphoreSlim loadGate = new(1, 1);
    private readonly object thumbnailTaskLock = new();
    private readonly HashSet<Task> thumbnailTasks = new();
    private Task activeLoadTask = Task.CompletedTask;
    private readonly ResourceType initialResourceType;
    private bool isActive;
    private long loadGeneration;
    public ModResources(ResourceType resourceType = ResourceType.Mod, LauncherUiCoordinator? uiCoordinator = null) {
        this.uiCoordinator = uiCoordinator ?? new LauncherUiCoordinator();
        InitializeComponent();
        DataContext = viewModel;
        viewModel.PercentText = "正在加载Mod列表... 0%";
        initialResourceType = resourceType;
        ConfigureResourceType(resourceType);
    }
    
    
    private async Task LoadAsync(PageResourceQuery query, long generation, CancellationToken ct) {
        try {
            ct.ThrowIfCancellationRequested();
            ResourceServiceContainer services = ResourceServices.Current;
            string gamePath = ApplicationState.GameSelection.CurrentGame?.Path ?? string.Empty;
            string gameVersion = ApplicationState.GameSelection.CurrentGame?.Name ?? string.Empty;
            services.Cache.ClearForVersion(gamePath);
            var displayQuery = new StarFallMC.Services.Resources.ResourceQuery(
                query.ResourceType,
                query.UseCurseForge,
                query.Page,
                query.SearchText,
                query.SelectedLoader,
                query.SelectedVersion,
                query.SelectedCategory,
                gameVersion,
                gamePath);

            if (query.IsInitialLoad &&
                services.Cache.TryGetLastQuery(query.ResourceType, out var savedQuery, out var savedKey) &&
                string.Equals(savedKey.DirectoryVersion, gamePath, StringComparison.Ordinal) &&
                string.Equals(savedKey.GameVersion, gameVersion, StringComparison.Ordinal) &&
                services.Cache.TryGet(savedKey, out var savedPage)) {
                ApplyCachedResult(savedQuery, savedPage);
                return;
            }

            string apiCategory = query.UseCurseForge
                ? query.SelectedCategory
                : ResourceCategory.ModrinthCategoryToString(query.SelectedCategory);
            var apiQuery = displayQuery with { SelectedCategory = apiCategory };
            ResourceCacheKey cacheKey = ResourceCacheKey.FromQuery(apiQuery);
            ResourcePageDto<CommunityResourceDto> page = await services.Cache.GetOrCreateAsync(
                cacheKey,
                async requestToken => {
                    ApiResult<ResourcePageDto<CommunityResourceDto>> response = query.UseCurseForge
                        ? await services.CurseForge.SearchAsync(
                            apiQuery,
                            ResourceCategory.CurseForgeCategoriesToInt(query.SelectedCategory, query.ResourceType),
                            cancellationToken: requestToken)
                        : await services.Modrinth.SearchAsync(apiQuery, cancellationToken: requestToken);
                    if (response.ErrorKind == ResourceErrorKind.Cancelled) {
                        throw new OperationCanceledException(requestToken);
                    }
                    if (!response.Success || response.Value == null) {
                        throw new ResourceApiException(
                            response.Error ?? "Resource API request failed.",
                            response.ErrorKind,
                            response.StatusCode);
                    }
                    return response.Value;
                },
                ct);
            var tmp = page.Items.Select(item => services.Mapper.Map(item, services.Localizations)).ToList();
            int totalCount = page.TotalCount;
            ct.ThrowIfCancellationRequested();
            await Dispatcher.InvokeAsync(() => {
                ct.ThrowIfCancellationRequested();
                if (!IsCurrentGeneration(generation)) {
                    return;
                }

                viewModel.Mods = new ObservableCollection<MinecraftResource>(tmp);
                Pagination.CurrentPage = query.Page;
                Pagination.TotalCount = totalCount;
                services.Cache.RememberQuery(displayQuery, cacheKey);
                ResourcePageExtension.AlreadyLoaded(this, ResourceContent, LoadingBorder, NotExist, tmp.Count == 0);
            });
        }
        catch (OperationCanceledException) {
            Console.WriteLine("GetModFileInfo取消");
        }
        catch (ResourceApiException exception) {
            Console.WriteLine(exception.Message);
            await Dispatcher.InvokeAsync(() => {
                if (!IsCurrentGeneration(generation)) return;
                viewModel.Mods = new ObservableCollection<MinecraftResource>();
                Pagination.CurrentPage = query.Page;
                Pagination.TotalCount = 0;
                ResourcePageExtension.AlreadyLoaded(this, ResourceContent, LoadingBorder, NotExist, true);
            });
        }
        catch (Exception e){
            Console.WriteLine(e);
        }
    }
    
    public class ViewModel : INotifyPropertyChanged {

        private ObservableCollection<MinecraftResource> _mods = new();

        public ObservableCollection<MinecraftResource> Mods {
            get => _mods;
            set => SetField(ref _mods, value);
        }
        
        private string _percentText = string.Empty;
        
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

        public List<string> _categories = ResourceCategory.ModCategories;
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
    

    private void ConfigureResourceType(ResourceType resourceType) {
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
    }

    private void ApplyCachedResult(
        StarFallMC.Services.Resources.ResourceQuery query,
        ResourcePageDto<CommunityResourceDto> page) {
        ResourceServiceContainer services = ResourceServices.Current;
        viewModel.UseCurseForge = query.UseCurseForge;
        Pagination.CurrentPage = query.Page;
        Pagination.TotalCount = page.TotalCount;
        viewModel.SearchText = query.SearchText;
        viewModel.SelectedLoader = query.SelectedLoader;
        viewModel.SelectedVersion = query.SelectedVersion;
        viewModel.SelectedCategory = query.SelectedCategory;
        viewModel.Mods = new ObservableCollection<MinecraftResource>(
            page.Items.Select(item => services.Mapper.Map(item, services.Localizations)));
        ResourcePageExtension.AlreadyLoaded(this, ResourceContent, LoadingBorder, NotExist, viewModel.Mods.Count == 0);
    }

    private bool IsCurrentGeneration(long generation) => isActive && generation == Volatile.Read(ref loadGeneration);

    private PageResourceQuery CaptureQuery(bool isInitialLoad = false) => new(
        viewModel.ResourceType,
        viewModel.UseCurseForge,
        Math.Max(1, Pagination.CurrentPage),
        viewModel.SearchText ?? string.Empty,
        viewModel.SelectedLoader ?? "全部",
        viewModel.SelectedVersion ?? "全部",
        viewModel.SelectedCategory ?? "全部",
        isInitialLoad);

    private async Task StartLatestQueryAsync(PageResourceQuery query) {
        long generation = Interlocked.Increment(ref loadGeneration);
        CancellationTokenSource? loadCts = null;
        CancellationToken loadToken = default;
        Task loadTask = Task.CompletedTask;
        await loadGate.WaitAsync();
        try {
            var previousCts = activeLoadCancellationTokenSource;
            var previousTask = activeLoadTask;
            activeLoadCancellationTokenSource = null;
            activeLoadTask = Task.CompletedTask;
            previousCts?.Cancel();
            await AwaitPreviousLoadAsync(previousTask);
            previousCts?.Dispose();

            if (!IsCurrentGeneration(generation) || pageCancellationTokenSource == null) {
                return;
            }

            loadCts = CancellationTokenSource.CreateLinkedTokenSource(pageCancellationTokenSource.Token);
            loadToken = loadCts.Token;
            loadTask = LoadAsync(query, generation, loadToken);
            activeLoadCancellationTokenSource = loadCts;
            activeLoadTask = loadTask;
        }
        finally {
            loadGate.Release();
        }

        try {
            await loadTask;
        }
        catch (OperationCanceledException) when (loadToken.IsCancellationRequested || !isActive) {
        }
    }

    private static async Task AwaitPreviousLoadAsync(Task previousTask) {
        try {
            await previousTask;
        }
        catch (OperationCanceledException) {
        }
        catch (Exception exception) {
            Console.WriteLine(exception);
        }
    }

    private sealed record PageResourceQuery(
        ResourceType ResourceType,
        bool UseCurseForge,
        int Page,
        string SearchText,
        string SelectedLoader,
        string SelectedVersion,
        string SelectedCategory,
        bool IsInitialLoad);

    private async void LogoImage_OnLoaded(object sender, RoutedEventArgs e) {
        await EnsureLogoLoadedAsync((sender as FrameworkElement)?.DataContext as MinecraftResource);
    }

    private async void LogoImage_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
        await EnsureLogoLoadedAsync(e.NewValue as MinecraftResource);
    }

    private async Task EnsureLogoLoadedAsync(MinecraftResource? resource) {
        if (!isActive || resource == null) {
            return;
        }

        var thumbnailCts = thumbnailCancellationTokenSource;
        if (thumbnailCts == null) {
            return;
        }

        var loadTask = resource.EnsureLogoLoadedAsync(thumbnailCts.Token);
        lock (thumbnailTaskLock) {
            thumbnailTasks.Add(loadTask);
        }
        try {
            await loadTask;
        }
        catch (OperationCanceledException) {
        }
        catch (Exception exception) {
            Console.WriteLine(exception);
        }
        finally {
            lock (thumbnailTaskLock) {
                thumbnailTasks.Remove(loadTask);
            }
        }
    }
    
    private async void Pagination_OnPageChanged(object sender, SelectionChangedEventArgs e) {
        await ChangePage();
    }

    private async Task ChangePage() {
        await ResetThumbnailRequestsAsync();
        viewModel.Mods = new ObservableCollection<MinecraftResource>();
        ScrollListToTop();
        ResourcePageExtension.ReloadList(ResourceContent, LoadingBorder, NotExist);
        await StartLatestQueryAsync(CaptureQuery());
    }

    private async void Platform_OnClick(object sender, RoutedEventArgs e) {
        Pagination.CurrentPage = 1;
        await ChangePage();
    }

    private void ComboBox_OnDropDownOpened(object? sender, EventArgs e) {
        SetListScrollEnabled(false);
    }
    
    private void ComboBox_OnDropDownClosed(object? sender, EventArgs e) {
        SetListScrollEnabled(true);
    }

    private async void Search_OnClick(object sender, RoutedEventArgs e) {
        await SearchResource();
    }

    private async Task SearchResource() {
        await ResetThumbnailRequestsAsync();
        Pagination.CurrentPage = 1;
        viewModel.Mods = new ObservableCollection<MinecraftResource>();
        if (string.IsNullOrEmpty(viewModel.SelectedVersion)) {
            viewModel.SelectedVersion = "全部";
        }
        ScrollListToTop();
        ResourcePageExtension.ReloadList(ResourceContent,LoadingBorder,NotExist);
        await StartLatestQueryAsync(CaptureQuery());
    }

    private void Reset_OnClick(object sender, RoutedEventArgs e) {
        viewModel.SearchText = string.Empty;
        viewModel.SelectedCategory = "全部";
        viewModel.SelectedLoader = "全部";
        viewModel.SelectedVersion = "全部";
    }

    private async void ListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        var listView = sender as ListView;
        if (listView == null) {
            return;
        }
        if (listView.SelectedIndex < 0) {
            return;
        }
        
        var resource = listView.SelectedItem as MinecraftResource;
        listView.SelectedIndex = -1;
        if (resource == null) {
            return;
        }
        
        await uiCoordinator.ShowModInfoAsync(resource);
    }

    private void ScrollListToTop() {
        var scrollViewer = ScrollViewerExtensions.FindScrollViewer(ListView);
        if (scrollViewer != null) {
            ScrollViewerExtensions.AnimateScroll(scrollViewer, 0);
        }
    }

    private void SetListScrollEnabled(bool enabled) {
        var scrollViewer = ScrollViewerExtensions.FindScrollViewer(ListView);
        if (scrollViewer != null) {
            ScrollViewerExtensions.ScrollEnabled(scrollViewer, enabled);
        }
    }

    public Task ActivateAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        isActive = true;
        pageCancellationTokenSource?.Dispose();
        pageCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (thumbnailCancellationTokenSource == null || thumbnailCancellationTokenSource.IsCancellationRequested) {
            thumbnailCancellationTokenSource?.Dispose();
            thumbnailCancellationTokenSource = new CancellationTokenSource();
        }
        ConfigureResourceType(initialResourceType);
        VirtualizingStackPanel.SetIsVirtualizing(ListView, true);
        VirtualizingStackPanel.SetVirtualizationMode(ListView, VirtualizationMode.Recycling);
        ResourcePageExtension.ReloadList(ResourceContent, LoadingBorder, NotExist);
        viewModel.Mods = new ObservableCollection<MinecraftResource>();
        return StartLatestQueryAsync(CaptureQuery(isInitialLoad: true));
    }

    public async Task DeactivateAsync() {
        isActive = false;
        Interlocked.Increment(ref loadGeneration);
        pageCancellationTokenSource?.Cancel();
        await ResetThumbnailRequestsAsync(renew: false);
        await loadGate.WaitAsync();
        try {
            var loadCts = activeLoadCancellationTokenSource;
            var loadTask = activeLoadTask;
            activeLoadCancellationTokenSource = null;
            activeLoadTask = Task.CompletedTask;
            loadCts?.Cancel();
            await AwaitPreviousLoadAsync(loadTask);
            loadCts?.Dispose();
        }
        finally {
            loadGate.Release();
        }
        pageCancellationTokenSource?.Dispose();
        pageCancellationTokenSource = null;
    }

    private async Task ResetThumbnailRequestsAsync(bool renew = true) {
        var thumbnailCts = thumbnailCancellationTokenSource;
        thumbnailCancellationTokenSource = null;
        thumbnailCts?.Cancel();
        while (true) {
            Task[] pending;
            lock (thumbnailTaskLock) {
                pending = thumbnailTasks.ToArray();
            }
            if (pending.Length == 0) {
                break;
            }
            await AwaitPreviousLoadAsync(Task.WhenAll(pending));
            lock (thumbnailTaskLock) {
                foreach (var task in pending) {
                    thumbnailTasks.Remove(task);
                }
            }
        }
        thumbnailCts?.Dispose();
        if (renew && isActive) {
            thumbnailCancellationTokenSource = new CancellationTokenSource();
        }
    }
    
}
