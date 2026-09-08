using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.ResourcePages.SubPage;
using StarFallMC.Util;
using StarFallMC.Navigation;
using StarFallMC.Services.Download;
using StarFallMC.Services.Resources;

namespace StarFallMC.ResourcePages;

public partial class DownloadGame : Page, IPageLifecycle {

    private ViewModel viewModel = new();
    private CancellationTokenSource cts;
    private Task activeLoadTask = Task.CompletedTask;
    private readonly LauncherUiCoordinator uiCoordinator;
    private readonly DownloadCoordinator downloadCoordinator;
    private readonly ResourceWorkflowService resourceWorkflow;
    public DownloadGame(
        LauncherUiCoordinator? uiCoordinator = null,
        DownloadCoordinator? downloadCoordinator = null,
        ResourceWorkflowService? resourceWorkflow = null) {
        this.uiCoordinator = uiCoordinator ?? new LauncherUiCoordinator();
        this.downloadCoordinator = downloadCoordinator ?? new DownloadCoordinator(this.uiCoordinator);
        this.resourceWorkflow = resourceWorkflow ?? new ResourceWorkflowService(this.uiCoordinator);
        InitializeComponent();
        DataContext = viewModel;
        cts = new();
    }
    
    public class ViewModel : INotifyPropertyChanged {
        

        private List<MinecraftDownloader> latestType = new();
        public List<MinecraftDownloader> LatestType {
            get => latestType;
            set => SetField(ref latestType, value);
        }
        
        private List<MinecraftDownloader> _releaseType = new();
        public List<MinecraftDownloader> ReleaseType {
            get => _releaseType;
            set => SetField(ref _releaseType, value);
        }
        
        private List<MinecraftDownloader> _snapshotType = new();
        public List<MinecraftDownloader> SnapshotType {
            get => _snapshotType;
            set => SetField(ref _snapshotType, value);
        }
        
        private List<MinecraftDownloader> _aprilFoolsType = new();
        public List<MinecraftDownloader> AprilFoolsType {
            get => _aprilFoolsType;
            set => SetField(ref _aprilFoolsType, value);
        }
        
        private List<MinecraftDownloader> _oldType = new();

        public List<MinecraftDownloader> OldType {
            get => _oldType;
            set => SetField(ref _oldType, value);
        }
        
        private string _percentText = string.Empty;
        public string PercentText {
            get => _percentText;
            set => SetField(ref _percentText, value);
        }
        
        public void ClearAllCollection() {
            ClearCollection(ref latestType);
            ClearCollection(ref _releaseType);
            ClearCollection(ref _snapshotType);
            ClearCollection(ref _aprilFoolsType);
            ClearCollection(ref _oldType);
        }
        
        private void ClearCollection<T>(ref List<T> collection) {
            if (collection != null) {
                collection.Clear();
                collection.TrimExcess();
                collection = new List<T>();
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
    
    private async Task InitMinecraftDownloader() {
        ResourcePageExtension.ReloadList(MainScrollViewer,LoadingBorder);
        Console.WriteLine("开始加载Minecraft列表");
        if (resourceWorkflow.IsNeedInitDownloader()) {
            Console.WriteLine("需要初始化Minecraft列表，开始获取...");
            var progress = new Progress<int>(percent => {
                viewModel.PercentText = $"加载中... {percent}%";
                if (percent == 100) {
                    viewModel.LatestType = resourceWorkflow.LatestType;
                    viewModel.ReleaseType = resourceWorkflow.ReleaseType;
                    viewModel.SnapshotType = resourceWorkflow.SnapshotType;
                    viewModel.AprilFoolsType = resourceWorkflow.AprilFoolsType;
                    viewModel.OldType = resourceWorkflow.OldType;
                    ResourcePageExtension.AlreadyLoaded(this,MainScrollViewer,LoadingBorder,null,resourceWorkflow.IsNeedInitDownloader());
                    viewModel.PercentText = "加载完成";
                    MessageTips.Show($"获取Minecraft列表完成");
                }
            });
            try {
                await resourceWorkflow.GetMinecraftDownloader(cts.Token,progress).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                Console.WriteLine("LoadMinecraftList取消");
                return;
            }
            catch (Exception e){
                Console.WriteLine(e);
            }
        }
        else {
            Console.WriteLine("无需初始化Minecraft列表，直接使用缓存数据");
            MessageTips.Show("卡顿一下~");
            await Task.Delay(250).ConfigureAwait(false);
            viewModel.LatestType = resourceWorkflow.LatestType;
            viewModel.ReleaseType = resourceWorkflow.ReleaseType;
            viewModel.SnapshotType = resourceWorkflow.SnapshotType;
            viewModel.AprilFoolsType = resourceWorkflow.AprilFoolsType;
            viewModel.OldType = resourceWorkflow.OldType;
            ResourcePageExtension.AlreadyLoaded(this,MainScrollViewer,LoadingBorder,null,resourceWorkflow.IsNeedInitDownloader());
        }
    }

    private async void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        var listView = sender as ListView;
        if (listView == null) {
            return;
        }
        if (listView.SelectedIndex < 0) {
            return;
        }
        
        var downloader = listView.SelectedItem as MinecraftDownloader;
        
        if (downloader == null) {
            return;
        }
        await uiCoordinator.ShowGameInfoAsync(downloader);
        Console.WriteLine($"选择了{downloader}");
        listView.SelectedIndex = -1;
    }
    
    private async void RefreshBtn_OnClick(object sender, RoutedEventArgs e) {
        cts.Cancel();
        await WaitForActiveLoadAsync();
        cts.Dispose();
        cts = new CancellationTokenSource();
        resourceWorkflow.ClearDownloader();
        activeLoadTask = InitMinecraftDownloader();
        await activeLoadTask;
    }

    public Task ActivateAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (cts.IsCancellationRequested) {
            cts.Dispose();
            cts = new CancellationTokenSource();
        }
        activeLoadTask = InitMinecraftDownloader();
        return activeLoadTask;
    }

    public async Task DeactivateAsync() {
        cts.Cancel();
        await WaitForActiveLoadAsync();
        cts.Dispose();
        cts = new CancellationTokenSource();
    }

    private async Task WaitForActiveLoadAsync() {
        try {
            await activeLoadTask;
        }
        catch (OperationCanceledException) {
        }
    }
}
