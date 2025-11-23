using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using StarFallMC.Entity.Enum;
using MessageBoxResult = StarFallMC.Entity.Enum.MessageBoxResult;

namespace StarFallMC.Component;

public partial class MessageBox : UserControl,INotifyPropertyChanged {
    
    private Action<MessageBoxResult> _callback;
    
    private Timer HideTimer;
    private Storyboard HighlightBox;
    private TaskCompletionSource<MessageBox> _tcs;
    
    private string _title;
    public string Title {
        get => _title;
        set => SetField(ref _title, value);
    }

    private string _message;
    public string Message {
        get => _message;
        set => SetField(ref _message, value);
    }

    private Visibility _confirmBtnVisibility;
    public Visibility ConfirmBtnVisibility {
        get => _confirmBtnVisibility;
        set => SetField(ref _confirmBtnVisibility, value);
    }
        
    private Visibility _cancelBtnVisibility;
    public Visibility CancelBtnVisibility {
        get => _cancelBtnVisibility;
        set => SetField(ref _cancelBtnVisibility, value);
    }
        
    private Visibility _closeBtnVisibility;
    public Visibility CloseBtnVisibility {
        get => _closeBtnVisibility;
        set => SetField(ref _closeBtnVisibility, value);
    }

    private Visibility _customBtnVisibility;
    public Visibility CustomBtnVisibility {
        get => _customBtnVisibility;
        set => SetField(ref _customBtnVisibility, value);
    }
        
    private string _customBtnText;
    public string CustomBtnText {
        get => _customBtnText;
        set => SetField(ref _customBtnText, value);
    }
        
    private string _confirmBtnText;
    public string ConfirmBtnText {
        get => _confirmBtnText;
        set => SetField(ref _confirmBtnText, value);
    }
        
    private string _cancelBtnText;
    public string CancelBtnText {
        get => _cancelBtnText;
        set => SetField(ref _cancelBtnText, value);
    }
    
    public MessageBox() {
        InitializeComponent();
        DataContext = this;
        HighlightBox = FindResource("HighlightBox") as Storyboard;
    }

    // 同步显示消息框,但是不等待用户操作完成
    public static MessageBox Show(string content,string title = "提示",
        MessageBoxBtnType btnType = MessageBoxBtnType.Confirm, Action<MessageBoxResult>? callback = null,
        string customBtnText = "",string confirmBtnText = "确定",string cancelBtnText = "取消",
        bool showCloseBtn = false) {
        var box = new MessageBox();
        
        box.Message = content;
        box.Title = title;
        box._callback = callback;
        box.ConfirmBtnVisibility = 
            btnType == MessageBoxBtnType.Confirm || 
            btnType == MessageBoxBtnType.ConfirmAndCancel || 
            btnType == MessageBoxBtnType.ConfirmAndCustom || 
            btnType == MessageBoxBtnType.ConfirmAndCancelAndCustom 
            ? Visibility.Visible : Visibility.Collapsed;
        box.CancelBtnVisibility =
            btnType == MessageBoxBtnType.Cancel ||
            btnType == MessageBoxBtnType.ConfirmAndCancel || 
            btnType == MessageBoxBtnType.CustomAndCancel || 
            btnType == MessageBoxBtnType.ConfirmAndCancelAndCustom 
            ? Visibility.Visible : Visibility.Collapsed;
        box.CustomBtnVisibility =
            btnType == MessageBoxBtnType.CustomAndCancel ||
            btnType == MessageBoxBtnType.ConfirmAndCustom ||
            btnType == MessageBoxBtnType.Custom ||
            btnType == MessageBoxBtnType.ConfirmAndCancelAndCustom 
            ? Visibility.Visible : Visibility.Collapsed;
        box.CloseBtnVisibility = showCloseBtn ? Visibility.Visible : Visibility.Collapsed;
        box.ConfirmBtnText = confirmBtnText;
        box.CancelBtnText = cancelBtnText;
        box.CustomBtnText = customBtnText;
        
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow != null && mainWindow.Content is Grid grid) {
            grid.Children.OfType<Grid>().FirstOrDefault(i => i.Name == "MessageBoxContainer")?.Children.Add(box);
        }
        box.ShowFunc();
        return box; 
    }
    
    public static async Task<MessageBox> ShowAsync(string content, string title = "提示",
        MessageBoxBtnType btnType = MessageBoxBtnType.Confirm, Action<MessageBoxResult>? callback = null,
        string customBtnText = "", string confirmBtnText = "确定", string cancelBtnText = "取消",
        bool showCloseBtn = false) {
        var box = Show(content, title, btnType, callback, customBtnText, confirmBtnText, cancelBtnText, showCloseBtn);
        box._tcs = new TaskCompletionSource<MessageBox>();
        return await box._tcs.Task;
    }

    private void ShowFunc() {
        Mask.Show();
    }

    private void Confirm_OnClick(object sender, RoutedEventArgs e) {
        CloseFunc(MessageBoxResult.Confirm);
    }
    
    private void Cancel_OnClick(object sender, RoutedEventArgs e) {
        CloseFunc(MessageBoxResult.Cancel);
    }
    
    private void Custom_OnClick(object sender, RoutedEventArgs e) {
        CloseFunc(MessageBoxResult.Custom);
    }

    private void CloseFunc(MessageBoxResult result) {
        Mask.Hide();
        HideTimer = new Timer((s) => {
            this.Dispatcher.Invoke(() => {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null && mainWindow.Content is Grid gird) {
                    gird.Children.Remove(this);
                }
                HideTimer.Dispose();
            });
        },null, 300, 0);
        _callback?.Invoke(result);
        _tcs?.TrySetResult(this);
    }

    private void Mask_OnClickMask(object sender, RoutedEventArgs e) {
        HighlightBox.Begin();
    }
    
    public static void Delete(MessageBox box) {
        if (box.HideTimer != null) {
            box.HideTimer.Dispose();
        }
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow != null && mainWindow.Content is Grid gird) {
            gird.Children.OfType<Grid>().FirstOrDefault(i => i.Name == "MessageBoxContainer")?.Children.Remove(box);
        }
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