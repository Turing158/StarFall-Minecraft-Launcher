using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StarFallMC.Util;

namespace StarFallMC.Component;

public class PlainButton : System.Windows.Controls.Button {
    
    private Border _container;
    private ContentPresenter _contentPresenter;

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
        _contentPresenter = (ContentPresenter)Template.FindName("ContentPresenter", this);
        if (Content is string or TextBlock) {
            TextBlock textBlock;
            if (Content is string) {
                textBlock = new TextBlock {
                    Text = (string)Content,
                };
            }
            else {
                textBlock = (TextBlock)Content;
                
            }
            Binding forceBinding = new () {
                Path = new PropertyPath("Foreground"),
                Source = this
            };
            BindingOperations.SetBinding(textBlock, TextBlock.ForegroundProperty, forceBinding);
            _contentPresenter.Content = textBlock;
        }
    }
    
    public void InitColor() {
        Foreground = ThemeUtil.SecondaryBrush;
        _container.Background = ThemeUtil.ToSolidColorBrush("#f1f1f1");
    }

    public void InitAnimation() {
        EnterAnim = new () {
            To = ThemeUtil.SecondaryBrush.Color,
            Duration = TimeSpan.FromMilliseconds(150)
        };
        LeaveAnim = new () {
            To = ThemeUtil.ToColor("#f1f1f1"),
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
        Foreground = ThemeUtil.PrimaryBrush;
        _container.Background.BeginAnimation(SolidColorBrush.ColorProperty, EnterAnim);
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(MouseEventArgs e) {
        Foreground = ThemeUtil.SecondaryBrush_2;
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