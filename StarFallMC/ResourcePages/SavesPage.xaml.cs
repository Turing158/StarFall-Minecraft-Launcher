using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.Entity.Enum;
using StarFallMC.Entity.Resource;
using StarFallMC.ResourcePages.SubPage;
using StarFallMC.Util;
using StarFallMC.Navigation;
using StarFallMC.Services;
using StarFallMC.Services.Resources;
using MessageBox = StarFallMC.Component.MessageBox;
using MessageBoxResult = StarFallMC.Entity.Enum.MessageBoxResult;

namespace StarFallMC.ResourcePages;



public partial class SavesPage : Page, IPageLifecycle {
    private readonly LauncherUiCoordinator uiCoordinator;
    private ViewModel viewModel = new();
    
    private CancellationTokenSource cancellationTokenSource;
    private Task activeLoadTask = Task.CompletedTask;
    private bool isActive;
    private long loadGeneration;
    private readonly List<SavesResource> resourceSnapshot = new();
    public SavesPage(LauncherUiCoordinator? uiCoordinator = null) {
        this.uiCoordinator = uiCoordinator ?? new LauncherUiCoordinator();
        InitializeComponent();
        DataContext = viewModel;
        cancellationTokenSource = new CancellationTokenSource();
        viewModel.PercentText = "0%";
    }
    
    private async Task InitResource(long generation, CancellationToken cancellationToken) {
        VirtualizingStackPanel.SetIsVirtualizing(ListView, true);
        VirtualizingStackPanel.SetVirtualizationMode(ListView, VirtualizationMode.Recycling);
        ResourcePageExtension.ReloadList(MainScrollViewer,LoadingBorder,NotExist);
        IProgress<int> progress = new Progress<int>(percent => {
            if (IsCurrentGeneration(generation)) viewModel.PercentText = $"加载中... {percent}%";
        });
        try {
            MinecraftItem game = ApplicationState.GameSelection.CurrentGame;
            IReadOnlyList<SavesResource> loadedResources = [];
            IReadOnlyList<ResourceError> errors = [];
            if (game == null || string.IsNullOrWhiteSpace(game.Path)) {
                progress.Report(100);
            }
            else {
                ResourceScanResult<SavesResource> result = await ResourceServices.Current.LocalCatalog.ScanSavesAsync(
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
            if (errors.Count != 0) Console.WriteLine($"Skipped {errors.Count} local save files.");
            viewModel.Saves = new ObservableCollection<SavesResource>(resourceSnapshot);
            ResourcePageExtension.AlreadyLoaded(this,MainScrollViewer,LoadingBorder,NotExist,resourceSnapshot.Count == 0);
            viewModel.PercentText = "加载完成";
            MessageTips.Show($"获取到{viewModel.Saves.Count}个地图文件");
        }
        catch (OperationCanceledException) {
            Console.WriteLine("LoadSaves取消");
            return;
        }
        catch (Exception e){
            Console.WriteLine(e);
        }
    }
    
    public class ViewModel : INotifyPropertyChanged {
        
        private string _percentText = "0%";
        
        public string PercentText { 
            get => _percentText;
            set => SetField(ref _percentText, value);
        }
        
        private ObservableCollection<SavesResource> _saves = new();

        public ObservableCollection<SavesResource> Saves {
            get => _saves;
            set => SetField(ref _saves, value);
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
    

    private async void SaveInfo_OnClick(object sender, RoutedEventArgs e) {
        var item = (sender as TextButton)?.Tag as SavesResource;
        if (item != null) {
            await uiCoordinator.ShowSaveInfoAsync(item);
        }
    }

    private async void IconImage_OnLoaded(object sender, RoutedEventArgs e) {
        await EnsureIconLoadedAsync((sender as FrameworkElement)?.DataContext as SavesResource);
    }

    private async void IconImage_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
        await EnsureIconLoadedAsync(e.NewValue as SavesResource);
    }

    private async Task EnsureIconLoadedAsync(SavesResource? resource) {
        if (!isActive || resource == null) {
            return;
        }

        try {
            await resource.EnsureIconLoadedAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) {
        }
    }

    private void SavePosition_OnClick(object sender, RoutedEventArgs e) {
        var item = sender as TextButton;
        if (item?.Tag == null) {
            return;
        }

        var path = item.Tag as string;
        if (path != null) {
            DirFileUtil.OpenContainingFolder(path);
        }
    }

    private void SavesDelete_OnClick(object sender, RoutedEventArgs e) {
        var item = sender as TextButton;
        if (item?.Tag == null) {
            return;
        }
        var resource = item.Tag as SavesResource;
        if (resource == null) {
           return;
        }
        MessageBox.Show($"确定删除下面的地图文件吗？它会永久消失不见喔\n\n {resource.WorldName} ({resource.DirName}) ","删除提示",MessageBoxBtnType.ConfirmAndCancel,
            r => {
                if (r == MessageBoxResult.Confirm) {
                    viewModel.Saves.Remove(resource);
                    resourceSnapshot.Remove(resource);
                    Directory.Delete(resource.Path,true);
                    MessageTips.Show($"已删除地图文件 {resource.WorldName} ({resource.DirName})");
                }
            });
    }

    private void SaveCopy_OnClick(object sender, RoutedEventArgs e) {
        var item = sender as TextButton;
        if (item?.Tag == null) {
            return;
        }
        var resource = item.Tag as SavesResource;
        if (resource == null) {
            return;
        }
        
        MessageTips.Show($"正在备份地图文件 {resource.WorldName} ({resource.DirName})");
        var copyResource = new SavesResource(resource.nbt,resource.DirName,resource.Path,resource.RefreshDate);
        if (Path.GetDirectoryName(resource.Path) is not string copyDirPath) {
            MessageTips.Show("地图路径无效");
            return;
        }
        var copyName = $"{resource.DirName}_备份";
        var copyPath = Path.Combine(copyDirPath,copyName);
        while (Directory.Exists(copyPath)) {
            copyName += "_备份";
            copyPath = Path.Combine(copyDirPath,copyName);
        }
        copyResource.Path = copyPath;
        copyResource.DirName = copyName;
        DirFileUtil.CopyDirAndFiles(resource.Path,copyPath);
        var copyIconPath = Path.Combine(copyPath, "icon.png");
        copyResource.IconPath = File.Exists(copyIconPath) ? copyIconPath : string.Empty;
        int index = resourceSnapshot.FindIndex(r => r.Path == resource.Path);
        if (index >= 0) {
            resourceSnapshot.Add(copyResource);
            viewModel.Saves.Add(copyResource);
        }
    }

    private async void RefreshBtn_OnClick(object sender, RoutedEventArgs e) {
        Interlocked.Increment(ref loadGeneration);
        cancellationTokenSource.Cancel();
        await WaitForActiveLoadAsync();
        cancellationTokenSource.Dispose();
        cancellationTokenSource = new CancellationTokenSource();
        resourceSnapshot.Clear();
        viewModel.Saves = new ObservableCollection<SavesResource>();
        activeLoadTask = StartLoad();
        await activeLoadTask;
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
}
