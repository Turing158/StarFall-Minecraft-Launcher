using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StarFallMC.Entity;
using StarFallMC.Util;

namespace StarFallMC.Component;

public partial class NavigationBar : ScrollViewer {
    
    public enum AnimationType {
        None,
        Elastic,
        Cubic,
        Bounce
    }
    
    public AnimationType Animation {
        get => (AnimationType)GetValue(AnimationProperty);
        set => SetValue(AnimationProperty, value);
    }
    public static readonly DependencyProperty AnimationProperty =
        DependencyProperty.Register(nameof(Animation), typeof(AnimationType), typeof(NavigationBar), new PropertyMetadata(AnimationType.Elastic));
    
    public Brush ActiveBlockColor {
        get => (Brush)GetValue(ActiveBlockColorProperty);
        set => SetValue(ActiveBlockColorProperty, value);
    }
    public static readonly DependencyProperty ActiveBlockColorProperty =
        DependencyProperty.Register(nameof(ActiveBlockColor), typeof(Brush), typeof(NavigationBar), new PropertyMetadata(ThemeUtil.SecondaryBrush_1));
    
    public Brush ActiveForeground {
        get => (Brush)GetValue(ActiveForegroundProperty);
        set => SetValue(ActiveForegroundProperty, value);
    }
    public static readonly DependencyProperty ActiveForegroundProperty =
        DependencyProperty.Register(nameof(ActiveForeground), typeof(Brush), typeof(NavigationBar), new PropertyMetadata(ThemeUtil.PrimaryBrush));
    
    public double ItemFontSize {
        get => (double)GetValue(ItemFontSizeProperty); 
        set => SetValue(ItemFontSizeProperty, value);
    }
    public static readonly DependencyProperty ItemFontSizeProperty =
        DependencyProperty.Register(nameof(ItemFontSize), typeof(double), typeof(NavigationBar), new PropertyMetadata(14.0));
    
    public Brush ItemBackground {
        get => (Brush)GetValue(ItemBackgroundProperty); 
        set => SetValue(ItemBackgroundProperty, value);
    }
    public static readonly DependencyProperty ItemBackgroundProperty =
        DependencyProperty.Register(nameof(ItemBackground), typeof(Brush), typeof(NavigationBar), new PropertyMetadata(ThemeUtil.PrimaryBrush));
    
    public Brush ItemForeground {
        get => (Brush)GetValue(ItemForegroundProperty);
        set => SetValue(ItemForegroundProperty, value);
    }
    public static readonly DependencyProperty ItemForegroundProperty =
        DependencyProperty.Register(nameof(ItemForeground), typeof(Brush), typeof(NavigationBar), new PropertyMetadata(ThemeUtil.SecondaryBrush_1));
    
    public Brush ItemHoverColor {
        get => (Brush)GetValue(ItemHoverColorProperty);
        set => SetValue(ItemHoverColorProperty, value);
    }
    public static readonly DependencyProperty ItemHoverColorProperty =
        DependencyProperty.Register(nameof(ItemHoverColor), typeof(Brush), typeof(NavigationBar), new PropertyMetadata(ThemeUtil.SecondaryBrush_2));
    
    public CornerRadius ItemRadius {
        get => (CornerRadius)GetValue(ItemRadiusProperty);
        set => SetValue(ItemRadiusProperty, value);
    }
    public static readonly DependencyProperty ItemRadiusProperty =
        DependencyProperty.Register(nameof(ItemRadius), typeof(CornerRadius), typeof(NavigationBar), new PropertyMetadata(new CornerRadius(20)));
    
    public double ItemWidth {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }
    public static readonly DependencyProperty ItemWidthProperty =
        DependencyProperty.Register(nameof(ItemWidth), typeof(double), typeof(NavigationBar), new PropertyMetadata(double.NaN));
    
    public double ItemHeight {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }
    public static readonly DependencyProperty ItemHeightProperty =
        DependencyProperty.Register(nameof(ItemHeight), typeof(double), typeof(NavigationBar), new PropertyMetadata(double.NaN));

    public ObservableCollection<NavigationItem> ItemsSource {
        get => (ObservableCollection<NavigationItem>)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(ObservableCollection<NavigationItem>), typeof(NavigationBar), new PropertyMetadata(new ObservableCollection<NavigationItem>()));
    
