using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace StarFallMC.Component;

public partial class MessageBox : UserControl {
    
    public enum BtnType {
        Confirm,
        ConfirmAndCancel,
        Custom,
        ConfirmAndCustom,
        CustomAndCancel,
        ConfirmAndCancelAndCustom
    }
    public enum Result {
        Confirm,
        Cancel,
        Custom
    }
    private Action<Result> _callback;
    
    private ViewModel viewModel = new ViewModel();
    private Timer HideTimer;
    private Storyboard HighlightBox;
    public MessageBox() {
        InitializeComponent();
        DataContext = viewModel;
        HighlightBox = FindResource("HighlightBox") as Storyboard;
    }
    
    public class ViewModel : INotifyPropertyChanged {

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

    public static void Show(string content,string title = "提示",
        BtnType btnType = BtnType.Confirm, Action<Result>? callback = null,
        string customBtnText = "",string confirmBtnText = "确定",string cancelBtnText = "取消",
        bool showCloseBtn = false) {
        var box = new MessageBox();
        
        box.viewModel.Message = content;
        box.viewModel.Title = title;
        box._callback = callback;
        box.viewModel.ConfirmBtnVisibility = 
            btnType == BtnType.Confirm || 
            btnType == BtnType.ConfirmAndCancel || 
            btnType == BtnType.ConfirmAndCustom || 
            btnType == BtnType.ConfirmAndCancelAndCustom 
            ? Visibility.Visible : Visibility.Collapsed;
        box.viewModel.CancelBtnVisibility =
            btnType == BtnType.ConfirmAndCancel || 
            btnType == BtnType.CustomAndCancel || 
            btnType == BtnType.ConfirmAndCancelAndCustom 
            ? Visibility.Visible : Visibility.Collapsed;
        box.viewModel.CustomBtnVisibility =
            btnType == BtnType.CustomAndCancel ||
            btnType == BtnType.ConfirmAndCustom ||
            btnType == BtnType.Custom 
            ? Visibility.Visible : Visibility.Collapsed;
        box.viewModel.CloseBtnVisibility = showCloseBtn ? Visibility.Visible : Visibility.Collapsed;
        box.viewModel.ConfirmBtnText = confirmBtnText;
        box.viewModel.CancelBtnText = cancelBtnText;
        box.viewModel.CustomBtnText = customBtnText;
        
        
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow != null && mainWindow.Content is Grid gird) {
            gird.Children.Add(box);
        }
        box.ShowFunc();
    }

    private void ShowFunc() {
        Mask.Show();
    }

    private void Confirm_OnClick(object sender, RoutedEventArgs e) {
        CloseFunc(Result.Confirm);
    }
    
    private void Cancel_OnClick(object sender, RoutedEventArgs e) {
        CloseFunc(Result.Cancel);
    }
    
    private void Custom_OnClick(object sender, RoutedEventArgs e) {
        CloseFunc(Result.Custom);
    }

    private void CloseFunc(Result result) {
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
    }

    private void Mask_OnClickMask(object sender, RoutedEventArgs e) {
        HighlightBox.Begin();
    }
}