using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using StarFallMC.Component;

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
    
    public static void ReloadList(ScrollViewer MainScrollViewer,Loading LoadingBorder,Grid NotExist = null) {
        MainScrollViewer.BeginAnimation(Page.OpacityProperty,ValueTo0);
        MainScrollViewer.IsHitTestVisible = false;
        LoadingBorder.BeginAnimation(Page.OpacityProperty,ValueTo1);
        MainScrollViewer.IsHitTestVisible = true;
        if (NotExist != null) {
            NotExist.Visibility = Visibility.Collapsed;
        }
    }
    
    public static void AlreadyLoaded(Page page,ScrollViewer MainScrollViewer,Loading LoadingBorder,Grid NotExist = null,bool resourceEmpty = true) {
        page.Dispatcher.BeginInvoke(() => {
            MainScrollViewer.BeginAnimation(Page.OpacityProperty, ValueTo1);
            MainScrollViewer.IsHitTestVisible = true;
            LoadingBorder.BeginAnimation(Page.OpacityProperty, ValueTo0);
            LoadingBorder.IsHitTestVisible = false;
            if (NotExist != null) {
                if (resourceEmpty) {
                    NotExist.Visibility = Visibility.Visible;
                }
                else {
                    NotExist.Visibility = Visibility.Collapsed;
                }
            }
        });
    }
}