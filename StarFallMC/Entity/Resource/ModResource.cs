using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace StarFallMC.Entity.Resource;

public class ModResource : INotifyPropertyChanged{
    public string DisplayName { get; set; }
    public string Slug { get; set; }
    public string Logo { get; set; }
    public string Type { get; set; }
    public string ResourceVersion { get; set; }
    public string FilePath { get; set; }
    public string FileName { get => Path.GetFileName(FilePath); }
    public string FileNameWithExtension { get => Path.GetFileNameWithoutExtension(FilePath); }
    public string Description { get; set; }
    public string Author { get; set; }
    public string AuthorUrl { get; set; }
    public string WebsiteUrl { get; set; }
    
    public List<string> Loaders { get; set; } = new();
    public List<string> GameVersions { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    
    public string ResourceSource { get; set; }
    
    public string ModrinthProjectId { get; set; }
    public string ModrinthAuthorId { get; set; }
    public string ModrinthSha1 { get; set; }
            
            
    public int CurseForgeId { get; set; }
    public int CurseForgeFileId { get; set; }
    public uint CurseForgeSha1 { get; set; }
    
    private bool _disabled = false;

    public bool Disabled {
        get => _disabled; 
        set => SetField(ref _disabled, value);
    }

    public List<ModDownloader> Downloaders { get; set; } = new();
    
    public int DownloadCount { get; set; }
    public string DownloadCountText {
        get {
            if (DownloadCount == 0) return "0";
        
            double absNumber = Math.Abs(DownloadCount);
            string sign = DownloadCount < 0 ? "-" : "";

            if (absNumber >= 100000000) {
                return $"{sign}{(absNumber / 100000000):F1}亿";
            }
            else if (absNumber >= 10000) {
                return $"{sign}{(absNumber / 10000):F1}万";
            }
            else if (absNumber >= 1000) {
                return $"{sign}{(absNumber / 1000):F1}千";
            }
            else {
                return $"{sign}{absNumber:F1}";
            }
        }
    }
    public int FollowsCount { get; set; }
    
    public string LastUpdated { get; set; }

    public string UpdatedTimeAgo {
        get {
            if (string.IsNullOrEmpty(LastUpdated)) {
                return string.Empty;
            }

            var startDate = DateTime.Parse(LastUpdated);
            var endDate = DateTime.Now;
            if (startDate > endDate) {
                (startDate, endDate) = (endDate, startDate);
            }

            int years = endDate.Year - startDate.Year;
            int months = 0;
            int days = 0;
            
            if (endDate.Month < startDate.Month || 
                (endDate.Month == startDate.Month && endDate.Day < startDate.Day)) {
                years--;
                months = 12 - startDate.Month + endDate.Month;
            }
            else {
                months = endDate.Month - startDate.Month;
            }
            if (endDate.Day < startDate.Day) {
                months--;
                DateTime lastMonth = endDate.AddMonths(-1);
                days = (endDate - lastMonth).Days + (DateTime.DaysInMonth(startDate.Year, startDate.Month) - startDate.Day);
            }
            else {
                days = endDate.Day - startDate.Day;
            }
            if (months < 0) {
                years--;
                months += 12;
            }
            string result = "";
            if (years > 0) {
                result += $"{years} 年";
            }
            else if (months > 0) {
                result += $"{months} 月";
            }
            else if (days > 0) {
                result += $"{days} 天";
            }
            else {
                result = "今天";
            }
            return result != "今天" ? result + "前" : result;
        }
    }

    public override string ToString() {
        return $"ModResource:(DisplayName:{DisplayName},ResourceVersion:{ResourceVersion},FilePath:{FilePath},FileName:{FileName},FileNameWithExtension:{FileNameWithExtension},Description:{Description},Author:{Author},AuthorUrl:{AuthorUrl},WebsiteUrl:{WebsiteUrl},ResourceSource:{ResourceSource},ModrinthProjectId:{ModrinthProjectId},ModrinthAuthorId:{ModrinthAuthorId},ModrinthSha1:{ModrinthSha1},CurseForgeId:{CurseForgeId},CurseForgeFileId:{CurseForgeFileId},CurseForgeSha1:{CurseForgeSha1})";
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