using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace StarFallMC.Entity;

public class DownloadFile : INotifyPropertyChanged {
    
    public enum StateType {
        Waiting,
        Downloading,
        Finished,
        Error
    }
    public string Name { get; set; }
    public string FilePath { get; set; }
    public string UrlPath { get; set; }
    public List<string> UrlPaths { get; set; }
    public string FileDate { get; set; }
    public int RetryCount { get; set; } = 0;
    public string ErrorMessage { get; set; } = string.Empty;
    public long Size { get; set; } = 1;

    private StateType _state = StateType.Waiting;
    public StateType State {
        get => _state;
        set {
            if (SetField(ref _state,value)) {
                OnPropertyChanged(nameof(StateColor));
            }
        }
    }

    public string FileName {
        get => Path.GetFileName(FilePath);
    }

    public string FileIcon {
        get {
            return Path.GetExtension(FilePath).ToLower() switch {
                ".json" => "\ue7bd",
                ".jar" => "\ue639",
                _ => "\ue625"
            };
        }
    }
    public string StateColor {
        get {
            return State switch {
                StateType.Waiting => "DarkGoldenrod",
                StateType.Downloading => "DarkCyan",
                StateType.Finished => "DarkGreen",
                StateType.Error => "DarkRed",
                _ => "#f1f1f1"
            };
        }
    }

    public DownloadFile() {
    }

    public DownloadFile(string name, string filePath, string urlPath) {
        Name = name;
        FilePath = filePath;
        UrlPath = urlPath;
        UrlPaths = new List<string> { urlPath };
    }

    public override bool Equals(object? obj) {
        return obj is DownloadFile file &&
               Name == file.Name &&
               FilePath == file.FilePath &&
               UrlPath == file.UrlPath;
    }

    public override int GetHashCode() {
        return HashCode.Combine(Name, FilePath, UrlPath);
    }

    public override string ToString() {
        return $"DownloadFile(Name: {Name}, FilePath: {FilePath}, UrlPath:( {string.Join(", ", UrlPath)}))";
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