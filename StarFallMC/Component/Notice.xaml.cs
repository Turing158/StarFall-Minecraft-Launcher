using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StarFallMC.Component;

public partial class Notice : UserControl {
    
    public string Icon {
        get => (string)GetValue(IconProperty);
        set {
            SetValue(IconProperty, value);
            if (string.IsNullOrEmpty(value)) {
                viewModel.TitleMargin = new Thickness(15,0,40,0);
            }
            else {
                viewModel.TitleMargin = new Thickness(40,0,40,0);
            }
        }
    }
    
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(string),
        typeof(CollapsePanel), new PropertyMetadata(""));
    
    public double IconSize {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }
    
    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(nameof(IconSize),
        typeof(double), typeof(CollapsePanel), new PropertyMetadata(22.0));
    
    public object Title {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
    
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string),
        typeof(CollapsePanel), new PropertyMetadata(""));
    
    public double TitleFontSize {
        get => (double)GetValue(TitleFontSizeProperty);
        set => SetValue(TitleFontSizeProperty, value);
    }
    
    public static readonly DependencyProperty TitleFontSizeProperty = DependencyProperty.Register(nameof(TitleFontSize),
        typeof(double), typeof(CollapsePanel), new PropertyMetadata(16.0));
    
    public Brush TitleForeground {
        get => (Brush)GetValue(TitleForegroundProperty);
        set => SetValue(TitleForegroundProperty, value);
    }
    
    public static readonly DependencyProperty TitleForegroundProperty = DependencyProperty.Register(nameof(TitleForeground),
        typeof(Brush), typeof(CollapsePanel), new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#264446"))));
    
    public string ContentText {
        get => (string)GetValue(ContentTextProperty);
        set => SetValue(ContentTextProperty, value);
    }
    
    public static readonly DependencyProperty ContentTextProperty = DependencyProperty.Register(nameof(ContentText), typeof(string),
        typeof(CollapsePanel), new PropertyMetadata(""));

    public ViewModel viewModel = new ViewModel();
    
    public Notice() {
        InitializeComponent();
        DataContext = viewModel;
        
        if (string.IsNullOrEmpty(Icon)) {
            viewModel.TitleMargin = new Thickness(15,0,40,0);
        }
        else {
            viewModel.TitleMargin = new Thickness(40,0,40,0);
        }
    }
    
    public class ViewModel : INotifyPropertyChanged {

        private Thickness _titleMargin;
        public Thickness TitleMargin {
            get => _titleMargin;
            set => SetField(ref _titleMargin, value);
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
}