    public int SelectedIndex {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }
    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(NavigationBar), new PropertyMetadata(0));

    public NavigationItem CurrentItem {
        get {
            if (ItemsSource.Count == 0 || SelectedIndex < 0) {
                return null;
            }
            return ItemsSource[SelectedIndex];
        }
    }
    
    public event SelectionChangedEventHandler SelectionChanged {
        add { AddHandler(SelectionChangedEvent, value); }
        remove { RemoveHandler(SelectionChangedEvent, value); }
    }
    public static readonly RoutedEvent SelectionChangedEvent =
        EventManager.RegisterRoutedEvent("SelectionChanged", RoutingStrategy.Bubble, typeof(SelectionChangedEventHandler), typeof(NavigationBar));
    
    public Thickness ItemMargin {
        get => (Thickness)GetValue(ItemMarginProperty);
        set => SetValue(ItemMarginProperty, value);
    }
    public static readonly DependencyProperty ItemMarginProperty =
        DependencyProperty.Register(nameof(ItemMargin), typeof(Thickness), typeof(NavigationBar), new PropertyMetadata(new Thickness(5, 0, 5, 0)));
    
    public Thickness ItemPadding {
        get => (Thickness)GetValue(ItemPaddingProperty);
        set => SetValue(ItemPaddingProperty, value);
    }
    public static readonly DependencyProperty ItemPaddingProperty =
        DependencyProperty.Register(nameof(ItemPadding), typeof(Thickness), typeof(NavigationBar), new PropertyMetadata(new Thickness(15, 8, 15, 8)));
    
    public Orientation Orientation {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(NavigationBar), new PropertyMetadata(Orientation.Horizontal));
    
    public string NaviIndexPath { get; private set; }
    
    private EasingFunctionBase _animationFunction;
    
    public NavigationBar() {
        InitializeComponent();
        switch (Animation) {
            case AnimationType.Elastic:
                _animationFunction = new ElasticEase{
                    Oscillations = 1,
                    Springiness = 10,
                    EasingMode = EasingMode.EaseOut
                };
                break;
            case AnimationType.Cubic:
                _animationFunction = new CubicEase();
                break;
            case AnimationType.Bounce:
                _animationFunction = new BounceEase{
                    Bounciness = 10,
                };
                break;
        }
    }
    
    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        SelectionChangedEventArgs args = new SelectionChangedEventArgs(SelectionChangedEvent, e.RemovedItems, e.AddedItems);
        args.Source = this;
        RaiseEvent(args);
        
        var listView = sender as ListView;
        var item = listView.ItemContainerGenerator.ContainerFromIndex(listView.SelectedIndex) as ListViewItem;
        if (item != null) {
            if (e.RemovedItems.Count > 0) {
                var pre = e.RemovedItems[0] as NavigationItem;
                if (pre.Children == null || pre.Children.Count == 0) {
                    UpdateActiveBlock(item);
                }
            }
            var secondMenu = item.Template.FindName("SecondMenu", item) as NavigationBar;
            NaviIndexPath = $"{listView.SelectedIndex+1}";
            if (secondMenu.Visibility == Visibility.Visible) {
                NaviIndexPath = $"{listView.SelectedIndex+1},{secondMenu.SelectedIndex+1}";
            }
        }
    }

    private void ListView_OnLoaded(object sender, RoutedEventArgs e) {
        if (ItemsSource != null && ItemsSource.Count > 0) {
            if (SelectedIndex == -1) {
                SelectedIndex = 0;
            }
            var item = ListView.ItemContainerGenerator.ContainerFromIndex(ListView.SelectedIndex) as ListViewItem;
            if (item != null) {
                //这里可能需要解决方块大小的问题，有二级菜单会有问题(有问题找这里)
                Grid main = item.Template.FindName("Main",item) as Grid;
                ActiveBlock.Width = main.ActualWidth;
                ActiveBlock.Height = main.ActualHeight;
                Point itemPoint = item.TranslatePoint(new Point(0, 0), ListView);
                ActiveBlock.RenderTransform = new TranslateTransform(itemPoint.X, itemPoint.Y);
            }
        }
    }

    private void UpdateActiveBlock(ListViewItem item) {
        Dispatcher.BeginInvoke(() => {
            Point itemPoint = item.TranslatePoint(new Point(0, 0), ListView);
            var moveAnim = new DoubleAnimation {
                To = Orientation == Orientation.Horizontal ? itemPoint.X : itemPoint.Y,
                Duration = TimeSpan.FromMilliseconds(Animation == AnimationType.Elastic || Animation == AnimationType.Cubic ? 500 : 200),
                EasingFunction = _animationFunction
            };
            ActiveBlock.RenderTransform.BeginAnimation(
                Orientation == Orientation.Horizontal ? 
                    TranslateTransform.XProperty : 
                    TranslateTransform.YProperty,
                moveAnim);
            var itemMain = item.Template.FindName("Main", item) as Grid;
            var sizeAnim = new DoubleAnimation {
                To = Orientation == Orientation.Horizontal ? itemMain.ActualWidth : itemMain.ActualHeight,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase()
            };
            ActiveBlock.BeginAnimation(
                Orientation == Orientation.Horizontal ? WidthProperty : HeightProperty,
                sizeAnim);
        });
    }

    private void UpdateSecondMenu(ListViewItem item,bool isSelect) {
        Dispatcher.BeginInvoke(() => {
            var secondMenu = item.Template.FindName("SecondMenu", item) as NavigationBar;
            if (secondMenu.Visibility == Visibility.Visible) {
                var border = item.Template.FindName("SecondMenuGrid", item) as Border;
                var sizeValue = Orientation == Orientation.Horizontal
                    ? secondMenu.ActualWidth
                    : secondMenu.ActualHeight;
                var sizeAnim = new DoubleAnimation {
                    To = isSelect ? sizeValue : 0,
                    Duration = TimeSpan.FromMilliseconds(100),
                    EasingFunction = new CubicEase()
                };
                if (!isSelect) {
                    sizeAnim.Completed += (s, e) => {
                        Dispatcher.BeginInvoke(() => {
                            UpdateActiveBlock(
                                ListView.ItemContainerGenerator.ContainerFromIndex(ListView.SelectedIndex) as
                                    ListViewItem);
                        });
                    };
                }
                border.BeginAnimation(
                    Orientation == Orientation.Horizontal ? WidthProperty : HeightProperty,
                    sizeAnim);
            }
        });
    }

    private void NavigationBar_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        var secondMenu = sender as NavigationBar;
        
    }

    private void ListViewItemUnSelected_OnHandler(object sender, RoutedEventArgs e) {
        UpdateSecondMenu(sender as ListViewItem, false);
       
    }

    private void ListViewItemSelected_OnHandler(object sender, RoutedEventArgs e) {
        UpdateSecondMenu(sender as ListViewItem, true);
    }
}