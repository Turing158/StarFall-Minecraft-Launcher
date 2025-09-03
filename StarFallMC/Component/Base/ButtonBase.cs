using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace StarFallMC.Component;

public class ButtonBase : System.Windows.Controls.Button{
    
    private DoubleAnimation DownAnim;
    private DoubleAnimation UpAnim;
    public ButtonBase() {
        RenderTransform = new ScaleTransform();
        RenderTransformOrigin = new (0.5, 0.5);
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
    
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
        RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, DownAnim);
        RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, DownAnim);
        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
        RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, UpAnim);
        RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, UpAnim);
        base.OnMouseLeftButtonUp(e);
    }
}