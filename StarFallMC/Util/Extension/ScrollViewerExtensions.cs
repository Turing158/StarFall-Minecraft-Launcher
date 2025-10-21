using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace StarFallMC.Util.Extension;
public static class ScrollViewerExtensions {
    
    public static readonly DependencyProperty SmoothScrollProperty =
        DependencyProperty.RegisterAttached(
            "SmoothScroll", 
            typeof(bool), 
            typeof(ScrollViewerExtensions), 
            new PropertyMetadata(false, OnSmoothScrollChanged)
        );

    public static bool GetSmoothScroll(DependencyObject obj) => (bool)obj.GetValue(SmoothScrollProperty);
    public static void SetSmoothScroll(DependencyObject obj, bool value) => obj.SetValue(SmoothScrollProperty, value);
    
    private static void OnSmoothScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer scrollViewer && e.NewValue is bool enable) {
            if (enable) {
                scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel; // 新增滚轮监听
            }
            else {
                scrollViewer.PreviewMouseWheel -= OnPreviewMouseWheel;
                
            }
        }
    }
    
    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e) {
        if (sender is ScrollViewer scrollViewer) {
            e.Handled = true;
            double delta = e.Delta;
            double targetOffset = scrollViewer.VerticalOffset - delta;
            targetOffset = Math.Max(0, targetOffset);
            targetOffset = Math.Min(scrollViewer.ScrollableHeight, targetOffset);
            AnimateScroll(scrollViewer, targetOffset);
        }
    }

    private static void OnPreviewMouseWheelDisabled(object sender, MouseWheelEventArgs e) {
        if (sender is ScrollViewer scrollViewer) {
            e.Handled = true;
        }
    }

    public static void ScrollEnabled(ScrollViewer scrollViewer,bool isEnabled) {
        if (isEnabled) {
            scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel;
            scrollViewer.PreviewMouseWheel -= OnPreviewMouseWheelDisabled;
        }
        else {
            scrollViewer.PreviewMouseWheel -= OnPreviewMouseWheel;
            scrollViewer.PreviewMouseWheel += OnPreviewMouseWheelDisabled;
        }
        
    }

    public static void AnimateScroll(ScrollViewer scrollViewer, double targetOffset,bool isHorizontal = false, Action onCompleted = null) {
        DoubleAnimation animation = new DoubleAnimation {
            To = targetOffset,
            Duration = TimeSpan.FromSeconds(0.15),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        if (onCompleted != null) {
            animation.Completed += (s, e) => onCompleted();
        }

        if (isHorizontal) {
            scrollViewer.BeginAnimation(ScrollViewerBehavior.HorizontalOffsetProperty, animation);
        }
        else {
            scrollViewer.BeginAnimation(ScrollViewerBehavior.VerticalOffsetProperty, animation);
        }
    }
}

public class ScrollViewerBehavior : DependencyObject {
    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.RegisterAttached(
            "VerticalOffset", 
            typeof(double), 
            typeof(ScrollViewerBehavior), 
            new FrameworkPropertyMetadata(
                0.0, 
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, 
                OnVerticalOffsetChanged
            )
        );

    public static double GetVerticalOffset(DependencyObject obj) => (double)obj.GetValue(VerticalOffsetProperty);
    public static void SetVerticalOffset(DependencyObject obj, double value) => obj.SetValue(VerticalOffsetProperty, value);
    
    private static void OnVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        if (d is ScrollViewer scrollViewer)
            scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
    }
    
    public static readonly DependencyProperty HorizontalOffsetProperty =
        DependencyProperty.RegisterAttached(
            "HorizontalOffset", 
            typeof(double), 
            typeof(ScrollViewerBehavior), 
            new FrameworkPropertyMetadata(
                0.0, 
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, 
                OnHorizontalOffsetChanged
            )
        );

    public static double GetHorizontalOffset(DependencyObject obj) => (double)obj.GetValue(HorizontalOffsetProperty);
    public static void SetHorizontalOffset(DependencyObject obj, double value) => obj.SetValue(HorizontalOffsetProperty, value);
    
    private static void OnHorizontalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        if (d is ScrollViewer scrollViewer)
            scrollViewer.ScrollToHorizontalOffset((double)e.NewValue);
    }
}