using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace StarFallMC ;
public static class ScrollViewerExtensions
{
    // 附加属性：启用平滑滚动
    public static readonly DependencyProperty SmoothScrollProperty =
        DependencyProperty.RegisterAttached(
            "SmoothScroll", 
            typeof(bool), 
            typeof(ScrollViewerExtensions), 
            new PropertyMetadata(false, OnSmoothScrollChanged)
        );

    public static bool GetSmoothScroll(DependencyObject obj) => (bool)obj.GetValue(SmoothScrollProperty);
    public static void SetSmoothScroll(DependencyObject obj, bool value) => obj.SetValue(SmoothScrollProperty, value);

    // 当启用平滑滚动时，订阅事件
    private static void OnSmoothScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer scrollViewer && e.NewValue is bool enable)
        {
            if (enable)
            {
                scrollViewer.PreviewMouseWheel += OnPreviewMouseWheel; // 新增滚轮监听
            }
            else
            {
                scrollViewer.PreviewMouseWheel -= OnPreviewMouseWheel;
                
            }
        }
    }
    
    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e) {
        if (sender is ScrollViewer scrollViewer)
        {
            e.Handled = true;
            double delta = e.Delta;
            double targetOffset = scrollViewer.VerticalOffset - delta;
            targetOffset = Math.Max(0, targetOffset);
            targetOffset = Math.Min(scrollViewer.ScrollableHeight, targetOffset);
            AnimateScroll(scrollViewer, targetOffset);
        }
    }
    
    private static void AnimateScroll(ScrollViewer scrollViewer, double targetOffset){
        DoubleAnimation animation = new DoubleAnimation
        {
            To = targetOffset,
            Duration = TimeSpan.FromSeconds(0.15),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        scrollViewer.BeginAnimation(ScrollViewerBehavior.VerticalOffsetProperty, animation);
    }
}

// 绑定 VerticalOffset 的可附加属性
public class ScrollViewerBehavior : DependencyObject
{
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

    // 当 VerticalOffset 变化时，手动滚动到目标位置
    private static void OnVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer scrollViewer)
            scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
    }
}