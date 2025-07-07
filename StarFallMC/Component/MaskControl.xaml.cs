using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace StarFallMC.Component;

public partial class MaskControl : UserControl {
    
    public Brush MaskColor {
        get => (Brush)GetValue(MaskColorProperty);
        set => SetValue(MaskColorProperty, value);
    }
    public static readonly DependencyProperty MaskColorProperty = DependencyProperty.Register(nameof(MaskColor),
        typeof(Brush), typeof(MaskControl), new PropertyMetadata(Brushes.Black, OnMaskPropertyChanged));


    public double MaskOpacity {
        get => (double)GetValue(MaskOpacityProperty);
        set => SetValue(MaskOpacityProperty, value);
    }
    public static readonly DependencyProperty MaskOpacityProperty = DependencyProperty.Register(nameof(MaskOpacity),
        typeof(double), typeof(MaskControl), new PropertyMetadata(0.5, OnMaskPropertyChanged)); 
    
    public event RoutedEventHandler OnHidden {
        add => AddHandler(OnHiddenEvent, value);
        remove => RemoveHandler(OnHiddenEvent, value);
    }
    public static readonly RoutedEvent OnHiddenEvent = EventManager.RegisterRoutedEvent(nameof(OnHidden),
        RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MaskControl));

    private void onHiddenEventFunc() {
        RoutedEventArgs args = new RoutedEventArgs(OnHiddenEvent);
        RaiseEvent(args);
    }
    
    private Timer HideTimer;
    private static void OnMaskPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        
    }
    
    
    private Storyboard MaskShowAnim;
    private Storyboard MaskHideAnim;
    
    public MaskControl() {
        InitializeComponent();
        Opacity = 0;
        Visibility = Visibility.Collapsed;
        MaskShowAnim = (Storyboard)FindResource("MaskControlShow");
        MaskHideAnim = (Storyboard)FindResource("MaskControlHide");
    }

    public void Show(double fadeIn = -1) {
        if (fadeIn >= 0) {
            MaskShowAnim.Children[0].Duration = TimeSpan.FromSeconds(fadeIn);
        }
        MaskShowAnim.Begin();
        IsHitTestVisible = true;
        Visibility = Visibility.Visible;
    }
    public void Hide(double fadeIn = -1) {
        onHiddenEventFunc();
        int interal = 300;
        if (fadeIn >= 0) {
            MaskHideAnim.Children[0].Duration = TimeSpan.FromSeconds(fadeIn);
            interal = (int)(fadeIn * 1000);
        }
        MaskHideAnim.Begin();
        IsHitTestVisible = false;
        if (HideTimer != null) {
            HideTimer = new Timer((s) => {
                Dispatcher.Invoke(() => {
                    Visibility = Visibility.Collapsed;
                    
                    HideTimer.Dispose();
                });
            }, null, interal, 0);
        }
        
    }

    private void Mask_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        Hide();
    }
}