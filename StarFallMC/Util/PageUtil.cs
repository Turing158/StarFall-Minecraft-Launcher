using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace StarFallMC.Util;

public class PageUtil {
    public static void CleanupPage(Page page, bool blocking = true) {
        if (page == null) return;
        BindingOperations.ClearAllBindings(page);
        page.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        
        // GC.Collect(2, GCCollectionMode.Forced, blocking, true);
        // GC.WaitForPendingFinalizers();
        // page.Dispatcher.Invoke(() => { }, DispatcherPriority.SystemIdle);
        // GC.Collect(2, GCCollectionMode.Forced, blocking, true);
    }
    
    public static void CleanupListView(ListView listView,params DependencyProperty[] onlyCleanProperties) {
        if (listView == null) return;
        BindingOperations.ClearAllBindings(listView);
        var properties = new DependencyProperty[] {
            ItemsControl.ItemsSourceProperty,
            ItemsControl.ItemTemplateProperty,
            ItemsControl.ItemContainerStyleProperty,
            ItemsControl.ItemsPanelProperty,
            FrameworkElement.DataContextProperty,
            FrameworkElement.StyleProperty,
            VirtualizingPanel.IsVirtualizingProperty,
            ListView.ViewProperty
        };
        foreach (var property in onlyCleanProperties.Length > 0 ? onlyCleanProperties : properties) {
            listView.ClearValue(property);
        }

        // listView.ClearValue(ItemsControl.ItemsSourceProperty);
    }
    
    private static void ForceVirtualizingPanelCleanup(ListView listView) {
        // 通过反射强制清理VirtualizingPanel的内部缓存
        try {
            var itemsControlType = typeof(ItemsControl);
            var method = itemsControlType.GetMethod("ClearContainerForItemOverride",
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (method != null && listView.Items != null) {
                // 尝试清理所有项容器
                foreach (var item in listView.Items)
                {
                    var container = listView.ItemContainerGenerator.ContainerFromItem(item);
                    if (container != null)
                    {
                        method.Invoke(listView, new object[] { container, item });
                    }
                }
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"清理虚拟化面板时出错: {ex.Message}");
        }
        
        // 强制ItemContainerGenerator清理
        listView.ItemContainerGenerator.StatusChanged += (s, e) =>
        {
            if (listView.ItemContainerGenerator.Status == 
                System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                // 容器生成完成后立即清理
                var field = typeof(ItemContainerGenerator).GetField("_items",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(listView.ItemContainerGenerator, null);
                }
            }
        };
    }
}