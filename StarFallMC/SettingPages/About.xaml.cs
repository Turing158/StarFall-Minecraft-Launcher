using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StarFallMC.Entity;
using StarFallMC.Util;
using Button = StarFallMC.Component.Button;

namespace StarFallMC.SettingPages;

public partial class About : Page {

    private ViewModel viewModel = new();
    DoubleAnimation valueTo1 = new () {
        To = 1,
        Duration = TimeSpan.FromSeconds(0.2),
    };
    DoubleAnimation valueTo0 = new () {
        To = 0,
        Duration = TimeSpan.FromSeconds(0.2),
    };
    public About() {
        InitializeComponent();
        DataContext = viewModel;
        UpdateLoading.BeginAnimation(OpacityProperty, valueTo1);
        CheckUpdate();
    }
    
    public async Task CheckUpdate() {
        UpdateLoading.BeginAnimation(OpacityProperty, valueTo1);
        if (DateTime.Now - PropertiesUtil.LastCheckUpdateTime > TimeSpan.FromMinutes(10)) {
            PropertiesUtil.LastCheckUpdateTime = DateTime.Now;
            var updateInfo = await NetworkUtil.GetUpdateInfo();
            if (updateInfo == null) {
                viewModel.NeedUpdate = false;
                UpdateLoading.BeginAnimation(OpacityProperty, valueTo0);
            }
            else {
                PropertiesUtil.LastUpdateInfo = updateInfo;
                viewModel.LastUpdateInfo = updateInfo;
                if (Version.Parse(viewModel.LastUpdateInfo.Version) > Version.Parse(PropertiesUtil.LauncherVersion)) {
                    viewModel.NeedUpdate = true;
                }
            }
        }
        UpdateLoading.BeginAnimation(OpacityProperty, valueTo0);
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
                    Name = "PCL",
                    Role = "感谢 @PCL 提供的模组中文文件",
                    Link = "https://afdian.com/a/LTCat",
                    Avatar = "https://attachment.mczwlt.net/mczwlt/public/resource/icon/Bt1x1ABmR5KYbyccrNNQL.png"
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
        
        private bool _needUpdate = false;
        public bool NeedUpdate { 
            get => _needUpdate;
            set => SetField(ref _needUpdate, value);
        }
        
        private UpdateInfo _lastUpdateInfo = new();
        public UpdateInfo LastUpdateInfo { 
            get => _lastUpdateInfo;
            set => SetField(ref _lastUpdateInfo, value);
        }
        
        public string CurrentVersion { 
            get => $"v{PropertiesUtil.LauncherVersion}";
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
        updateBtnDown = true;
        UpdateBtn.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, updateBtnDownAnimation);
        UpdateBtn.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, updateBtnDownAnimation);
    }

    private void Update_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
        if (updateBtnDown) {
            updateBtnDown = false;
            UpdateBtn.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, updateBtnUpAnimation);
            UpdateBtn.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, updateBtnUpAnimation);
            if (viewModel.NeedUpdate) {
                if (!string.IsNullOrEmpty(viewModel.LastUpdateInfo.UpdateUrl)) {
                    NetworkUtil.OpenUrl(viewModel.LastUpdateInfo.UpdateUrl);
                }
                else {
                    NetworkUtil.OpenUrl(viewModel.LastUpdateInfo.DefaultUpdateUrl);
                }
            }
            else {
                CheckUpdate();
            }
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