using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using StarFallMC.Util;

namespace StarFallMC;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application {
    
    public static Action HardwareAccelerationSetting;
    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);
        PropertiesUtil.LoadPropertiesJson();
        DownloadUtil.init(15,3);
        ThemeUtil.init();
        
        HardwareAccelerationSetting = hardwareAccelerationSetting;
    }

    private void hardwareAccelerationSetting() {
        RenderOptions.ProcessRenderMode = PropertiesUtil.launcherArgs.HardwareAcceleration ?
            RenderMode.Default : RenderMode.SoftwareOnly;
    }
    
    
}