using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StarFallMC.Util;

namespace StarFallMC.Component;

public class Button : ButtonBase {
    
    private Border _container;

    private ColorAnimation EnterAnim;
    private ColorAnimation LeaveAnim;
    private DoubleAnimation DownAnim;
    private DoubleAnimation UpAnim;
    
    public override void OnApplyTemplate() {
        base.OnApplyTemplate();
        InitElement();
        InitColor();
        InitAnimation();
        ThemeUtil.updateColor += () => {
            Dispatcher.BeginInvoke(() => {
                InitColor();
                InitAnimation();
            });
        };
    }

    public void InitElement() {
        _container = (Border)Template.FindName("Container", this);
    }
    
    public void InitColor() {
        _container.Background = ThemeUtil.SecondaryBrush.Clone();
    }

    public void InitAnimation() {
        EnterAnim = new () {
            To = ThemeUtil.SecondaryBrush_1.Color,
            Duration = TimeSpan.FromMilliseconds(150)
        };
        LeaveAnim = new () {
            To = ThemeUtil.SecondaryBrush.Color,
            Duration = TimeSpan.FromMilliseconds(150)
        };
    }
    
    protected override void OnMouseEnter(MouseEventArgs e) {
        _container.Background.BeginAnimation(SolidColorBrush.ColorProperty, EnterAnim);
        
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(MouseEventArgs e) {
        _container.Background.BeginAnimation(SolidColorBrush.ColorProperty, LeaveAnim);
        
        base.OnMouseLeave(e);
    }
}