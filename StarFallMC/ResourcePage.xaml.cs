using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.Entity.Enum;
using StarFallMC.ResourcePages;
using StarFallMC.Util;
using StarFallMC.Navigation;
using StarFallMC.Services.Download;
using StarFallMC.Services.Resources;


namespace StarFallMC;



public partial class ResourcePage : Page, IPageLifecycle {

    private ViewModel viewModel = new ViewModel();
    
    private Storyboard NaviBarChangeAnim;

    private readonly ContentNavigationHost navigationHost;
    private readonly LauncherUiCoordinator uiCoordinator;
    private readonly DownloadCoordinator downloadCoordinator;
    private readonly ResourceWorkflowService resourceWorkflow;
    private CancellationTokenSource? navigationDelayCts;
    private Task navigationDelayTask = Task.CompletedTask;
    private long navigationGeneration;
    private bool lifecycleActive;
    
    private string tempPagePath = "TexturePacksPage";
    private string? currentPagePath;
    
    public ResourcePage(
        LauncherUiCoordinator? uiCoordinator = null,
        DownloadCoordinator? downloadCoordinator = null,
        ResourceWorkflowService? resourceWorkflow = null) {
        this.uiCoordinator = uiCoordinator ?? new LauncherUiCoordinator();
        this.downloadCoordinator = downloadCoordinator ?? new DownloadCoordinator(this.uiCoordinator);
        this.resourceWorkflow = resourceWorkflow ?? new ResourceWorkflowService(this.uiCoordinator);
        InitializeComponent();
        DataContext = viewModel;
        NaviBarChangeAnim = (Storyboard)FindResource("NaviBarChangeAnim");
        navigationHost = new ContentNavigationHost(PageFrame);
        
        //初始化ModData
        this.resourceWorkflow.GetMcModDataInit();
    }
    
    public class ViewModel : INotifyPropertyChanged {
        
        private ObservableCollection<NavigationItem> _navi = new () {
            new ("\ue7f9",20,
                0,new ObservableCollection<NavigationItem> {
                    new ("材质包","TexturePacksPage"),
                    new ("地图","SavesPage"),
                    new ("模组","ModsPage"),
                }),
            new ("Minecraft","DownloadGame"),
            new ("社区资源","ModResources",
                0 ,new ObservableCollection<NavigationItem>() {
                    new ("Mod",tag:ResourceType.Mod),
                    new ("整合包",tag:ResourceType.ModPack),
                    new ("材质包",tag:ResourceType.TexturePack),
                    new ("光影包",tag:ResourceType.ShaderPack),
                    new ("数据包",tag:ResourceType.DataPack),
                }),
        };
        public ObservableCollection<NavigationItem> Navi {
            get=> _navi;
            set => SetField(ref _navi, value);
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
    private bool theFirstEnter = true;
    private async void NaviBar_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (theFirstEnter || !lifecycleActive) {
            return;
        }
        NaviBarChangeAnim.Begin(this, true);
        long generation = Interlocked.Increment(ref navigationGeneration);
        await CancelNavigationDelayAsync();
        if (generation != Volatile.Read(ref navigationGeneration) || !lifecycleActive) {
            return;
        }
        var delayCts = new CancellationTokenSource();
        navigationDelayCts = delayCts;
        try {
            navigationDelayTask = Task.Delay(300, delayCts.Token);
            await navigationDelayTask;
            var item = ResourceBar.CurrentItem;
            if (item == null) {
                return;
            }
            string path = item.Path;
            ResourceType resourceType = ResourceType.Mod;
            if (item.Children != null && item.Children.Count > item.ChildrenIndex && item.Children[item.ChildrenIndex] is NavigationItem child) {
                if (!string.IsNullOrEmpty(child.Path)) {
                    path = child.Path;
                }
                resourceType = child.Tag as ResourceType? ?? ResourceType.Mod;
            }
            await ShowPageAsync(path, resourceType, delayCts.Token);
        }
        catch (OperationCanceledException) {
        }
        catch (Exception exception) {
            Console.WriteLine(exception);
        }
        finally {
            if (ReferenceEquals(navigationDelayCts, delayCts)) {
                navigationDelayCts = null;
                navigationDelayTask = Task.CompletedTask;
                delayCts.Dispose();
            }
        }
    }

    private async Task ShowPageAsync(string path, ResourceType resourceType = ResourceType.Mod, CancellationToken cancellationToken = default) {
        if (string.IsNullOrEmpty(path)) {
            return;
        }
        currentPagePath = path;
        var key = path == "ModResources" ? $"{path}:{resourceType}" : path;
        await navigationHost.ShowAsync(key, () => CreatePage(path, resourceType), cancellationToken);
    }

    private Page CreatePage(string path, ResourceType resourceType) {
        return path switch {
            "TexturePacksPage" => new TexturePacksPage(),
            "SavesPage" => new SavesPage(uiCoordinator),
            "ModsPage" => new ModsPage(uiCoordinator),
            "DownloadGame" => new DownloadGame(uiCoordinator, downloadCoordinator, resourceWorkflow),
            "ModResources" => new ModResources(resourceType, uiCoordinator),
            _ => throw new ArgumentOutOfRangeException(nameof(path), path, "Unknown resource page.")
        };
    }
    private string[] needReloadPage = {"ModsPage","SavesPage","TexturePacksPage"};
    internal async Task ChangeVersionAsync() {
        if (currentPagePath != null && needReloadPage.Contains(currentPagePath)) {
            Console.WriteLine("切换版本，清空页面，需要重新加载");
            tempPagePath = currentPagePath;
            currentPagePath = null;
            await navigationHost.ClearAsync();
        }
    }
    
    internal async Task LoadTempPageAsync() {
        if (!string.IsNullOrEmpty(tempPagePath)) {
            Console.WriteLine("存在切换版本，加载临时页面");
            var path = tempPagePath;
            tempPagePath = "";
            await ShowPageAsync(path);
        }
    }

//  此方法為了不讓啓動器加載后直接加載第一個ResourcePage頁面
    internal void TheFirstEnterResourcePage() {
        if (theFirstEnter) {
            theFirstEnter = false;
        }
    }

    public async Task ActivateAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        lifecycleActive = true;
        TheFirstEnterResourcePage();
        await LoadTempPageAsync();
        if (navigationHost.CurrentPage == null) {
            var item = ResourceBar.CurrentItem;
            if (item != null) {
                var path = item.Path;
                var resourceType = ResourceType.Mod;
                if (item.Children != null && item.Children.Count > item.ChildrenIndex && item.Children[item.ChildrenIndex] is NavigationItem child) {
                    if (!string.IsNullOrEmpty(child.Path)) {
                        path = child.Path;
                    }
                    resourceType = child.Tag as ResourceType? ?? ResourceType.Mod;
                }
                await ShowPageAsync(path, resourceType, cancellationToken);
            }
        }
    }

    public async Task DeactivateAsync() {
        lifecycleActive = false;
        Interlocked.Increment(ref navigationGeneration);
        await CancelNavigationDelayAsync();
        await navigationHost.ClearAsync();
    }

    private async Task CancelNavigationDelayAsync() {
        var oldCts = Interlocked.Exchange(ref navigationDelayCts, null);
        var oldTask = Interlocked.Exchange(ref navigationDelayTask, Task.CompletedTask);
        oldCts?.Cancel();
        try {
            await oldTask;
        }
        catch (OperationCanceledException) {
        }
        finally {
            oldCts?.Dispose();
        }
    }

    internal Task ClearAsync() => DeactivateAsync();

    internal FrameworkElement? CurrentPage => navigationHost.CurrentPage;
}
