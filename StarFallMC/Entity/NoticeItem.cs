using System.IO;
using StarFallMC.Util;

namespace StarFallMC.Entity;

public class NoticeItem {
    public string Title { set; get; }
    public string Content { set; get; }
    public string Icon { set; get; }
    
    public string Source { set; get; }

    public NoticeItem() {
    }

    public NoticeItem(string title, string source) {
        Source = DirFileUtil.GetAbsolutePathInLauncherSettingDir(source);
        if (File.Exists(Source)) {
            var texts = File.ReadAllLines(Source);
            if (texts.Length == 0) {
                return;
            }
            string titleMarkdown = Markdig.Markdown.ToPlainText(texts[0]);
            bool isSkip = false;
            Title = title;
            if (string.IsNullOrEmpty(Title)) {
                Title = string.IsNullOrEmpty(titleMarkdown) 
                    ? Path.GetFileNameWithoutExtension(Source)
                    : texts[0];
                isSkip = true;
                if (string.IsNullOrEmpty(titleMarkdown)) {
                    isSkip = false;
                }
            }
            Content = string.Join("\n", isSkip ? texts.Skip(1) : texts);
        }
    }

    public NoticeItem(string title) {
        this.Title = title;
    }
    
    public override bool Equals(object? obj) {
        return obj is NoticeItem notice &&
               Title == notice.Title &&
               Content == notice.Content;
    }

    public override int GetHashCode() {
        return HashCode.Combine(Title, Content);
    }

    public override string ToString() {
        return $"NoticeItem(title={Title}, content={Content})";
    }
}