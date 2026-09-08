using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using StarFallMC.Util.Extension;

namespace StarFallMC.Component;

public class TopButton : ButtonBase{
    public FrameworkElement? BindingScrollViewer {
        get => (FrameworkElement?)GetValue(BindingScrollViewerProperty);
        set => SetValue(BindingScrollViewerProperty, value);
    }
    public static readonly DependencyProperty BindingScrollViewerProperty =
        DependencyProperty.Register(nameof(BindingScrollViewer), typeof(FrameworkElement), typeof(TopButton),
            new PropertyMetadata(null, OnBindingScrollViewerChanged));
    
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
    private ScrollViewer? resolvedScrollViewer;
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
        RebindScrollViewer();
        Dispatcher.BeginInvoke(RebindScrollViewer);
    }

    private void UnloadedHandle(object sender, RoutedEventArgs e) {
        UnbindScrollViewer();
    }

    private static void OnBindingScrollViewerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        if (d is TopButton button && button.IsLoaded) {
            button.RebindScrollViewer();
        }
    }

    private void RebindScrollViewer() {
        UnbindScrollViewer();
        resolvedScrollViewer = BindingScrollViewer as ScrollViewer
            ?? ScrollViewerExtensions.FindScrollViewer(BindingScrollViewer);
        if (resolvedScrollViewer != null) {
            resolvedScrollViewer.ScrollChanged += ScrollChangedHandle;
        }
    }

    private void UnbindScrollViewer() {
        if (resolvedScrollViewer != null) {
            resolvedScrollViewer.ScrollChanged -= ScrollChangedHandle;
            resolvedScrollViewer = null;
        }
    }
    
    private void ScrollChangedHandle(object sender, ScrollChangedEventArgs e) {
        if (resolvedScrollViewer is null) return;
        if (resolvedScrollViewer.VerticalOffset >= ShowToOffsetY) {
            Visibility = Visibility.Visible;
            isVisible = true;
            BeginAnimation(OpacityProperty, showAnimation);
        } else if (isVisible && resolvedScrollViewer.VerticalOffset < CloseToOffsetY) {
            isVisible = false;
            BeginAnimation(OpacityProperty, hideAnimation);
        }
    }

    protected override void OnClick() {
        base.OnClick();
        if (resolvedScrollViewer is null) return;
        resolvedScrollViewer.BeginAnimation(ScrollViewerBehavior.VerticalOffsetProperty, new DoubleAnimation() {
            From = resolvedScrollViewer.VerticalOffset,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase() 
        });
    }
}
