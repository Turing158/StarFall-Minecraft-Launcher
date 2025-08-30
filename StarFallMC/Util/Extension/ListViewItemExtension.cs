using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace StarFallMC.Util.Extension;

public static class ListViewItemExtension {
    
    public static readonly DependencyProperty IsClickToSelectProperty =
        DependencyProperty.RegisterAttached("IsClickToSelect", typeof(bool), typeof(ListViewItemExtension), new PropertyMetadata(false, OnIsSelectableChanged));
    
    public static bool GetIsClickToSelect(DependencyObject obj) => (bool)obj.GetValue(IsClickToSelectProperty);
    public static void SetIsClickToSelect(DependencyObject obj, bool value) => obj.SetValue(IsClickToSelectProperty, value);
    
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
    
    private static void PreviewMouseLeftButtonDown_OnHandler(object sender, MouseButtonEventArgs e) {
        _isSelecting = true;
        if ((e.OriginalSource as FrameworkElement)?.TemplatedParent is ListViewItem) {
            e.Handled = true;
        }
    }

    private static void MouseLeftButtonUp_OnHandler(object sender, MouseButtonEventArgs e) {
        if (_isSelecting) {
            var listViewItem = sender as ListViewItem;
            var listView = ItemsControl.ItemsControlFromItemContainer(listViewItem) as ListView;
            if (listView != null) {
                listView.SelectedItem = listViewItem.Content;
            }
            _isSelecting = false;
        }
    }
    
    private static void MouseLeave_OnHandler(object sender, MouseEventArgs e) {
        _isSelecting = false;
        
    }
}