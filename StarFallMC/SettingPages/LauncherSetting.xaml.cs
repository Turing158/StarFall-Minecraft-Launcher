using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.Util;
using Button = System.Windows.Controls.Button;

namespace StarFallMC.SettingPages;

public partial class LauncherSetting : Page {
    
    public ViewModel viewModel = new ViewModel();
    
    public LauncherSetting() {
        InitializeComponent();
        DataContext = viewModel;
        
        BgSelectNavi.SelectedIndex = PropertiesUtil.launcherArgs.BgType switch {
            "none" => 0,
            "default" => 1,
            "local" => 2,
            "network" => 3,
            _ => 0,
        };
        if (BgSelectNavi.SelectedIndex == 2) {
            LocalBgPath.Text = PropertiesUtil.launcherArgs.BgPath;
        }
        else if (BgSelectNavi.SelectedIndex == 3) {
            NetworkBgPath.Text = PropertiesUtil.launcherArgs.BgPath;
        }
    }
    
    public class ViewModel : INotifyPropertyChanged {
        public ObservableCollection<NavigationItem> BgOptions {
            get => new () {
                new NavigationItem("无"),
                new NavigationItem("默认"),
                new NavigationItem("本地图片"),
                new NavigationItem("网络图片"),
            };
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

    private void ColorBtn_OnClick(object sender, RoutedEventArgs e) {
        var btn = sender as Button;
        string tag = btn?.Tag.ToString();
        if (string.IsNullOrEmpty(tag)) {
            return;
        }
        switch (tag) {
            case "红棕":
                ThemeUtil.ChangeColor(ThemeUtil.ThemeType.Puce);
                break;
            case "乌木灰":
                ThemeUtil.ChangeColor(ThemeUtil.ThemeType.Ebony);
                break;
        }
    }

    private void NavigationBar_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        NoImageBg.Visibility = Visibility.Collapsed;
        DefaultImageBg.Visibility = Visibility.Collapsed;
        LocalImageBg.Visibility = Visibility.Collapsed;
        NetworkImageBg.Visibility = Visibility.Collapsed;
        switch (BgSelectNavi.SelectedIndex) {
            case 1:
                PropertiesUtil.launcherArgs.BgType = "default";
                DefaultImageBg.Visibility = Visibility.Visible;
                Home.SettingBackground.Invoke();
                break;
            case 2:
                PropertiesUtil.launcherArgs.BgType = "local";
                LocalImageBg.Visibility = Visibility.Visible;
                LocalBg_OnLostFocus(null, null);
                break;
            case 3:
                PropertiesUtil.launcherArgs.BgType = "network";
                NetworkImageBg.Visibility = Visibility.Visible;
                NetworkBg_OnLostFocus(null, null);
                break;
            default:
                PropertiesUtil.launcherArgs.BgType = "none";
                NoImageBg.Visibility = Visibility.Visible;
                Home.SettingBackground.Invoke();
                break;
        }
    }

    private void LocalBg_OnLostFocus(object sender, RoutedEventArgs e) {
        PropertiesUtil.launcherArgs.BgPath = LocalBgPath.Text;
        Home.SettingBackground.Invoke();
    }

    private void NetworkBg_OnLostFocus(object sender, RoutedEventArgs e) {
        if (NetworkUtil.IsValidUrl(NetworkBgPath.Text)) {
            PropertiesUtil.launcherArgs.BgPath = NetworkBgPath.Text;
        }
        Home.SettingBackground.Invoke();
    }

    private void DefaultBg_OnClick(object sender, RoutedEventArgs e) {
        Home.SettingBackground.Invoke();
    }

    private void LocalBg_OnClick(object sender, RoutedEventArgs e) {
        var dialog = new OpenFileDialog();
        dialog.Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp";
        if (dialog.ShowDialog() == true) {
            LocalBgPath.Text = dialog.FileName;
            PropertiesUtil.launcherArgs.BgPath = dialog.FileName;
            Home.SettingBackground.Invoke();
        }
    }

    private void HardwareAcceleration_OnClick(object sender, RoutedEventArgs e) {
        var toggleButton = sender as ToggleButton;
        if (toggleButton == null) {
            return;
        }

        PropertiesUtil.launcherArgs.HardwareAcceleration = !toggleButton.IsChecked.Value;
        App.HardwareAccelerationSetting?.Invoke();
    }
}