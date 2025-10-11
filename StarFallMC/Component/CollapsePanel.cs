using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace StarFallMC.Component;

public class CollapsePanel : UserControl{
        
    public double ToggleBtnSize {
        get => (double)GetValue(ToggleBtnSizeProperty);
        set => SetValue(ToggleBtnSizeProperty, value);
    }
    public static readonly DependencyProperty ToggleBtnSizeProperty = DependencyProperty.Register(nameof(ToggleBtnSize),
        typeof(double), typeof(CollapsePanel), new PropertyMetadata(30.0));
    
    public Object TitleContent {
        get => GetValue(TitleContentProperty);
        set => SetValue(TitleContentProperty, value);
    }
    public static readonly DependencyProperty TitleContentProperty = DependencyProperty.Register(nameof(TitleContent), typeof(Object),
        typeof(CollapsePanel), new PropertyMetadata(null));

    public Object TitleOperateContent {
        get => GetValue(TitleOperateContentProperty);
        set => SetValue(TitleOperateContentProperty, value);
    }
    public static readonly DependencyProperty TitleOperateContentProperty = DependencyProperty.Register(nameof(TitleOperateContent), typeof(Object),
        typeof(CollapsePanel), new PropertyMetadata(null));

    public Visibility NeedDivider {
        get => (Visibility)GetValue(NeedDividerProperty);
        set => SetValue(NeedDividerProperty, value);
    }
    public static readonly DependencyProperty NeedDividerProperty = DependencyProperty.Register(nameof(NeedDivider),
        typeof(Visibility), typeof(CollapsePanel), new PropertyMetadata(Visibility.Visible));

    public Object DisabledContent {
        get => GetValue(DisabledContentProperty);
        set => SetValue(DisabledContentProperty, value);
    }
    public static readonly DependencyProperty DisabledContentProperty = DependencyProperty.Register(nameof(DisabledContent), typeof(Object),
        typeof(CollapsePanel), new PropertyMetadata(null));
    
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
    
    public event RoutedEventHandler Opened {
        add => AddHandler(OpenedEvent, value);
        remove => RemoveHandler(OpenedEvent, value);
    }
    public static readonly RoutedEvent OpenedEvent = EventManager.RegisterRoutedEvent(
        nameof(Opened), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(CollapsePanel));
    
    public event RoutedEventHandler Closed {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }
    public static readonly RoutedEvent ClosedEvent = EventManager.RegisterRoutedEvent(
        nameof(Closed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(CollapsePanel));
    
    private Border _main;
    private StackPanel _entirePanel;
    private TextBlock _stateIcon;
    private Border _triggerBorder;
    private ContentPresenter _contentPresenter;
    
    private DoubleAnimation OpenHeightAnim;
    
    private Storyboard OpenAnim;
    private Storyboard CloseAnim;
    private Storyboard MouseDownAnim;
    private Storyboard MouseUpAnim;
    
    public CollapsePanel() {
        Loaded += CollapsePanel_OnLoaded;
        IsEnabledChanged += OnIsEnabledChanged;
    }

    private void OnIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e) {
        Hide();
    }

    public override void OnApplyTemplate() {
        base.OnApplyTemplate();
        _main = (Border)Template.FindName("Main",this);
        _entirePanel = (StackPanel)Template.FindName("EntirePanel",this);
        _stateIcon = (TextBlock)Template.FindName("StateIcon",this);
        _triggerBorder = (Border)Template.FindName("TriggerBorder",this);
        _contentPresenter = (ContentPresenter)Template.FindName("ContentPresenter",this);

        _triggerBorder.MouseLeave += Top_OnMouseLeave;
        _triggerBorder.MouseLeftButtonDown += Top_OnMouseLeftButtonDown;
        _triggerBorder.MouseLeftButtonUp += Top_OnMouseLeftButtonUp;

        _contentPresenter.SizeChanged += Content_OnSizeChanged;

        OpenHeightAnim = new DoubleAnimation {
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new CubicEase()
        };
    }
    
    private void CollapsePanel_OnLoaded(object sender, RoutedEventArgs e) {
        OpenAnim = (FindResource("OpenAnim") as Storyboard).Clone();
        OpenAnim.Children[0].SetValue(Storyboard.TargetProperty, _stateIcon);
        CloseAnim = (FindResource("CloseAnim") as Storyboard).Clone();
        CloseAnim.Children[0].SetValue(Storyboard.TargetProperty, _main);
        CloseAnim.Children[1].SetValue(Storyboard.TargetProperty, _stateIcon);
        MouseDownAnim = (FindResource("MouseDownAnim") as Storyboard).Clone();
        Storyboard.SetTarget(MouseDownAnim,_main);
        MouseUpAnim = (FindResource("MouseUpAnim") as Storyboard).Clone();
        Storyboard.SetTarget(MouseUpAnim,_main);
    }
    
    private bool isMouseLeftDown = false;
    private void Top_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        isMouseLeftDown = true;
        MouseDownAnim.Begin();
    }

    private void Top_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
        if (isMouseLeftDown) {
            isMouseLeftDown = false;
            if (IsOpen) {
                Hide();
            }
            else {
                Show();
            }
            MouseUpAnim.Begin();
        }
    }

    public void Show() {
        RaiseEvent(new RoutedEventArgs(OpenedEvent));
        IsOpen = true;
        if (Content != null) {
            var openAnim = new DoubleAnimation(
                toValue:_entirePanel.ActualHeight,
                duration:new Duration(TimeSpan.FromMilliseconds(200))
            );
            openAnim.EasingFunction = new CubicEase();
            _main.BeginAnimation(HeightProperty,openAnim);
        }
        OpenAnim.Begin();
    }

    public void Hide() {
        RaiseEvent(new RoutedEventArgs(ClosedEvent));
        IsOpen = false;
        CloseAnim.Begin();
    }

    private void Top_OnMouseLeave(object sender, MouseEventArgs e) {
        if (isMouseLeftDown) {
            MouseUpAnim.Begin();
        }
        isMouseLeftDown = false;
    }
    
    private bool isSizeChanging;
    private void Content_OnSizeChanged(object sender, SizeChangedEventArgs e) {
        if (!isSizeChanging && IsOpen && Content != null) {
            var content = sender as ContentPresenter;
            isSizeChanging = true;
            Dispatcher.BeginInvoke(() => {
                OpenHeightAnim.To = content.ActualHeight + MainHeight + 1;
                OpenHeightAnim.Completed += (o, args) => {
                    isSizeChanging = false;
                };
                _main.BeginAnimation(HeightProperty,OpenHeightAnim);
            });
        }
    }
    
}