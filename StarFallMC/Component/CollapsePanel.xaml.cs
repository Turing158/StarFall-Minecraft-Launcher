using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace StarFallMC.Component;

public partial class CollapsePanel : UserControl {
    
    public double ToggleBtnSize {
        get => (double)GetValue(ToggleBtnSizeProperty);
        set => SetValue(ToggleBtnSizeProperty, value);
    }
    
    public static readonly DependencyProperty ToggleBtnSizeProperty = DependencyProperty.Register(nameof(ToggleBtnSize),
        typeof(double), typeof(CollapsePanel), new PropertyMetadata(30.0));
    
    public object TitleContent {
        get => GetValue(TitleContentProperty);
        set => SetValue(TitleContentProperty, value);
    }
    
    public static readonly DependencyProperty TitleContentProperty = DependencyProperty.Register(nameof(TitleContent), typeof(object),
        typeof(CollapsePanel), new PropertyMetadata(null));

    public Visibility NeedDivider {
        get => (Visibility)GetValue(NeedDividerProperty);
        set => SetValue(NeedDividerProperty, value);
    }
    
    public static readonly DependencyProperty NeedDividerProperty = DependencyProperty.Register(nameof(NeedDivider),
        typeof(Visibility), typeof(CollapsePanel), new PropertyMetadata(Visibility.Visible));

    
    public bool IsOpen {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }
    
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen),
        typeof(bool), typeof(CollapsePanel), new PropertyMetadata(false));

    public double MainHeight {
        get => (double)GetValue(MainHeightProperty);
        set => SetValue(MainHeightProperty, value);
    }
    
    public static readonly DependencyProperty MainHeightProperty = DependencyProperty.Register(nameof(MainHeight),
        typeof(double), typeof(CollapsePanel), new PropertyMetadata(45.0));
    
    private Border Main;
    private StackPanel EntirePanel;
    private TextBlock StateIcon;
    
    private Storyboard OpenAnim;
    private Storyboard CloseAnim;
    private Storyboard MouseDownAnim;
    private Storyboard MouseUpAnim;
    
    public CollapsePanel() {
        InitializeComponent();
    }
    
    public override void OnApplyTemplate() {
        base.OnApplyTemplate();
        Main = (Border)Template.FindName("Main",this);
        EntirePanel = (StackPanel)Template.FindName("EntirePanel",this);
        StateIcon = (TextBlock)Template.FindName("StateIcon",this);
        
    }
    private void CollapsePanel_OnLoaded(object sender, RoutedEventArgs e) {
        OpenAnim = (FindResource("OpenAnim") as Storyboard).Clone();
        OpenAnim.Children[0].SetValue(Storyboard.TargetProperty, StateIcon);
        CloseAnim = (FindResource("CloseAnim") as Storyboard).Clone();
        CloseAnim.Children[0].SetValue(Storyboard.TargetProperty, Main);
        CloseAnim.Children[1].SetValue(Storyboard.TargetProperty, StateIcon);
        MouseDownAnim = (FindResource("MouseDownAnim") as Storyboard).Clone();
        Storyboard.SetTarget(MouseDownAnim,Main);
        MouseUpAnim = (FindResource("MouseUpAnim") as Storyboard).Clone();
        Storyboard.SetTarget(MouseUpAnim,Main);
    }
    
    private bool isMouseLeftDown = false;
    private void Top_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        isMouseLeftDown = true;
        MouseDownAnim.Begin();
    }

    private void Top_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
        if (isMouseLeftDown) {
            isMouseLeftDown = false;
            IsOpen = !IsOpen;
            if (IsOpen) {
                if (this.Content != null) {
                    var openAnim = new DoubleAnimation(
                        toValue:EntirePanel.ActualHeight,
                        duration:new Duration(TimeSpan.FromMilliseconds(200))
                    );
                    openAnim.EasingFunction = new CubicEase();
                    Main.BeginAnimation(HeightProperty,openAnim);
                }
                OpenAnim.Begin();
            }
            else {
                CloseAnim.Begin();
            }
            MouseUpAnim.Begin();
        }
    }

    private void Top_OnMouseLeave(object sender, MouseEventArgs e) {
        if (isMouseLeftDown) {
            MouseUpAnim.Begin();
        }
        isMouseLeftDown = false;
    }
}