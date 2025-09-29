using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace StarFallMC.Entity;

public class ModResource : INotifyPropertyChanged{
    public string DisplayName { get; set; }
    public string slug { get; set; }
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

    public List<DownloadFile> DownloadFiles { get; set; } = new();

    public override string ToString() {
        return $"MinecraftResource:{{DisplayName:{DisplayName},ResourceVersion:{ResourceVersion},FilePath:{FilePath},FileName:{FileName},FileNameWithExtension:{FileNameWithExtension},Description:{Description},Author:{Author},AuthorUrl:{AuthorUrl},WebsiteUrl:{WebsiteUrl},ResourceSource:{ResourceSource},ModrinthProjectId:{ModrinthProjectId},ModrinthAuthorId:{ModrinthAuthorId},ModrinthSha1:{ModrinthSha1},CurseForgeId:{CurseForgeId},CurseForgeFileId:{CurseForgeFileId},CurseForgeSha1:{CurseForgeSha1}}}";
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