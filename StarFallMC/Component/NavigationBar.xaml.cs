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
        DependencyProperty.Register(nameof(ItemPadding), typeof(Thickness), typeof(NavigationBar), new PropertyMetadata(new Thickness(15, 5, 15, 5)));
    
    public Orientation Orientation {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(NavigationBar), new PropertyMetadata(Orientation.Horizontal));
    
    private EasingFunctionBase _animationFunction;
    
    public NavigationBar() {
        InitializeComponent();
        ThemeUtil.updateColor += () => {
            Dispatcher.BeginInvoke(() => {
                ActiveBlockColor = ThemeUtil.SecondaryBrush_1;
                ActiveForeground = ThemeUtil.PrimaryBrush;
                ItemBackground = ThemeUtil.PrimaryBrush;
                ItemForeground = ThemeUtil.SecondaryBrush_1;
                ItemHoverColor = ThemeUtil.SecondaryBrush_2;
            });
        };
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
            UpdateActiveBlock(item, listView);
        }
    }

    private void ListView_OnLoaded(object sender, RoutedEventArgs e) {
        if (ItemsSource.Count > 0) {
            if (SelectedIndex == -1) {
                SelectedIndex = 0;
            }
            var listView = sender as ListView;
            var item = listView.ItemContainerGenerator.ContainerFromIndex(listView.SelectedIndex) as ListViewItem;
            if (item != null) {
                ActiveBlock.Width = item.ActualWidth;
                ActiveBlock.Height = item.ActualHeight;
                Point itemPoint = item.TranslatePoint(new Point(0, 0), listView);
                ActiveBlock.RenderTransform = new TranslateTransform(itemPoint.X, itemPoint.Y);
            }
        }
    }

    private void UpdateActiveBlock(ListViewItem item, ListView listView) {
        Point itemPoint = item.TranslatePoint(new Point(0, 0), listView);
        var moveAnim = new DoubleAnimation {
            To = Orientation == Orientation.Horizontal ? itemPoint.X : itemPoint.Y,
            Duration = TimeSpan.FromMilliseconds(Animation == AnimationType.Elastic || Animation == AnimationType.Cubic ? 500 : 200),
            EasingFunction = _animationFunction
        };
        var sizeAnim = new DoubleAnimation {
            To = Orientation == Orientation.Horizontal ? item.ActualWidth : item.ActualHeight,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase()
        };
        ActiveBlock.RenderTransform.BeginAnimation(
            Orientation == Orientation.Horizontal ? 
                TranslateTransform.XProperty : 
                TranslateTransform.YProperty,
            moveAnim);
        ActiveBlock.BeginAnimation(
            Orientation == Orientation.Horizontal ? 
                WidthProperty : 
                HeightProperty,
            sizeAnim);
    }
}