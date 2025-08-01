using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    private Timer SubFrameTimer;

    private Storyboard DownloadShow;
    private Storyboard DownloadOnlyHide;
    private Storyboard DownloadHide;
    private Timer DownloadFrameTimer;
    
    
    public static Action<string,string> SubFrameNavigate;
    public static Action<string,Action> ReloadSubFrame;
    public static Action DownloadPageShow;
    public MainWindow() {
        
        InitializeComponent();
        
        HomeBtnEnter = (Storyboard)FindResource("HomeBtnEnter");
        SettingBtnEnter = (Storyboard)FindResource("SettingBtnEnter");
        
        SubFrameShow = (Storyboard)FindResource("SubFrameShow");
        SubFrameHide = (Storyboard)FindResource("SubFrameHide");

        DownloadShow = (Storyboard)FindResource("DownloadShow");
        DownloadOnlyHide = (Storyboard)FindResource("DownloadOnlyHide");
        DownloadHide = (Storyboard)FindResource("DownloadHide");
        
        SubFrameNavigate = SubFrameNavigateFunc;
        ReloadSubFrame = reloadSubFrame;
        DownloadPageShow = downloadPageShow;

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
        if (Home.GameStarting) {
            Console.WriteLine("游戏正在启动中，无法进入设置");
            return;
        }
        if (SettingFrame.RenderTransform.Value.OffsetX != 0) {
            HomeBtnEnter.Stop();
            SettingBtnEnter.Begin();
            if (SettingFrameTimer != null) {
                SettingFrameTimer.Dispose();
            }
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
        if (DownloadFrame.Opacity == 0) {
            SubFrameHide.Begin();
            SubFrameTimer = new Timer(o => {
                this.Dispatcher.BeginInvoke(() => {
                    SubFrame.RenderTransform = new TranslateTransform(0, SubFrame.ActualHeight+10);
                    SubFrameTimer.Dispose();
                });
            }, null, 200, 0);
            
        }
        else {
            if (SubFrame.Opacity == 0) {
                DownloadHide.Begin();
            }
            else {
                DownloadOnlyHide.Begin();
            }
            DownloadFrameTimer = new Timer(o => {
                this.Dispatcher.BeginInvoke(() => {
                    DownloadFrame.IsHitTestVisible = false;
                    DownloadFrameTimer.Dispose();
                });
            }, null, 300, 0);
        }
    }

    private void downloadPageShow() {
        DownloadShow.Begin();
        DownloadFrame.IsHitTestVisible = true;
        Title.Text = "下载 - Download";
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
            if (action != null) {
                action();
            }
        });
    }
}