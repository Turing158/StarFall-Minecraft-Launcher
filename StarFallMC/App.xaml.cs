using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Threading;
using System.Threading.Tasks;
using StarFallMC.Util;

namespace StarFallMC;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application {
    
    public static Action HardwareAccelerationSetting;
    private Timer _autoSaveTimer;

    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);
        PropertiesUtil.LoadPropertiesJson();
        DownloadUtil.init(30,10);
        ThemeUtil.init();
        HardwareAccelerationSetting = hardwareAccelerationSetting;
        PropertiesUtil.Save();
        // 启动自动保存定时器，每隔5分钟保存一次配置
        StartAutoSaveTimer();
    }

    private void StartAutoSaveTimer() {
        _autoSaveTimer = new Timer(async _ => {
            try {
                // 异步执行保存操作
                await Task.Run(() => {
                    PropertiesUtil.Save();
                    Console.WriteLine($"自动保存配置成功");
                });
            } catch (Exception ex) {
                // 记录异常但不影响程序运行
                Console.WriteLine($"自动保存配置失败: {ex.Message}");
            }
        }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    private void hardwareAccelerationSetting() {
        RenderOptions.ProcessRenderMode = PropertiesUtil.launcherArgs.HardwareAcceleration ?
            RenderMode.Default : RenderMode.SoftwareOnly;
    }

    protected override void OnExit(ExitEventArgs e) {
        // 停止定时器并释放资源
        _autoSaveTimer?.Dispose();
        _autoSaveTimer = null;

        base.OnExit(e);
    }
    
    
}