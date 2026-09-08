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
using StarFallMC.Util;
using StarFallMC.Navigation;
using StarFallMC.Services;
using StarFallMC.Services.Resources;
using MessageBox = StarFallMC.Component.MessageBox;
using MessageBoxResult = StarFallMC.Entity.Enum.MessageBoxResult;

namespace StarFallMC.ResourcePages;

public partial class TexturePacksPage : Page, IPageLifecycle {

    private ViewModel viewModel = new();
    private CancellationTokenSource cancellationTokenSource;
    private Task activeLoadTask = Task.CompletedTask;
    private bool isActive;
    private long loadGeneration;
    private readonly List<TexturePackResource> resourceSnapshot = new();
    public TexturePacksPage() {
        InitializeComponent();
        DataContext = viewModel;
        cancellationTokenSource = new();
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
            IReadOnlyList<TexturePackResource> loadedResources = [];
            IReadOnlyList<ResourceError> errors = [];
            if (game != null && !string.IsNullOrWhiteSpace(game.Path)) {
                ResourceScanResult<TexturePackResource> result = await ResourceServices.Current.LocalCatalog.ScanTexturePacksAsync(
                    game.Path,
                    ApplicationState.GameSettings.IsIsolation,
                    progress,
                    cancellationToken);
                loadedResources = result.Items;
                errors = result.Errors;
            }
            else {
                progress.Report(100);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentGeneration(generation)) return;
            resourceSnapshot.Clear();
            resourceSnapshot.AddRange(loadedResources);
            if (errors.Count != 0) Console.WriteLine($"Skipped {errors.Count} texture packs.");
            viewModel.TexturePacks = new ObservableCollection<TexturePackResource>(resourceSnapshot);
            ResourcePageExtension.AlreadyLoaded(this,MainScrollViewer,LoadingBorder,NotExist,resourceSnapshot.Count == 0);
            viewModel.PercentText = "加载完成";
            MessageTips.Show($"获取到{viewModel.TexturePacks.Count}个材质包文件");
        }
        catch (OperationCanceledException) {
            Console.WriteLine("LoadTexturePacks取消");
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
        
        private ObservableCollection<TexturePackResource> _texturePacks = new();

        public ObservableCollection<TexturePackResource> TexturePacks{
            get => _texturePacks;
            set => SetField(ref _texturePacks, value);
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

    private void PackPosition_OnClick(object sender, RoutedEventArgs e) {
        var item = sender as TextButton;
        if (item == null) return;
        var path = item.Tag as string;
        if (string.IsNullOrEmpty(path)) {
            MessageTips.Show("路径无效");
            return;
        }
        DirFileUtil.OpenContainingFolder(path);
    }

    private async void IconImage_OnLoaded(object sender, RoutedEventArgs e) {
        await EnsureIconLoadedAsync((sender as FrameworkElement)?.DataContext as TexturePackResource);
    }

    private async void IconImage_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
        await EnsureIconLoadedAsync(e.NewValue as TexturePackResource);
    }

    private async Task EnsureIconLoadedAsync(TexturePackResource? resource) {
        if (!isActive || resource == null) {
            return;
        }

        try {
            await resource.EnsureIconLoadedAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) {
        }
    }

    private void PackDelete_OnClick(object sender, RoutedEventArgs e) {
        if (sender is not TextButton button || button.Tag is not TexturePackResource item) {
            return;
        }
        MessageBox.Show($"确定删除材质包 {item.Name} 吗？", "确认删除", MessageBoxBtnType.ConfirmAndCancel, (result) => {
            if (result == MessageBoxResult.Confirm) {
                resourceSnapshot.Remove(item);
                viewModel.TexturePacks.Remove(item);
                File.Delete(item.Path);
                MessageTips.Show($"删除材质包 {item.Name} 成功");
            }
        });
    }
    
    private async void RefreshBtn_OnClick(object sender, RoutedEventArgs e) {
        Interlocked.Increment(ref loadGeneration);
        cancellationTokenSource.Cancel();
        await WaitForActiveLoadAsync();
        cancellationTokenSource.Dispose();
        cancellationTokenSource = new CancellationTokenSource();
        resourceSnapshot.Clear();
        viewModel.TexturePacks = new ObservableCollection<TexturePackResource>();
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
