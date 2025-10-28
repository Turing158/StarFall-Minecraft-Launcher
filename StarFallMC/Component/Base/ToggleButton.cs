using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StarFallMC.Util;

namespace StarFallMC.Component;

public class ToggleButton : System.Windows.Controls.Primitives.ToggleButton {

    public Object LeftContent {
        get => GetValue(LeftContentProperty);
        set => SetValue(LeftContentProperty, value);
    }
    public static readonly DependencyProperty LeftContentProperty = DependencyProperty.Register(nameof(LeftContent),
        typeof(Object), typeof(ToggleButton), new PropertyMetadata(null));

    public Object RightContent {
        get => GetValue(RightContentProperty);
        set => SetValue(RightContentProperty, value);
    }
    public static readonly DependencyProperty RightContentProperty = DependencyProperty.Register(nameof(RightContent),
        typeof(Object), typeof(ToggleButton), new PropertyMetadata(null));

    public Thickness CornerRadius {
        get => (Thickness)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(nameof(CornerRadius),
        typeof(Thickness), typeof(ToggleButton), new PropertyMetadata(new Thickness(5)));
    
    public bool useAutoForceground {
        get => (bool)GetValue(useAutoForcegroundProperty);
        set => SetValue(useAutoForcegroundProperty, value);
    }
    public static readonly DependencyProperty useAutoForcegroundProperty = DependencyProperty.Register(nameof(useAutoForceground),
        typeof(bool), typeof(ToggleButton), new PropertyMetadata(true));

    private Grid _contentGrid;
    private Border _active;
    private Border _leftPresenter;
    private Border _rightPresenter;
    private Border _leftMask;
    private Border _rightMask;

    private DoubleAnimation ScaleAnim;
    private DoubleAnimation ToOriginAnim;

    public ToggleButton() {
        
    }
    
    public override void OnApplyTemplate() {
        base.OnApplyTemplate();
        initAnimation();
        _contentGrid = Template.FindName("ContentGrid", this) as Grid;
        _active = Template.FindName("Active", this) as Border;
        _leftPresenter = Template.FindName("LeftPresenter", this) as Border;
        _rightPresenter = Template.FindName("RightPresenter", this) as Border;
        _leftMask = Template.FindName("LeftMask", this) as Border;
        _rightMask = Template.FindName("RightMask", this) as Border;

        RenderTransform = new ScaleTransform();
        _active.RenderTransform = new TranslateTransform();
        _leftMask.RenderTransform = new TranslateTransform();
        _rightMask.RenderTransform = new TranslateTransform();
        Loaded += (sender, args) => {
            initColor();
            _leftMask.Width = _leftPresenter.ActualWidth;
            _leftMask.Height = _leftPresenter.ActualHeight;
            _rightMask.Width = _rightPresenter.ActualWidth;
            _rightMask.Height = _rightPresenter.ActualHeight;
            ThemeUtil.updateColor += initColor;
            if (IsChecked.Value) {
                moveRight();
            }
            else {
                moveLeft();
            }
        };
    }

    private void initColor() {
        if (useAutoForceground && (_leftPresenter.Child as ContentPresenter).Content is TextBlock leftContentTextBlock) {
            leftContentTextBlock.Foreground = ThemeUtil.SecondaryBrush;
            
        }
        if (useAutoForceground && (_rightPresenter.Child as ContentPresenter).Content is TextBlock rightContentTextBlock) {
            rightContentTextBlock.Foreground = ThemeUtil.SecondaryBrush;
        }
    }
    
    private void initAnimation(){
        ScaleAnim = new () {
            To = 0.95,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase()
        };
        ToOriginAnim = new () {
            To = 1,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase()
        };
    }

    protected override void OnClick() {
        base.OnClick();
        if (IsChecked.Value) {
            moveRight();
        }
        else {
            moveLeft();
        }
    }

    private void moveLeft() {
        _active.Width = _leftPresenter.ActualWidth;
        _active.Height = _leftPresenter.ActualHeight;
        Point activePoint = _leftPresenter.TranslatePoint(new Point(0, 0),_contentGrid);
        var doubleAnimation = new DoubleAnimation {
            To = activePoint.X,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase()
        };
        _active.RenderTransform.BeginAnimation(TranslateTransform.XProperty,doubleAnimation);
        if (double.IsNaN(_leftMask.Width)) {
            doubleAnimation.To = 0;
        }
        else {
            doubleAnimation.To = -_leftMask.Width;
        }
        
        _leftMask.RenderTransform.BeginAnimation(TranslateTransform.XProperty, doubleAnimation);
        doubleAnimation.To = 0;
        _rightMask.RenderTransform.BeginAnimation(TranslateTransform.XProperty, doubleAnimation);
    }
    
    private void moveRight() {
        _active.Width = _rightPresenter.ActualWidth;
        _active.Height = _rightPresenter.ActualHeight;
        Point activePoint = _rightPresenter.TranslatePoint(new Point(0, 0),_contentGrid);
        var doubleAnimation = new DoubleAnimation {
            To = activePoint.X,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase()
        };
        _active.RenderTransform.BeginAnimation(TranslateTransform.XProperty,doubleAnimation);
        if (double.IsNaN(_rightMask.Width)) {
            doubleAnimation.To = 0;
        }
        else {
            doubleAnimation.To = _rightMask.Width;
        }
        _rightMask.RenderTransform.BeginAnimation(TranslateTransform.XProperty, doubleAnimation);
        doubleAnimation.To = 0;
        _leftMask.RenderTransform.BeginAnimation(TranslateTransform.XProperty, doubleAnimation);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
        base.OnMouseLeftButtonDown(e);
        RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, ScaleAnim);
        RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, ScaleAnim);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
        base.OnMouseLeftButtonUp(e);
        RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, ToOriginAnim);
        RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, ToOriginAnim);
    }
    
}