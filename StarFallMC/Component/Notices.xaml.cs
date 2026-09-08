using System.IO;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
using StarFallMC.Entity;
using StarFallMC.Util;

namespace StarFallMC.Component;

public partial class Notices : UserControl {
    private string SettingFile = $"{DirFileUtil.LauncherSettingsDir}/Notices.json";
    private static WeakReference<Notices>? _current;
    public static void RefreshAll() {
        if (_current != null && _current.TryGetTarget(out var notices)) {
            notices.Dispatcher.BeginInvoke(notices.refreshNotices);
        }
    }
    public Notices() {
        InitializeComponent();
        InitNotices();
        _current = new WeakReference<Notices>(this);
    }

    public void InitNotices() {
        if (PropertiesUtil.launcherArgs.EnableNotice) {
            foreach (var i in GetNotices()) {
                var noticeItem = new Notice();
                noticeItem.Title = i.Title;
                noticeItem.ContentText = i.Content;
                noticeItem.Icon = ConvertToImageSource(i.Icon);
                NoticesContainer.Children.Add(noticeItem);
            }
        }
    }

    private static BitmapImage? ConvertToImageSource(string? path) {
        if (string.IsNullOrEmpty(path))
            return null;
        try {
            var uri = new Uri(path, UriKind.RelativeOrAbsolute);
            return new BitmapImage(uri);
        }
        catch {
            return null;
        }
    }
    
    private List<NoticeItem> GetNotices() {
        List<NoticeItem> notices = new ();
        
        if (File.Exists(SettingFile)) {
            try {
                foreach (var i in JArray.Parse(File.ReadAllText(SettingFile))) {
                    NoticeItem item;
                    if (i["source"] == null || string.IsNullOrEmpty(i["source"].ToString())) {
                        item = new NoticeItem(i["title"]?.ToString());
                        if (i["content"] != null && !string.IsNullOrEmpty(i["content"].ToString())) {
                            item.Content = i["content"].ToString();
                        }
                    }
                    else {
                        item = new NoticeItem(i["title"]?.ToString(), i["source"].ToString());
                    }
                    // 处理图标路径：如果是相对路径则转换为绝对路径，最终转为 ImageSource
                    var iconPath = i["icon"]?.ToString();
                    if (!string.IsNullOrEmpty(iconPath)) {
                        // 检查是否是网络路径或pack:// URI
                        bool isNetworkPath = iconPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                             iconPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                                             iconPath.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase);
                        bool isPackUri = iconPath.StartsWith("pack://", StringComparison.OrdinalIgnoreCase);

                        if (isNetworkPath || isPackUri) {
                            // 网络路径或pack:// URI，直接使用
                            item.Icon = iconPath;
                        }
                        else if (!Path.IsPathRooted(iconPath)) {
                            // 相对路径，转换为绝对路径
                            item.Icon = DirFileUtil.GetAbsolutePathInLauncherSettingDir(iconPath);
                        }
                        else {
                            // 已经是绝对路径，直接使用
                            item.Icon = iconPath;
                        }
                    }
                    else {
                        item.Icon = null;
                    }
                    notices.Add(item);
                }
            }
            catch (Exception e){
                Console.WriteLine(e);
            }
        }

        if (notices.Count == 0) {
            var item = new NoticeItem("一个 Minecraft 启动器");
            item.Content =
                "- 目前启动器还在开发阶段 \n- 目前启动器支持的功能\n\n   ● 支持正版登录\n\n   ● 支持多版本管理\n\n   ● 支持资源文件补全\n\n   ● 支持修改版本属性\n\n   ● 支持自定义启动器背景\n\n   ● 支持自定义公告\n\n   ● 支持多文件并行下载\n";
            item.Icon = "pack://application:,,,/;component/assets/ico.ico";
            notices.Add(item);
        }
        return notices;
    }

    private void refreshNotices(){
        // 清除所有Notice控件并释放资源
        foreach (var child in NoticesContainer.Children.OfType<Notice>().ToList()) {
            // 清除数据上下文以释放绑定
            child.DataContext = null;
            // 清除依赖属性值
            child.ClearValue(Notice.IconProperty);
            child.ClearValue(Notice.TitleProperty);
            child.ClearValue(Notice.ContentTextProperty);
        }
        NoticesContainer.Children.Clear();

        InitNotices();
    }
}
