using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StarFallMC.Util;

namespace StarFallMC.Component;

public class TextInput : TextBox {
    
    public enum Type {
        TextLine,
        TextArea
    }

    public Type InputType {
        get => (Type)GetValue(InputTypeProperty);
        set => SetValue(InputTypeProperty, value);
    }
    public static readonly DependencyProperty InputTypeProperty = DependencyProperty.Register(nameof(InputType),
        typeof(Type), typeof(TextInput), new PropertyMetadata(Type.TextLine));

    public bool AutoLostFocus {
        get => (bool)GetValue(AutoLostFocusProperty);
        set => SetValue(AutoLostFocusProperty, value);
    }
    public static readonly DependencyProperty AutoLostFocusProperty =
        DependencyProperty.RegisterAttached(nameof(AutoLostFocus), typeof(bool), typeof(TextInput),
            new PropertyMetadata(true));

    private Border border;

    private ColorAnimation EnterAnim;
    private ColorAnimation LeaveAnim;

    public override void OnApplyTemplate() {
        base.OnApplyTemplate();
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        TextWrapping = TextWrapping.NoWrap;
        AcceptsReturn = false;
        if (InputType == Type.TextArea) {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            TextWrapping = TextWrapping.Wrap;
            AcceptsReturn = true;
            Padding = new Thickness(2);
        }
        border = Template.FindName("Border", this) as Border;
        initColor();
        initAnimation();
        ThemeUtil.updateColor += () => {
            Dispatcher.BeginInvoke(() => {
                initColor();
                initAnimation();
            });
        };
    }

    private void initColor() {
        border.BorderBrush = ThemeUtil.SecondaryBrush.Clone();
        border.Background = ThemeUtil.ToSolidColorBrush("#f1f1f1");
    }

    private void initAnimation() {
        EnterAnim = new() {
            To = ThemeUtil.PrimaryBrush_3.Color,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase(),
            FillBehavior = FillBehavior.HoldEnd
        };
        LeaveAnim = new() {
            To = ThemeUtil.SecondaryBrush.Color,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase(),
            FillBehavior = FillBehavior.HoldEnd
        };
    }
    
    protected override void OnMouseEnter(MouseEventArgs e) {
        base.OnMouseEnter(e);
        border.BorderBrush.BeginAnimation(SolidColorBrush.ColorProperty,EnterAnim);
    }

    protected override void OnMouseLeave(MouseEventArgs e) {
        base.OnMouseLeave(e);
        if (!IsFocused) {
            border.BorderBrush.BeginAnimation(SolidColorBrush.ColorProperty,LeaveAnim);
        }
    }

    protected override void OnGotFocus(RoutedEventArgs e) {
        base.OnGotFocus(e);
        if (AutoLostFocus) {
            Window.GetWindow(this).PreviewMouseDown += WindowPreviewMouseDown;
        }
    }

    protected override void OnLostFocus(RoutedEventArgs e) {
        base.OnLostFocus(e);
        if (AutoLostFocus) {
            var window = Window.GetWindow(this);
            if (window != null) {
                window.PreviewMouseDown -= WindowPreviewMouseDown;
            }
        }
        border.BorderBrush.BeginAnimation(SolidColorBrush.ColorProperty, LeaveAnim);
    }
    
    private static void WindowPreviewMouseDown(object sender, MouseButtonEventArgs e) {
        var window = sender as Window;
        var originalSource = e.OriginalSource as DependencyObject;
        var isClickOnTextBox = IsChildOfTextBox(originalSource);
        if (!isClickOnTextBox) {
            Keyboard.ClearFocus();
            FocusManager.SetFocusedElement(window, null);
        }
    }

    private static bool IsChildOfTextBox(DependencyObject ele) {
        try {
            while (ele != null) {
                if (ele is TextInput) {
                    return true;
                }

                ele = VisualTreeHelper.GetParent(ele);
            }
        }
        catch (Exception e){ }
        return false;
    }
}