using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StarFallMC.Util;

namespace StarFallMC.Component;

public class TextButton : ButtonBase {
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
    }

    protected override void OnMouseEnter(MouseEventArgs e) {
        base.OnMouseEnter(e);
        _textContent.Foreground.BeginAnimation(SolidColorBrush.ColorProperty, HoverAnimation);
    }

    protected override void OnMouseLeave(MouseEventArgs e) {
        base.OnMouseLeave(e);
        _textContent.Foreground.BeginAnimation(SolidColorBrush.ColorProperty, UnhoverAnimation);
    }
}