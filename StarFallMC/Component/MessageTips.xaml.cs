using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using StarFallMC.Util;

namespace StarFallMC.Component;

public partial class MessageTips : UserControl ,INotifyPropertyChanged{
    public enum MessageType {
        None,
        Warning,
        Error
    }
    
    public Storyboard ShowAnim { get; private set; }
    private Storyboard HideAnim;
    private Storyboard MouseDownAnim;
    private Storyboard MouseUpAnim;
    private Storyboard ChangeTextAnim;
    
    private DispatcherTimer? HideTimer;
    private DispatcherTimer? DeleteTimer;
    private DispatcherTimer? MessageTimer;
    private string pendingMessage = string.Empty;
    private MessageType pendingMessageType;

    private bool isClosing = false;
    
    private string _message;
    public string Message {
        get => _message;
        set => SetField(ref _message, value);
    }

    private string _messageColor;
    public string MessageColor {
        get => _messageColor;
        set => SetField(ref _messageColor, value);
    }
    

    
    public MessageTips(string message, MessageType messageType) {
        InitializeComponent();
        ShowAnim = FindResource("ShowAnim") as Storyboard;
        HideAnim = FindResource("HideAnim") as Storyboard;
        MouseDownAnim = FindResource("MouseDownAnim") as Storyboard;
        MouseUpAnim = FindResource("MouseUpAnim") as Storyboard;
        ChangeTextAnim = FindResource("ChangeTextAnim") as Storyboard;
        
        DataContext = this;
        
        initColor();
        ThemeUtil.AddColorChangedHandler(OnThemeColorChanged);
        Unloaded += OnUnloaded;
        Main.Width = 0;
        Main.Height = 0;
        SetMessage(message,messageType);
        TextColorChange(messageType);
        Hide();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) {
        StopAllTimers();
        ThemeUtil.RemoveColorChangedHandler(OnThemeColorChanged);
    }

    private void initColor() {
        MessageColor = ThemeUtil.SecondaryBrush_1.Color.ToString();
    }

    private void OnThemeColorChanged(object? sender, EventArgs e) => initColor();

    public void SetMessage(string message, MessageType messageType) {
        ChangeTextAnim.Begin(this, true);
        StopMessageTimer();
        pendingMessage = message;
        pendingMessageType = messageType;
        HideContent.Text = message;
        MessageTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        MessageTimer.Tick += MessageTimer_OnTick;
        MessageTimer.Start();
    }

    public static void Show(string message, MessageType messageType = MessageType.None) {
        Application.Current.Dispatcher.BeginInvoke(() => {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null && mainWindow.Content is Grid gird) {
                var container = gird.Children.OfType<Grid>().FirstOrDefault(i => i.Name == "MessageTipContainer")
                    ?.Children;
                var first = container.OfType<MessageTips>().FirstOrDefault();
                if (container.Count == 1 && !first.isClosing) {
                    first.Hide();
                    if (first.Message != message) {
                        first.SetMessage(message, messageType);
                    }
                }
                else if (container.Count == 0 ||
                         (container.Count == 1 && container.OfType<MessageTips>().FirstOrDefault().isClosing)) {
                    var messageTips = new MessageTips(message, messageType);
                    container.Add(messageTips);
                    messageTips.ShowAnim.Begin(messageTips, true);
                }
            }
        });
    }

    public void Hide() {
        StopHideTimer();
        StopDeleteTimer();
        HideTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        HideTimer.Tick += HideTimer_OnTick;
        HideTimer.Start();
    }

    public void HideImmediately() {
        StopHideTimer();
        StopDeleteTimer();
        isClosing = true;
        HideAnim.Begin(this, true);
        DeleteTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        DeleteTimer.Tick += DeleteTimer_OnTick;
        DeleteTimer.Start();
    }

    public void TextColorChange(MessageType messageType) {
        switch (messageType) {
            case MessageType.Warning :
                MessageColor = "#9F7D00";
                break;
            case MessageType.Error :
                MessageColor = "DarkRed";
                break;
            default:
                MessageColor = ThemeUtil.SecondaryBrush_1.Color.ToString();
                break;
        }
    }

    private void Main_OnMouseEnter(object sender, MouseEventArgs e) {
        if (!isClosing) {
            StopHideTimer();
            StopDeleteTimer();
        }
    }

    private void Main_OnMouseLeave(object sender, MouseEventArgs e) {
        isDown = false;
        if (isClosing) {
            return;
        }
        MouseUpAnim.Begin(this, true);
        Hide();
    }

    private bool isDown = false;
    private void Main_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        if (!isClosing) {
            MouseDownAnim.Begin(this, true);
            isDown = true;
        }
    }

    private void Main_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
        if (isDown) {
            MouseUpAnim.Begin(this, true);
            HideImmediately();
        }
        isDown = false;
    }

    private void MessageTimer_OnTick(object? sender, EventArgs e) {
        StopMessageTimer();
        Message = pendingMessage;
        TextColorChange(pendingMessageType);
    }

    private void HideTimer_OnTick(object? sender, EventArgs e) {
        StopHideTimer();
        HideImmediately();
    }

    private void DeleteTimer_OnTick(object? sender, EventArgs e) {
        StopDeleteTimer();
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow != null && mainWindow.Content is Grid grid) {
            grid.Children.OfType<Grid>().FirstOrDefault(i => i.Name == "MessageTipContainer")?.Children.Remove(this);
        }
    }

    private void StopAllTimers() {
        StopHideTimer();
        StopDeleteTimer();
        StopMessageTimer();
    }

    private void StopHideTimer() {
        if (HideTimer == null) return;
        HideTimer.Stop();
        HideTimer.Tick -= HideTimer_OnTick;
        HideTimer = null;
    }

    private void StopDeleteTimer() {
        if (DeleteTimer == null) return;
        DeleteTimer.Stop();
        DeleteTimer.Tick -= DeleteTimer_OnTick;
        DeleteTimer = null;
    }

    private void StopMessageTimer() {
        if (MessageTimer == null) return;
        MessageTimer.Stop();
        MessageTimer.Tick -= MessageTimer_OnTick;
        MessageTimer = null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void HideContent_OnSizeChanged(object sender, SizeChangedEventArgs e) {
        var width = HideContent.ActualWidth + 30;
        var height = HideContent.ActualHeight + 20;
        if (!width.Equals(Main.Width)) {
            var widthAnim = new DoubleAnimation {
                To = width,
                Duration = new Duration(TimeSpan.FromSeconds(0.1)),
                EasingFunction = new CubicEase()
            };
            Main.BeginAnimation(WidthProperty, widthAnim);
        }
        if (!height.Equals(Main.Height)) {
            var heightAnim = new DoubleAnimation {
                To = height,
                Duration = new Duration(TimeSpan.FromSeconds(0.1)),
                EasingFunction = new CubicEase()
            };
            Main.BeginAnimation(HeightProperty, heightAnim);
        }
    }
}
