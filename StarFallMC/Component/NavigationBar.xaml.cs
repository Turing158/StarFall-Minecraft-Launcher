using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace StarFallMC.Component;

public partial class NavigationBar : ListView {
    
    public double ItemFontSize {
        get => (double)GetValue(ItemFontSizeProperty); 
        set => SetValue(ItemFontSizeProperty, value);
    }
    public static readonly DependencyProperty ItemFontSizeProperty =
        DependencyProperty.Register(nameof(ItemFontSize), typeof(double), typeof(NavigationBar), new PropertyMetadata(14.0));
    
    public Brush ItemBackground {
        get => (Brush)GetValue(ItemBackgroundProperty); 
        set => SetValue(ItemBackgroundProperty, value);
    }
    public static readonly DependencyProperty ItemBackgroundProperty =
        DependencyProperty.Register(nameof(ItemBackground), typeof(Brush), typeof(NavigationBar), new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#264446"))));
    
    public Brush ItemForeground {
        get => (Brush)GetValue(ItemForegroundProperty);
        set => SetValue(ItemForegroundProperty, value);
    }
    public static readonly DependencyProperty ItemForegroundProperty =
        DependencyProperty.Register(nameof(ItemForeground), typeof(Brush), typeof(NavigationBar), new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C4ABAA"))));
    
    public Brush ItemHoverColor {
        get => (Brush)GetValue(ItemHoverColorProperty);
        set => SetValue(ItemHoverColorProperty, value);
    }
    public static readonly DependencyProperty ItemHoverColorProperty =
        DependencyProperty.Register(nameof(ItemHoverColor), typeof(Brush), typeof(NavigationBar), new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#001D1F"))));
    
    public NavigationBar() {
        InitializeComponent();
        //之后会用到
        // Main.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(WrapPanel)));
    }
}