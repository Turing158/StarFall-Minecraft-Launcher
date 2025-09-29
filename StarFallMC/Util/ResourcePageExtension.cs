using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace StarFallMC.Util;

public class ResourcePageExtension{
    public static DoubleAnimation ValueTo0 = new() {
        To = 0,
        Duration = TimeSpan.FromMilliseconds(200),
        EasingFunction = new CubicEase()
    };
    
    public static DoubleAnimation ValueTo1 = new() {
        To = 1,
        Duration = TimeSpan.FromMilliseconds(200),
        EasingFunction = new CubicEase()
    };
    
    public static void ReloadModList(ScrollViewer MainScrollViewer,Border LoadingBorder,Grid NotExist) {
        MainScrollViewer.BeginAnimation(Page.OpacityProperty,ValueTo0);
        MainScrollViewer.IsHitTestVisible = false;
        LoadingBorder.BeginAnimation(Page.OpacityProperty,ValueTo1);
        MainScrollViewer.IsHitTestVisible = true;
        NotExist.Visibility = Visibility.Collapsed;
    }
    
    public static void AlreadyModLoaded<T>(Page page,ScrollViewer MainScrollViewer,Border LoadingBorder,Grid NotExist,List<T> resource) {
        Console.WriteLine("加载完成");
        page.Dispatcher.BeginInvoke(() => {
            MainScrollViewer.BeginAnimation(Page.OpacityProperty, ValueTo1);
            MainScrollViewer.IsHitTestVisible = true;
            LoadingBorder.BeginAnimation(Page.OpacityProperty, ValueTo0);
            LoadingBorder.IsHitTestVisible = false;
            if (resource == null || resource.Count == 0) {
                NotExist.Visibility = Visibility.Visible;
            }
            else {
                NotExist.Visibility = Visibility.Collapsed;
            }
        });
    }
}