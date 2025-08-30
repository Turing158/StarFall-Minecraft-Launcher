using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StarFallMC.Util;

namespace StarFallMC.Component;

public class Button : System.Windows.Controls.Button {
    
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
        _container.RenderTransform = new ScaleTransform();
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
        DownAnim = new () {
            To = 0.95,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase()
        };
        UpAnim = new () {
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase()
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
    
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
        _container.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, DownAnim);
        _container.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, DownAnim);
        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
        _container.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, UpAnim);
        _container.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, UpAnim);
        base.OnMouseLeftButtonUp(e);
    }
}