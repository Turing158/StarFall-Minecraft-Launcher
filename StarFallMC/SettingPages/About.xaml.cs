using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StarFallMC.Util;
using Button = StarFallMC.Component.Button;

namespace StarFallMC.SettingPages;

public partial class About : Page {

    private ViewModel viewModel = new();
    
    public About() {
        InitializeComponent();
        DataContext = viewModel;
    }
    
    public class ViewModel : INotifyPropertyChanged {

        public List<PublicThanksItem> PublicThanksItems {
            get => new () { 
                new PublicThanksItem() {
                    Name = "Turing158",
                    Role = "感谢 @Turing158 把我创造出来",
                    Link = "https://turing158.github.io/",
                    Avatar = "https://foruda.gitee.com/avatar/1682216074543204020/12834578_turing-ice_1682216074.png!avatar60"
                },
                new PublicThanksItem() {
                    Name = "bangbang93",
                    Role = "感谢 @bangbang93 提供的BMCLAPI支持",
                    Link = "https://blog.bangbang93.com/",
                    Avatar = "https://www.bangbang93.com/assets/uploads/profile/uid-1/1-profileimg.jpg"
                },
                new PublicThanksItem() {
                    Name = "Minecraft中文Wiki",
                    Role = "感谢 @Minecraft中文Wiki 提供关于Minecraft的参数信息",
                    Link = "https://zh.minecraft.wiki/",
                    Avatar = "https://zh.minecraft.wiki/images/Wiki.png"
                },
                new PublicThanksItem() {
                    Name = "Modrinth",
                    Role = "感谢 @Modrinth 提供的资源查询和下载服务",
                    Link = "https://modrinth.com/",
                    Avatar = "https://modrinth.com/favicon-light.ico"
                },
                new PublicThanksItem() {
                    Name = "CurseForge",
                    Role = "感谢 @CurseForge 提供的资源查询和下载服务",
                    Link = "https://www.curseforge.com/minecraft",
                    Avatar = "https://static-beta.curseforge.com/images/favicon.ico"
                },
            };
        }
        
        public bool _needUpdate = false;
        public bool NeedUpdate { 
            get => _needUpdate;
            set => SetField(ref _needUpdate, value);
        }

        public string _updateVersionName;
        public string UpdateVersionName { 
            get => _updateVersionName;
            set => SetField(ref _updateVersionName, value);
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
    
    public class PublicThanksItem {
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e) {
        var item = sender as Button;
        if (item.Tag is string link) {
            if (NetworkUtil.IsValidUrl(link)) {
                NetworkUtil.OpenUrl(link);
            }
        }
    }

    private bool updateBtnDown = false;
    private DoubleAnimation updateBtnDownAnimation = new() {
        To = 0.95,
        Duration = TimeSpan.FromMilliseconds(200),
        EasingFunction = new CubicEase()
    };
    private DoubleAnimation updateBtnUpAnimation = new() {
        To = 1,
        Duration = TimeSpan.FromMilliseconds(200),
        EasingFunction = new CubicEase()
    };
    private void Update_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        if (viewModel.NeedUpdate) {
            updateBtnDown = true;
            UpdateBtn.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, updateBtnDownAnimation);
            UpdateBtn.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, updateBtnDownAnimation);
        }
    }

    private void Update_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
        if (viewModel.NeedUpdate && updateBtnDown) {
            updateBtnDown = false;
            UpdateBtn.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, updateBtnUpAnimation);
            UpdateBtn.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, updateBtnUpAnimation);
            // 执行更新操作
        }
    }

    private void Update_OnMouseLeave(object sender, MouseEventArgs e) {
        if (viewModel.NeedUpdate && updateBtnDown) {
            updateBtnDown = false;
            UpdateBtn.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, updateBtnUpAnimation);
            UpdateBtn.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, updateBtnUpAnimation);
        }
    }
}