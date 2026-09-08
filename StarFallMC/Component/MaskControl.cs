using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace StarFallMC.Component;

public class MaskControl : UserControl {

    public bool ClickMaskToClose {
        get => (bool)GetValue(ClickMaskToCloseProperty);
        set => SetValue(ClickMaskToCloseProperty, value);
    }
    public static readonly DependencyProperty ClickMaskToCloseProperty = DependencyProperty.Register(nameof(ClickMaskToClose),
        typeof(bool), typeof(MaskControl), new PropertyMetadata(true));
    
    public event RoutedEventHandler ClickMask {
        add => AddHandler(OnHiddenEvent, value);
        remove => RemoveHandler(OnHiddenEvent, value);
    }
    public static readonly RoutedEvent ClickMaskEvent = EventManager.RegisterRoutedEvent(nameof(ClickMask),
        RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MaskControl));
    
    public Brush MaskColor {
        get => (Brush)GetValue(MaskColorProperty);
        set => SetValue(MaskColorProperty, value);
    }
    public static readonly DependencyProperty MaskColorProperty = DependencyProperty.Register(nameof(MaskColor),
        typeof(Brush), typeof(MaskControl), new PropertyMetadata(Brushes.Black));

    public double MaskOpacity {
        get => (double)GetValue(MaskOpacityProperty);
        set => SetValue(MaskOpacityProperty, value);
    }
    public static readonly DependencyProperty MaskOpacityProperty = DependencyProperty.Register(nameof(MaskOpacity),
        typeof(double), typeof(MaskControl), new PropertyMetadata(0.3)); 
    
    public event RoutedEventHandler OnHidden {
        add => AddHandler(OnHiddenEvent, value);
        remove => RemoveHandler(OnHiddenEvent, value);
    }
    public static readonly RoutedEvent OnHiddenEvent = EventManager.RegisterRoutedEvent(nameof(OnHidden),
        RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MaskControl));

    private Border _mask;

    private DoubleAnimation MaskShowAnim;
    private DoubleAnimation MaskHideAnim;

    public MaskControl() {
        Opacity = 0;
        Visibility = Visibility.Collapsed;
        
        MaskShowAnim = new() {
            To = 1,
            Duration = TimeSpan.FromSeconds(0.15)
        };
        MaskHideAnim = new() {
            To = 0,
            Duration = TimeSpan.FromSeconds(0.2)
        };
        MaskHideAnim.Completed += MaskHideAnim_OnCompleted;
        Unloaded += MaskControl_OnUnloaded;
    }

    public override void OnApplyTemplate() {
        base.OnApplyTemplate();
        if (_mask != null) {
            _mask.MouseLeftButtonDown -= Mask_OnMouseLeftButtonDown;
        }
        _mask = Template.FindName("Mask", this) as Border;
        if (_mask != null) {
            _mask.MouseLeftButtonDown += Mask_OnMouseLeftButtonDown;
        }
    }

    public void Show(double fadeIn = -1) {
        if (fadeIn >= 0) {
            MaskShowAnim.Duration = TimeSpan.FromSeconds(fadeIn);
        }
        Visibility = Visibility.Visible;
        BeginAnimation(OpacityProperty, MaskShowAnim);
        IsHitTestVisible = true;
    }
    
    public void Hide(double fadeIn = -1) {
        RaiseEvent(new RoutedEventArgs(OnHiddenEvent));
        if (fadeIn >= 0) {
            MaskHideAnim.Duration = TimeSpan.FromSeconds(fadeIn);
        }
        BeginAnimation(OpacityProperty, MaskHideAnim);
        IsHitTestVisible = false;
    }

    private void MaskHideAnim_OnCompleted(object? sender, EventArgs e) {
        if (!IsHitTestVisible) {
            Visibility = Visibility.Collapsed;
        }
    }

    private void MaskControl_OnUnloaded(object sender, RoutedEventArgs e) {
        BeginAnimation(OpacityProperty, null);
    }

    private void Mask_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        if (ClickMaskToClose) {
            Hide();
        }
        RaiseEvent(new RoutedEventArgs(ClickMaskEvent));
    }
}
