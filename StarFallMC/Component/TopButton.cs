using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using StarFallMC.Util.Extension;

namespace StarFallMC.Component;

public class TopButton : ButtonBase{
    public ScrollViewer BindingScrollViewer {
        get => (ScrollViewer)GetValue(BindingScrollViewerProperty);
        set => SetValue(BindingScrollViewerProperty, value);
    }
    public static readonly DependencyProperty BindingScrollViewerProperty =
        DependencyProperty.Register(nameof(BindingScrollViewer), typeof(ScrollViewer), typeof(TopButton), new PropertyMetadata(null));
    
    public double ShowToOffsetY {
        get => (double)GetValue(ShowToOffsetYProperty);
        set => SetValue(ShowToOffsetYProperty, value);
    }
    public static readonly DependencyProperty ShowToOffsetYProperty =
        DependencyProperty.Register(nameof(ShowToOffsetY), typeof(double), typeof(TopButton), new PropertyMetadata(400.0));
    
    public double CloseToOffsetY {
        get => (double)GetValue(CloseToOffsetYProperty);
        set => SetValue(CloseToOffsetYProperty, value);
    }
    public static readonly DependencyProperty CloseToOffsetYProperty =
        DependencyProperty.Register(nameof(CloseToOffsetY), typeof(double), typeof(TopButton), new PropertyMetadata(200.0));

    private DoubleAnimation showAnimation;
    private DoubleAnimation hideAnimation;
    private bool isVisible = false;
    public TopButton() {
        Opacity = 0;
        Visibility = Visibility.Collapsed;
        showAnimation = new DoubleAnimation {
            To = 1,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase() 
        };
        hideAnimation = new DoubleAnimation {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase()
        };
        hideAnimation.Completed += (s, e) => {
            if (!isVisible) {
                Visibility = Visibility.Collapsed;
            }
        };
        Loaded += LoadedHandle;
        Unloaded += UnloadedHandle;
    }
    
    private void LoadedHandle(object sender, RoutedEventArgs e) {
        if (BindingScrollViewer is null) return;
        BindingScrollViewer.ScrollChanged += ScrollChangedHandle;
    }

    private void UnloadedHandle(object sender, RoutedEventArgs e) {
        if (BindingScrollViewer is null) return;
        BindingScrollViewer.ScrollChanged -= ScrollChangedHandle;
    }
    
    private void ScrollChangedHandle(object sender, ScrollChangedEventArgs e) {
        if (BindingScrollViewer is null) return;
        if (BindingScrollViewer.VerticalOffset >= ShowToOffsetY) {
            Visibility = Visibility.Visible;
            isVisible = true;
            BeginAnimation(OpacityProperty, showAnimation);
        } else if (isVisible && BindingScrollViewer.VerticalOffset < CloseToOffsetY) {
            isVisible = false;
            BeginAnimation(OpacityProperty, hideAnimation);
        }
    }

    protected override void OnClick() {
        base.OnClick();
        if (BindingScrollViewer is null) return;
        BindingScrollViewer.BeginAnimation(ScrollViewerBehavior.VerticalOffsetProperty, new DoubleAnimation() {
            From = BindingScrollViewer.VerticalOffset,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase() 
        });
    }
}