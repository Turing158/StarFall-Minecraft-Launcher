using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

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
    
    private Timer HideTimer;
    private Timer DeleteTimer;
    private Timer MessageTimer;
    private Timer SizeTimer;

    private bool isClosing = false;
    
    private string _message;
    public string Message {
        get => _message;
        set => SetField(ref _message, value);
    }

    private string _messageColor = "#001D1F";
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
        
        Message = message;
        
        SizeTimer?.Dispose();
        SizeTimer = new Timer(o => {
            this.Dispatcher.Invoke(() => {
                Main.Width = Content.ActualWidth + 30;
                Main.Height = Content.ActualHeight + 20;
                Console.WriteLine($"MessageTips Size: {Main.Width}x{Main.Height}");
                SizeTimer?.Dispose();
            });
        }, null, 50, 0);
        TextColorChange(messageType);
        Hide();
    }

    public void SetMessage(string message, MessageType messageType) {
        ChangeTextAnim.Begin();
        MessageTimer?.Dispose();
        MessageTimer = new Timer(o => {
            this.Dispatcher.BeginInvoke(() => {
                Message = message;
                TextColorChange(messageType);
                SizeTimer?.Dispose();
                SizeTimer = new Timer(s => {
                    this.Dispatcher.BeginInvoke(() => {
                        var width = Content.ActualWidth + 30;
                        var height = Content.ActualHeight + 20;
                        if (!width.Equals(Main.Width)) {
                            var widthAnim = new DoubleAnimation(
                                toValue:width,
                                duration:new Duration(TimeSpan.FromSeconds(0.1)));
                            widthAnim.EasingFunction = new CubicEase();
                            Main.BeginAnimation( WidthProperty, widthAnim);
                        }
                        if (!height.Equals(Main.Height)) {
                            var heightAnim = new DoubleAnimation(
                                toValue:height,
                                duration:new Duration(TimeSpan.FromSeconds(0.1)));
                            heightAnim.EasingFunction = new CubicEase();
                            Main.BeginAnimation( HeightProperty, heightAnim);
                        }
                        SizeTimer?.Dispose();
                    });
                }, null, 50, 0);
                MessageTimer?.Dispose();
            });
        }, null, 150, 0);
    }

    public static void Show(string message, MessageType messageType = MessageType.None) {
        Console.WriteLine(message);
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow != null && mainWindow.Content is Grid gird) {
            var container = gird.Children.OfType<Grid>().FirstOrDefault(i => i.Name == "MessageTipContainer")?.Children;
            var first = container.OfType<MessageTips>().FirstOrDefault();
            if (container.Count == 1 && !first.isClosing) {
                first.Hide();
                if (first.Message != message) {
                    first.SetMessage(message,messageType);
                }
            }
            else if (container.Count == 0 || (container.Count == 1 && container.OfType<MessageTips>().FirstOrDefault().isClosing)) {
                var messageTips = new MessageTips(message,messageType);
                container.Add(messageTips);
                messageTips.ShowAnim.Begin();
            }
            
        }
    }

    public void Hide() {
        HideTimer?.Dispose();
        DeleteTimer?.Dispose();
        HideTimer = new Timer(o => {
            HideImmediately();
        }, null, 1500, 0);
    }

    public void HideImmediately() {
        HideTimer?.Dispose();
        DeleteTimer?.Dispose();
        this.Dispatcher.BeginInvoke(() => {
            isClosing = true;
            HideAnim.Begin();
            DeleteTimer = new Timer(o => {
                this.Dispatcher.BeginInvoke(() => {
                    var mainWindow = Application.Current.MainWindow;
                    if (mainWindow != null && mainWindow.Content is Grid gird) {
                        gird.Children.OfType<Grid>().FirstOrDefault(i => i.Name == "MessageTipContainer")?.Children
                            .Remove(this);
                    }
                });
                DeleteTimer?.Dispose();
            },null,300, 0);
            HideTimer?.Dispose();
        });
    }

    public void TextColorChange(MessageType messageType) {
        switch (messageType) {
            case MessageType.Warning :
                MessageColor = "DarkGoldenrod";
                break;
            case MessageType.Error :
                MessageColor = "DarkRed";
                break;
            default:
                MessageColor = "#001D1F";
                break;
        }
    }

    private void Main_OnMouseEnter(object sender, MouseEventArgs e) {
        if (!isClosing) {
            HideTimer?.Dispose();
            DeleteTimer?.Dispose();
        }
    }

    private void Main_OnMouseLeave(object sender, MouseEventArgs e) {
        isDown = false;
        if (isClosing) {
            return;
        }
        MouseUpAnim.Begin();
        Hide();
    }

    private bool isDown = false;
    private void Main_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        if (!isClosing) {
            MouseDownAnim.Begin();
            isDown = true;
        }
    }

    private void Main_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
        if (isDown) {
            MouseUpAnim.Begin();
            HideImmediately();
        }
        isDown = false;
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
}