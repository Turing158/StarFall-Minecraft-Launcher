using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StarFallMC.Util;

namespace StarFallMC.Component;

public class TextButton : System.Windows.Controls.Button{
    public Brush HoverForeground {
        get { return (Brush)GetValue(HoverForegroundProperty); }
        set { SetValue(HoverForegroundProperty, value); }
    }
    public static readonly DependencyProperty HoverForegroundProperty =
        DependencyProperty.Register(nameof(HoverForeground), typeof(Brush), typeof(TextButton), new PropertyMetadata(ThemeUtil.SecondaryBrush));
    
    private TextBlock _textContent;

    private ColorAnimation HoverAnimation;
    private ColorAnimation UnhoverAnimation;
    
    private DoubleAnimation PressAnimation;
    private DoubleAnimation UnpressAnimation;
    
    public override void OnApplyTemplate() {
        base.OnApplyTemplate();
        RenderTransform = new ScaleTransform();
        _textContent = Template.FindName("TextContent", this) as TextBlock;
        _textContent.Foreground = ThemeUtil.PrimaryBrush_4.Clone();
        initAnimation();
        ThemeUtil.updateColor += () => {
            initAnimation();
        };
    }

    public void initAnimation() {
        HoverAnimation = new ColorAnimation {
            To = ThemeUtil.SecondaryBrush.Color,
            Duration = TimeSpan.FromMilliseconds(200)
        };
        UnhoverAnimation = new ColorAnimation {
            To = ThemeUtil.PrimaryBrush_4.Color,
            Duration = TimeSpan.FromMilliseconds(200)
        };
        if (PressAnimation == null) {
            PressAnimation = new DoubleAnimation {
                To = 0.95,
                Duration = TimeSpan.FromMilliseconds(150)
            };
        }
        if (UnpressAnimation == null) {
            UnpressAnimation = new DoubleAnimation {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(150)
            };
        }
    }

    protected override void OnMouseEnter(MouseEventArgs e) {
        base.OnMouseEnter(e);
        _textContent.Foreground.BeginAnimation(SolidColorBrush.ColorProperty, HoverAnimation);
    }

    protected override void OnMouseLeave(MouseEventArgs e) {
        base.OnMouseLeave(e);
        _textContent.Foreground.BeginAnimation(SolidColorBrush.ColorProperty, UnhoverAnimation);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
        base.OnMouseLeftButtonDown(e);
        RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, PressAnimation);
        RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, PressAnimation);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
        base.OnMouseLeftButtonUp(e);
        RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, UnpressAnimation);
        RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, UnpressAnimation);
    }
}