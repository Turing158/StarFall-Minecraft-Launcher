using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using StarFallMC.Util;
using StarFallMC.Services;
using StarFallMC.Services.Download;
using StarFallMC.Services.Minecraft;
using StarFallMC.Navigation;
using StarFallMC.Services.Resources;

namespace StarFallMC;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application {
    
    private DispatcherTimer? _autoSaveTimer;
    private DownloadManager? _downloadManager;
    private DownloadCoordinator? _downloadCoordinator;
    private MinecraftServiceContainer? _minecraftServices;
    private ResourceServiceContainer? _resourceServices;
    private LauncherUiCoordinator? _uiCoordinator;

    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);
        PropertiesUtil.LoadPropertiesJson();
        ApplicationState.LoadFromProperties();
        _uiCoordinator = new LauncherUiCoordinator();
        _downloadManager = new DownloadManager(new DownloadManagerOptions {
            Concurrency = 8,
            RetryCount = 10
        });
        _downloadCoordinator = new DownloadCoordinator(_downloadManager, _uiCoordinator);
        _minecraftServices = new MinecraftServiceContainer(
            installInteraction: new PageLoaderInstallInteraction(_uiCoordinator),
            launchInteraction: new PageMinecraftLaunchInteraction(_uiCoordinator),
            downloadClient: new ApplicationMinecraftDownloadClient(_downloadCoordinator));
        MinecraftServices.Configure(_minecraftServices);
        _resourceServices = new ResourceServiceContainer();
        ResourceServices.Configure(_resourceServices);
        ThemeUtil.init();
        ApplyHardwareAccelerationSetting();
        PropertiesUtil.Save();
        // 启动自动保存定时器，每隔5分钟保存一次配置
        StartAutoSaveTimer();
        var mainWindow = new MainWindow(_uiCoordinator, _downloadCoordinator);
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private void StartAutoSaveTimer() {
        StopAutoSaveTimer();
        _autoSaveTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) {
            Interval = TimeSpan.FromMinutes(5)
        };
        _autoSaveTimer.Tick += AutoSaveTimer_OnTick;
        _autoSaveTimer.Start();
    }

    private void AutoSaveTimer_OnTick(object? sender, EventArgs e) {
        try {
            PropertiesUtil.Save();
            Console.WriteLine("自动保存配置成功");
        }
        catch (Exception exception) {
            Console.WriteLine($"自动保存配置失败: {exception.Message}");
        }
    }

    private void StopAutoSaveTimer() {
        if (_autoSaveTimer == null) {
            return;
        }

        _autoSaveTimer.Stop();
        _autoSaveTimer.Tick -= AutoSaveTimer_OnTick;
        _autoSaveTimer = null;
    }

    internal static void ApplyHardwareAccelerationSetting() {
        RenderOptions.ProcessRenderMode = PropertiesUtil.launcherArgs.HardwareAcceleration ?
            RenderMode.Default : RenderMode.SoftwareOnly;
    }

    internal async Task DisposeDownloadManagerAsync() {
        var manager = _downloadManager;
        if (manager != null) {
            await manager.DisposeAsync();
            Interlocked.CompareExchange(ref _downloadManager, null, manager);
        }
    }

    protected override void OnExit(ExitEventArgs e) {
        // 停止定时器并释放资源
        StopAutoSaveTimer();

        try {
            PropertiesUtil.Save();
        }
        catch (Exception exception) {
            Console.WriteLine($"退出时保存配置失败: {exception.Message}");
        }

        if (_downloadManager != null) {
            Console.WriteLine("应用退出时下载管理器尚未完成异步清理。");
        }

        _minecraftServices?.Dispose();
        _minecraftServices = null;
        _resourceServices?.Dispose();
        _resourceServices = null;
        _downloadCoordinator?.Dispose();
        _downloadCoordinator = null;
        _uiCoordinator = null;

        base.OnExit(e);
    }
    
    
}
