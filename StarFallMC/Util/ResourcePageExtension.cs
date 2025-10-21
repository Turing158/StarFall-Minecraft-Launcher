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
    
    public static void ReloadList(FrameworkElement contentViewer,FrameworkElement loadingViewer,FrameworkElement emptyViewer = null) {
        contentViewer.BeginAnimation(Page.OpacityProperty,ValueTo0);
        contentViewer.IsHitTestVisible = false;
        loadingViewer.Visibility = Visibility.Visible;
        loadingViewer.BeginAnimation(Page.OpacityProperty,ValueTo1);
        contentViewer.IsHitTestVisible = true;
        if (emptyViewer != null) {
            emptyViewer.Visibility = Visibility.Collapsed;
        }
    }

    

    public static void AlreadyLoaded(Page page,FrameworkElement contentViewer,FrameworkElement loadingViewer,FrameworkElement emptyViewer = null,bool resourceEmpty = true) {
        page.Dispatcher.BeginInvoke(() => {
            contentViewer.BeginAnimation(Page.OpacityProperty, ValueTo1);
            contentViewer.IsHitTestVisible = true;
            var hideAnim = new DoubleAnimation() {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase()
            };
            hideAnim.Completed += (_, _) => {
                loadingViewer.Visibility = Visibility.Collapsed;
            };
            loadingViewer.BeginAnimation(Page.OpacityProperty, hideAnim);
            loadingViewer.IsHitTestVisible = false;
            if (emptyViewer != null) {
                if (resourceEmpty) {
                    emptyViewer.Visibility = Visibility.Visible;
                }
                else {
                    emptyViewer.Visibility = Visibility.Collapsed;
                }
            }
        });
    }
}