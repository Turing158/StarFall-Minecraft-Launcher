using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StarFallMC.Util;

namespace StarFallMC.Component;

public class Slider : System.Windows.Controls.Slider{

    public double ValueFontSize {
        get => (double)GetValue(ValueFontSizeProperty);
        set => SetValue(ValueFontSizeProperty, value);
    }
    public static readonly DependencyProperty ValueFontSizeProperty = DependencyProperty.Register(
        nameof(ValueFontSize), typeof(double), typeof(Slider), new PropertyMetadata(12.0));

    public string MinimumText {
        get => (string)GetValue(MinimumTextProperty);
        set => SetValue(MinimumTextProperty, value);
    }
    public static readonly DependencyProperty MinimumTextProperty = DependencyProperty.Register(
        nameof(MinimumText), typeof(string), typeof(Slider), new PropertyMetadata(string.Empty));
    
    public string ValueText {
        get => (string)GetValue(ValueTextProperty);
        set => SetValue(ValueTextProperty, value);
    }
    public static readonly DependencyProperty ValueTextProperty = DependencyProperty.Register(
        nameof(ValueText), typeof(string), typeof(Slider), new PropertyMetadata(string.Empty));
    
    public string MaximumText {
        get => (string)GetValue(MaximumTextProperty);
        set => SetValue(MaximumTextProperty, value);
    }
    public static readonly DependencyProperty MaximumTextProperty = DependencyProperty.Register(
        nameof(MaximumText), typeof(string), typeof(Slider), new PropertyMetadata(string.Empty));
    
    private Border _border;
    
    private ColorAnimation EnableAnimation;
    private ColorAnimation DisableAnimation;
    private DoubleAnimation ValueAnimation;
    
    public override void OnApplyTemplate() {
        base.OnApplyTemplate();
        _border = Template.FindName("Border", this) as Border;
        initColor();
        initAnimation();
        ThemeUtil.updateColor += () => {
            initColor();
            initAnimation();
        };
        IsEnabledChanged += (sender, args) => {
            if (IsEnabled) {
                _border.Background.BeginAnimation(SolidColorBrush.ColorProperty, EnableAnimation);
            }
            else {
                _border.Background.BeginAnimation(SolidColorBrush.ColorProperty, DisableAnimation);
            }
        };
    }
    
    private void initColor(){
        if (IsEnabled) {
            _border.Background = ThemeUtil.PrimaryBrush_2.Clone();
        }
        else {
            _border.Background = ThemeUtil.SecondaryBrush_2.Clone();
        }
    }

    private void initAnimation() {
        EnableAnimation = new() {
            To = ThemeUtil.PrimaryBrush_2.Color,
            Duration = TimeSpan.FromMilliseconds(200),
        };
        DisableAnimation = new() {
            To = ThemeUtil.SecondaryBrush_2.Color,
            Duration = TimeSpan.FromMilliseconds(200),
        };
        if (ValueAnimation == null) {
            ValueAnimation = new() {
                Duration = TimeSpan.FromMilliseconds(100),
                EasingFunction = new CubicEase(),
            };
        }
    }
    
    private bool isDragging;
    private bool isAnimating;

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e) {
        base.OnPreviewMouseLeftButtonDown(e);
        AnimationFromValue(ValueFromPoint(e.GetPosition(this)));
        CaptureMouse();
        isDragging = true;
        e.Handled = true;
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e) {
        base.OnPreviewMouseMove(e);
        if (isDragging && e.LeftButton == MouseButtonState.Pressed) {
            BeginAnimation(ValueProperty, null);
            isAnimating = false;
            DirectionFromPoint(ValueFromPoint(e.GetPosition(this)));
        }
    }
    
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e) {
        base.OnPreviewMouseLeftButtonUp(e);
        if (isDragging) {
            DirectionFromPoint(ValueFromPoint(e.GetPosition(this)));
        }
        ReleaseMouseCapture();
        isDragging = false;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e) {
        base.OnLostMouseCapture(e);
        if (isDragging) {
            isDragging = false;
        }
    }
    
    private double ValueFromPoint(Point position) {
        double value = Orientation == Orientation.Horizontal ? 
            ((position.X / ActualWidth) * (Maximum - Minimum) + Minimum) : 
            ((position.Y / ActualHeight) * (Maximum - Minimum) + Minimum);
        return Math.Max(Minimum, Math.Min(Maximum, value));
    }

    protected override void OnValueChanged(double oldValue, double newValue) {
        base.OnValueChanged(oldValue, newValue);
        if (isDragging) {
            Value = newValue;
        }
    }

    private void DirectionFromPoint(double value) {
        Value = value;
    }
    
    private void AnimationFromValue(double value) {
        if (Math.Abs(Value - value) < 0.001) {
            return;
        }
        isAnimating = true;
        ValueAnimation.To = value;
        ValueAnimation.Completed += (sender, args) => {
            Value = value;
            isAnimating = false;
        };
        BeginAnimation(ValueProperty, ValueAnimation);
    }
}