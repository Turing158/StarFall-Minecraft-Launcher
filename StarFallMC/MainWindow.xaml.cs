using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using StarFallMC.Entity;
using StarFallMC.Util;

namespace StarFallMC;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window {
    
    private Storyboard HomeBtnEnter;
    private Storyboard SettingBtnEnter;
    private Timer SettingFrameTimer;

    
    private Storyboard SubFrameShow;
    private Storyboard SubFrameHide;
    
    public static Action<string,string> SubFrameNavigate;
    public static Action<string,Action> ReloadSubFrame;
    public MainWindow() {
        
        InitializeComponent();
        
        HomeBtnEnter = (Storyboard)FindResource("HomeBtnEnter");
        SettingBtnEnter = (Storyboard)FindResource("SettingBtnEnter");

        
        SubFrameShow = (Storyboard)FindResource("SubFrameShow");
        SubFrameHide = (Storyboard)FindResource("SubFrameHide");
        SubFrameNavigate = SubFrameNavigateFunc;
        ReloadSubFrame = reloadSubFrame;

    }

    private void HomeBtn_OnClick(object sender, RoutedEventArgs e) {
        if (SettingFrame.RenderTransform.Value.OffsetX == 0) {
            HomeBtnEnter.Begin();
            SettingBtnEnter.Stop();
            SettingFrameTimer = new Timer(new TimerCallback((state => {
                this.Dispatcher.BeginInvoke(new Action(() => {
                    SettingFrame.RenderTransform = new TranslateTransform(SettingFrame.ActualWidth+10, 0);
                }));
                SettingFrameTimer.Dispose();
            })),null,200,0);
        }
    }

    private void SettingBtn_OnClick(object sender, RoutedEventArgs e) {
        if (SettingFrame.RenderTransform.Value.OffsetX != 0) {
            HomeBtnEnter.Stop();
            SettingBtnEnter.Begin();
            if (SettingFrameTimer != null) {
                SettingFrameTimer.Dispose();
            }
            SettingFrame.RenderTransform = new TranslateTransform(0, 0);
        }
    }
    
    private void MiniBtn_OnClick(object sender, RoutedEventArgs e) {
        WindowState = WindowState.Minimized;
    }

    private void CloseBtn_OnClick(object sender, RoutedEventArgs e) {
        PropertiesUtil.Save();
        Close();
    }
    
    public void SubFrameNavigateFunc(string pageName,string pageTitle) {
        SubFrame.RenderTransform = new TranslateTransform(0, 0);
        Title.Text = pageTitle;
        SubFrame.Navigate(new Uri($"{pageName}.xaml", UriKind.Relative));
        SubFrameShow.Begin();
    }
    ~MainWindow() => SubFrameNavigate -= SubFrameNavigateFunc;

    private void BackBtn_OnClick(object sender, RoutedEventArgs e) {
        SubFrameHide.Begin();
        SubFrame.RenderTransform = new TranslateTransform(0, SubFrame.ActualHeight+10);
    }

    private void TopFrame_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        DragMove();
    }
    

    private void SettingFrame_OnIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e) {
        
    }

    private void reloadSubFrame(string pageName,Action action) {
        SubFrame.Navigate(new Uri("Blank.xaml", UriKind.Relative));
        Dispatcher.BeginInvoke(() => {
            SubFrame.Navigate(new Uri($"{pageName}.xaml", UriKind.Relative));
            action();
        });
    }
}