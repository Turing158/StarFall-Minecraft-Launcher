using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.Entity.Resource;
using StarFallMC.ResourcePages.SubPage;
using StarFallMC.Util;
using StarFallMC.Navigation;
using StarFallMC.Services;
using StarFallMC.Services.Resources;
using Path = System.IO.Path;

namespace StarFallMC.ResourcePages;

public partial class ModsPage : Page, IPageLifecycle {
    private readonly LauncherUiCoordinator uiCoordinator;

    private ViewModel viewModel = new();

    private CancellationTokenSource cancellationTokenSource;
    private CancellationTokenSource thumbnailCancellationTokenSource = new();
    private Task activeLoadTask = Task.CompletedTask;
    private bool isActive;
    private long loadGeneration;
    private readonly List<MinecraftResource> resourceSnapshot = new();
    public ModsPage(LauncherUiCoordinator? uiCoordinator = null) {
        this.uiCoordinator = uiCoordinator ?? new LauncherUiCoordinator();
        InitializeComponent();
        DataContext = viewModel;
        cancellationTokenSource = new CancellationTokenSource();
        viewModel.PercentText = "正在加载Mod列表... 0%";
    }
    
    private Dictionary<string,int> _modIndexCache = new();
    
    private async Task InitResource(long generation, CancellationToken cancellationToken) {
        VirtualizingStackPanel.SetIsVirtualizing(ListView, true);
        VirtualizingStackPanel.SetVirtualizationMode(ListView, VirtualizationMode.Recycling);
        ResourcePageExtension.ReloadList(MainScrollViewer,LoadingBorder,NotExist);
        IProgress<int> progress = new Progress<int>(percent => {
            if (IsCurrentGeneration(generation)) viewModel.PercentText = $"加载Mod列表... {percent}%";
        });
        try {
            cancellationToken.ThrowIfCancellationRequested();
            MinecraftItem game = ApplicationState.GameSelection.CurrentGame;
            IReadOnlyList<MinecraftResource> loadedResources = [];
            IReadOnlyList<ResourceError> errors = [];
            if (game == null || string.IsNullOrWhiteSpace(game.Path)) {
                progress.Report(100);
            }
            else {
                ResourceScanResult<MinecraftResource> result = await ResourceServices.Current.LocalCatalog.ScanModsAsync(
                    game.Path,
                    ApplicationState.GameSettings.IsIsolation,
                    progress,
                    cancellationToken);
                loadedResources = result.Items;
                errors = result.Errors;
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentGeneration(generation)) return;
            resourceSnapshot.Clear();
            resourceSnapshot.AddRange(loadedResources);
            viewModel.TotalMods = resourceSnapshot.ToList();
            _modIndexCache.Clear();
            for (int i = 0; i < viewModel.TotalMods.Count; i++) {
                _modIndexCache[viewModel.TotalMods[i].ModrinthSha1] = i;
            }
            if (errors.Count != 0) Console.WriteLine($"Skipped {errors.Count} local mod files.");
            ResourcePageExtension.AlreadyLoaded(this,MainScrollViewer,LoadingBorder,NotExist,viewModel.TotalMods.Count == 0);
            viewModel.PercentText = "加载完成";
            MessageTips.Show($"获取到{viewModel.TotalMods.Count}个Mods资源");
            Pagination.CurrentPage = 1;
            SetModsPages();
        }
        catch (OperationCanceledException) {
            Console.WriteLine("LoadMods取消");
            return;
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

        private List<MinecraftResource> _TotalMods = new();
        public List<MinecraftResource> TotalMods {
            get => _TotalMods;
            set => SetField(ref _TotalMods, value);
        }

        
        private string _percentText = string.Empty;
        
        public string PercentText {
            get => _percentText;
            set => SetField(ref _percentText, value);
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
    
    private async void ModInfo_OnClick(object sender, RoutedEventArgs e) {
        var item = (sender as TextButton)?.Tag as MinecraftResource;
        if (item != null) {
            await uiCoordinator.ShowModInfoAsync(item);
        }
    }

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

        try {
            await resource.EnsureLogoLoadedAsync(thumbnailCancellationTokenSource.Token);
        }
        catch (OperationCanceledException) {
        }
    }

    private void ModPosition_OnClick(object sender, RoutedEventArgs e) {
        var item = (sender as TextButton)?.Tag as MinecraftResource;
        if (item == null) {
            return;
        }
        DirFileUtil.OpenContainingFolder(item.FilePath);
        
    }

    private void ModDisable_OnClick(object sender, RoutedEventArgs e) {
        var textButton = sender as TextButton;
        var item = textButton?.Tag as MinecraftResource;
        if (item == null) {
            return;
        }
        
        if (Path.GetDirectoryName(item.FilePath) is not string dirPath) {
            MessageTips.Show($"模组禁用失败:{item.DisplayName}");
            return;
        }
        if (Path.GetFileName(dirPath) == ".disabled") {
            MessageTips.Show($"模组已禁用:{item.DisplayName}");
            return;
        }
        string disabledDirPath = Path.Combine(dirPath, ".disabled");
        if (!Directory.Exists(disabledDirPath)) {
            Directory.CreateDirectory(disabledDirPath);
        }

        string disabledFilePath = Path.Combine(disabledDirPath, item.FileName);
        try {
            File.Move(item.FilePath, disabledFilePath, true);
            item.FilePath = disabledFilePath;
            item.Disabled = true;

            MessageTips.Show($"模组已禁用:{item.DisplayName}");
            int index = -1;
            if (_modIndexCache.TryGetValue(item.ModrinthSha1, out index)) {
                if (index >= 0) {
                    resourceSnapshot[index] = item;
                    viewModel.TotalMods[index] = item;
                }
            }
        }
        catch (Exception exception){
            MessageTips.Show($"模组禁用失败:{item.DisplayName}");
            Console.WriteLine(exception);
        }
        
    }

    private void ModRollBack_OnClick(object sender, RoutedEventArgs e) {
        var textButton = sender as TextButton;
        var item = textButton?.Tag as MinecraftResource;
        if (item == null) {
            return;
        }
        if (Path.GetDirectoryName(item.FilePath) is not string dirPath) {
            MessageTips.Show($"模组启用失败:{item.DisplayName}");
            return;
        }
        if (Path.GetFileName(dirPath) != ".disabled") {
            MessageTips.Show($"模组已启用:{item.DisplayName}");
            return;
        }

        if (Path.GetDirectoryName(dirPath) is not string enabledDirectoryPath) {
            MessageTips.Show($"模组启用失败:{item.DisplayName}");
            return;
        }
        string enabledFilePath = Path.Combine(enabledDirectoryPath, item.FileName);

        if (!File.Exists(item.FilePath)) {
            MessageTips.Show($"模组已启用:{item.DisplayName}");
            return;
        }
        
        try {
            File.Move(item.FilePath, enabledFilePath, true);
            item.FilePath = enabledFilePath;
            item.Disabled = false;
            MessageTips.Show($"模组已启用:{item.DisplayName}");
            int index = resourceSnapshot.FindIndex(i => item.DisplayName == i.DisplayName);
            if (index >= 0) {
                resourceSnapshot[index] = item;
                viewModel.TotalMods[index] = item;
            }
        }
        catch (Exception exception){
            MessageTips.Show($"模组启用失败:{item.DisplayName}");
            Console.WriteLine(exception);
        }
    }

    private async void RefreshBtn_OnClick(object sender, RoutedEventArgs e) {
        Interlocked.Increment(ref loadGeneration);
        cancellationTokenSource.Cancel();
        await WaitForActiveLoadAsync();
        cancellationTokenSource.Dispose();
        cancellationTokenSource = new CancellationTokenSource();
        resourceSnapshot.Clear();
        viewModel.TotalMods = new List<MinecraftResource>();
        activeLoadTask = StartLoad();
        await activeLoadTask;
    }
    
    private void SetModsPages() {
        ResetThumbnailRequests();
        var tmp = NetworkUtil.GetPageList(viewModel.TotalMods, Pagination.CurrentPage, 20);
        viewModel.Mods = new ObservableCollection<MinecraftResource>(tmp);
        Pagination.TotalCount = viewModel.TotalMods?.Count ?? 0;
    }

    private void Pagination_OnPageChanged(object sender, SelectionChangedEventArgs e) {
        SetModsPages();
    }

    public Task ActivateAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        isActive = true;
        if (cancellationTokenSource.IsCancellationRequested) {
            cancellationTokenSource.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
        }
        activeLoadTask = StartLoad();
        return activeLoadTask;
    }

    public async Task DeactivateAsync() {
        isActive = false;
        Interlocked.Increment(ref loadGeneration);
        cancellationTokenSource.Cancel();
        ResetThumbnailRequests();
        await WaitForActiveLoadAsync();
        cancellationTokenSource.Dispose();
        cancellationTokenSource = new CancellationTokenSource();
    }

    private async Task WaitForActiveLoadAsync() {
        try {
            await activeLoadTask;
        }
        catch (OperationCanceledException) {
        }
    }

    private Task StartLoad() {
        long generation = Interlocked.Increment(ref loadGeneration);
        return InitResource(generation, cancellationTokenSource.Token);
    }

    private bool IsCurrentGeneration(long generation) =>
        isActive && generation == Volatile.Read(ref loadGeneration);

    private void ResetThumbnailRequests() {
        thumbnailCancellationTokenSource.Cancel();
        thumbnailCancellationTokenSource.Dispose();
        thumbnailCancellationTokenSource = new CancellationTokenSource();
    }
}
