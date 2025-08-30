using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.Util;

namespace StarFallMC;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window {
    
    private Storyboard SettingEnter;
    private Storyboard SettingLeave;
    private Storyboard DownloadGameEnter;
    private Storyboard DownloadGameLeave;
    private Timer SettingFrameTimer;
    private Timer DownloadGameFrameTimer;
    
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
    
    private ViewModel viewModel = new ViewModel();
    public MainWindow() {
        
        InitializeComponent();

        DataContext = viewModel;
        
        SettingEnter = (Storyboard)FindResource("SettingEnter");
        SettingLeave = (Storyboard)FindResource("SettingLeave");
        DownloadGameEnter = (Storyboard)FindResource("DownloadGameEnter");
        DownloadGameLeave = (Storyboard)FindResource("DownloadGameLeave");
        
        SubFrameShow = (Storyboard)FindResource("SubFrameShow");
        SubFrameHide = (Storyboard)FindResource("SubFrameHide");

        DownloadShow = (Storyboard)FindResource("DownloadShow");
        DownloadOnlyHide = (Storyboard)FindResource("DownloadOnlyHide");
        DownloadHide = (Storyboard)FindResource("DownloadHide");
        
        SubFrameNavigate = SubFrameNavigateFunc;
        ReloadSubFrame = reloadSubFrame;
        DownloadPageShow = downloadPageShow;
    }
    
    public class ViewModel : INotifyPropertyChanged{
        private ObservableCollection<NavigationItem> _tabs = new () {
            new NavigationItem("主 页"),
            new NavigationItem("下 载"),
            new NavigationItem("设 置"),
        };
        public ObservableCollection<NavigationItem> Tabs {
            get => _tabs;
            set => SetField(ref _tabs, value);
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

    private void ToHome() {
        HideSetting();
        HideDownloadGame();
    }

    private void ToSetting() {
        if (Home.GameStarting) {
            MessageTips.Show("游戏正在启动中，无法进入设置！", MessageTips.MessageType.Error);
            OperateGrid.SelectedIndex = 0;
            return;
        }

        SettingFrameTimer?.Dispose();
        SettingLeave.Stop();
        SettingEnter.Begin();
        HideDownloadGame();
    }

    private void ToDownloadGame() {
        DownloadGameFrameTimer?.Dispose();
        DownloadGameLeave.Stop();
        DownloadGameEnter.Begin();
        HideSetting();
    }

    private void HideSetting() {
        SettingEnter.Stop();
        SettingLeave.Begin();
        SettingFrameTimer?.Dispose();
        SettingFrameTimer = new Timer(new TimerCallback((state => {
            Dispatcher.BeginInvoke(new Action(() => {
                SettingFrame.RenderTransform = new TranslateTransform(SettingFrame.ActualWidth+10, 0);
            }));
            SettingFrameTimer.Dispose();
        })),null,200,0);
    }

    private void HideDownloadGame() {
        DownloadGameEnter.Stop();
        DownloadGameLeave.Begin();
        DownloadGameFrameTimer?.Dispose();
        DownloadGameFrameTimer = new Timer(new TimerCallback((state => {
            Dispatcher.BeginInvoke(new Action(() => {
                DownloadGameFrame.RenderTransform = new TranslateTransform(DownloadGameFrame.ActualWidth+10, 0);
            }));
            DownloadGameFrameTimer.Dispose();
        })),null,200,0);
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

    private void reloadSubFrame(string pageName,Action action) {
        SubFrame.Navigate(new Uri("Blank.xaml", UriKind.Relative));
        Dispatcher.BeginInvoke(() => {
            SubFrame.Navigate(new Uri($"{pageName}.xaml", UriKind.Relative));
            if (action != null) {
                action();
            }
        });
    }

    private void OperateGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        switch (OperateGrid.SelectedIndex) {
            case 0:
                ToHome();
                break;
            case 1:
                ToDownloadGame();
                break;
            case 2:
                ToSetting();
                break;
        }
    }
}