using System.IO;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using StarFallMC.Entity;
using StarFallMC.Util;

namespace StarFallMC.Component;

public partial class Notices : UserControl {
    private string SettingFile = $"{DirFileUtil.LauncherSettingsDir}/Notices.json";
    public Notices() {
        InitializeComponent();

        InitNotices();
    }

    public void InitNotices() {
        if (PropertiesUtil.launcherArgs.EnableNotice) {
            foreach (var i in GetNotices()) {
                var noticeItem = new Notice();
                noticeItem.Title = i.Title;
                noticeItem.ContentText = i.Content;
                noticeItem.Icon = i.Icon;
                NoticesContainer.Children.Add(noticeItem);
            }
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
                    item.Icon = i["icon"]?.ToString();
                    notices.Add(item);
                }
            }
            catch (Exception e){
                Console.WriteLine(e);
            }
        }

        if (notices.Count == 0) {
            var item = new NoticeItem("一个 Minecraft 启动器",
                "- 目前启动器还在开发阶段 -\n目前启动器支持的功能\n   ● 支持正版登录\n   ● 支持多版本管理\n   ● 支持资源文件补全\n   ● 支持修改版本属性\n   ● 支持自定义启动器背景\n   ● 支持自定义公告\n   ● 支持多文件并行下载\n");
            item.Icon = "pack://application:,,,/;component/assets/ico.ico";
            notices.Add(item);
        }
        
        return notices;
    }
    
}