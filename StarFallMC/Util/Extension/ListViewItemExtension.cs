using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace StarFallMC.Util.Extension;

public static class ListViewItemExtension {
    
    public static readonly DependencyProperty IsClickToSelectProperty =
        DependencyProperty.RegisterAttached("IsClickToSelect", typeof(bool), typeof(ListViewItemExtension), new PropertyMetadata(false, OnIsSelectableChanged));
    
    public static bool GetIsClickToSelect(DependencyObject obj) => (bool)obj.GetValue(IsClickToSelectProperty);
    public static void SetIsClickToSelect(DependencyObject obj, bool value) => obj.SetValue(IsClickToSelectProperty, value);
    
    public static readonly DependencyProperty ClickAnimateProperty =
        DependencyProperty.RegisterAttached("ClickAnimate", typeof(bool), typeof(ListViewItemExtension), new PropertyMetadata(false,OnClickAnimateChanged));

    public static bool GetClickAnimate(DependencyObject obj) => (bool)obj.GetValue(ClickAnimateProperty);
    public static void SetClickAnimate(DependencyObject obj, bool value) => obj.SetValue(ClickAnimateProperty, value);
    
    private static void OnClickAnimateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        var listViewItem = d as ListViewItem;
        if (listViewItem == null) {
            return;
        }

        if ((bool)e.NewValue) {
            (d as ListViewItem).RenderTransform = new ScaleTransform();
            (d as ListViewItem).RenderTransformOrigin = new Point(0.5, 0.5);
        }
        else {
            (d as ListViewItem).RenderTransform = null;
            (d as ListViewItem).RenderTransformOrigin = new Point(0,0);
        }
    }
    
    private static void OnIsSelectableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        var listViewItem = d as ListViewItem;
        if (listViewItem == null) {
            return;
        }
        if ((bool)e.NewValue) {
            listViewItem.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, 
                new MouseButtonEventHandler(PreviewMouseLeftButtonDown_OnHandler), true);
            listViewItem.MouseLeftButtonUp += MouseLeftButtonUp_OnHandler;
            listViewItem.MouseLeave += MouseLeave_OnHandler;
        }
        else {
            listViewItem.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, 
                new MouseButtonEventHandler(PreviewMouseLeftButtonDown_OnHandler));
            listViewItem.MouseLeftButtonUp -= MouseLeftButtonUp_OnHandler;
            listViewItem.MouseLeave -= MouseLeave_OnHandler;
        }
    }

    private static bool _isSelecting;

    public static DoubleAnimation ValueTo1 = new() {
        To = 1,
        Duration = TimeSpan.FromMilliseconds(200),
        EasingFunction = new CubicEase()
    };
    
    public static DoubleAnimation ValueTo098 = new() {
        To = 0.98,
        Duration = TimeSpan.FromMilliseconds(200),
        EasingFunction = new CubicEase()
    };
    
    private static void PreviewMouseLeftButtonDown_OnHandler(object sender, MouseButtonEventArgs e) {
        _isSelecting = true;
        var item = sender as ListViewItem;
        if (item.RenderTransform is ScaleTransform) {
            item.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty,ValueTo098);
            item.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty,ValueTo098);
        }
        if ((e.OriginalSource as FrameworkElement)?.TemplatedParent is ListViewItem) {
            e.Handled = true;
        }
    }

    private static void MouseLeftButtonUp_OnHandler(object sender, MouseButtonEventArgs e) {
        var listViewItem = sender as ListViewItem;
        if (listViewItem.RenderTransform is ScaleTransform) {
            listViewItem.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty,ValueTo1);
            listViewItem.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty,ValueTo1);
        }
        if (_isSelecting) {
            var listView = ItemsControl.ItemsControlFromItemContainer(listViewItem) as ListView;
            if (listView != null) {
                listView.SelectedItem = listViewItem.Content;
            }
            _isSelecting = false;
        }
    }
    
    private static void MouseLeave_OnHandler(object sender, MouseEventArgs e) {
        _isSelecting = false;
        var item = sender as ListViewItem;
        if (item.RenderTransform is ScaleTransform) {
            item.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty,ValueTo1);
            item.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty,ValueTo1);
        }
    }
}