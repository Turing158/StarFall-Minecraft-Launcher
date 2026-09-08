using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using StarFallMC.Entity;
using StarFallMC.Navigation;
using StarFallMC.SettingPages;

namespace StarFallMC;

public partial class Setting : Page, IPageLifecycle {

    private ViewModel viewModel = new ViewModel();
    
    private Storyboard NaviBarChangeAnim;

    private readonly ContentNavigationHost navigationHost;
    private readonly LauncherUiCoordinator uiCoordinator;
    private readonly Services.Download.DownloadCoordinator downloadCoordinator;
    private CancellationTokenSource? navigationDelayCts;
    private Task navigationDelayTask = Task.CompletedTask;
    private long navigationGeneration;
    private bool lifecycleActive;
    private bool suppressSelectionChanged;
    
    public Setting(
        LauncherUiCoordinator? uiCoordinator = null,
        Services.Download.DownloadCoordinator? downloadCoordinator = null) {
        this.uiCoordinator = uiCoordinator ?? new LauncherUiCoordinator();
        this.downloadCoordinator = downloadCoordinator ??
            new Services.Download.DownloadCoordinator(this.uiCoordinator);
        InitializeComponent();
        navigationHost = new ContentNavigationHost(PageFrame);
        DataContext = viewModel;
        NaviBarChangeAnim = (Storyboard) FindResource("NaviBarChangeAnim");
    }
    
    public class ViewModel : INotifyPropertyChanged {
        private ObservableCollection<NavigationItem> _navi = new () {
            new NavigationItem("游戏设置","GameSetting"),
            new NavigationItem("启动器设置","LauncherSetting"),
            new NavigationItem("关于","About"),
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
    private async void NaviBar_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (!lifecycleActive || suppressSelectionChanged) {
            return;
        }

        await NavigateToSelectedPageAsync(useDelay: true);
    }

    private async Task NavigateToSelectedPageAsync(bool useDelay) {
        var item = SettingBar.CurrentItem;
        if (item == null || string.IsNullOrEmpty(item.Path)) {
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
            if (useDelay) {
                navigationDelayTask = Task.Delay(300, delayCts.Token);
                await navigationDelayTask;
            }
            await navigationHost.ShowAsync(item.Path, () => CreatePage(item.Path), delayCts.Token);
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

    internal async Task SelectPageAsync(int index) {
        suppressSelectionChanged = true;
        try {
            SettingBar.SelectedIndex = index;
        }
        finally {
            suppressSelectionChanged = false;
        }

        if (lifecycleActive) {
            await NavigateToSelectedPageAsync(useDelay: false);
        }
    }

    internal FrameworkElement? CurrentPage => navigationHost.CurrentPage;

    private Page CreatePage(string path) {
        return path switch {
            "GameSetting" => new GameSetting(),
            "LauncherSetting" => new LauncherSetting(uiCoordinator, downloadCoordinator),
            "About" => new About(),
            _ => throw new ArgumentOutOfRangeException(nameof(path), path, "Unknown setting page.")
        };
    }

    public async Task ActivateAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        lifecycleActive = true;
        await NavigateToSelectedPageAsync(useDelay: false);
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
}
