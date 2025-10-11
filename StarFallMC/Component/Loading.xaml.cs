using System.Windows;
using System.Windows.Controls;

namespace StarFallMC.Component;

public partial class Loading : UserControl {
    
    public string PercentText {
        get => (string)GetValue(PercentTextProperty);
        set => SetValue(PercentTextProperty, value);
    }
    public static readonly DependencyProperty PercentTextProperty =
        DependencyProperty.Register("PercentText", typeof(string), typeof(Loading), new PropertyMetadata("0%"));
    
    public Loading() {
        InitializeComponent();
    }
}