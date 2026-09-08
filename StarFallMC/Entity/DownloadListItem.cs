using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using StarFallMC.Services.Download;

namespace StarFallMC.Entity;

public sealed class DownloadListItem : INotifyPropertyChanged {
    private string _name;
    private string _filePath;
    private string _urlPath;
    private DownloadFile.StateType _state;
    private long _size;
    private long _bytesDownloaded;
    private string _errorMessage;

    public DownloadListItem(DownloadFileSnapshot snapshot) {
        Id = snapshot.Id;
        _name = snapshot.Name;
        _filePath = snapshot.FilePath;
        _urlPath = snapshot.UrlPath;
        _state = snapshot.State;
        _size = snapshot.Size;
        _bytesDownloaded = snapshot.BytesDownloaded;
        _errorMessage = snapshot.ErrorMessage;
    }

    public long Id { get; }
    public string Name => _name;
    public string FilePath => _filePath;
    public string UrlPath => _urlPath;
    public DownloadFile.StateType State => _state;
    public long Size => _size;
    public long BytesDownloaded => _bytesDownloaded;
    public string ErrorMessage => _errorMessage;
    public string FileName => Path.GetFileName(FilePath);

    public string FileIcon => Path.GetExtension(FilePath).ToLowerInvariant() switch {
        ".json" => "\ue7bd",
        ".jar" => "\ue639",
        ".zip" or ".rar" => "\ue7bb",
        _ => "\ue625"
    };

    public string SizeStr => Size < 0
        ? "未知"
        : Size >= 1024L * 1024 * 1024
            ? $"{Size / (1024L * 1024 * 1024):F0} GB"
            : Size >= 1024L * 1024
                ? $"{Size / (1024L * 1024):F0} MB"
                : Size >= 1024
                    ? $"{Size / 1024L:F0} KB"
                    : $"{Size} B";

    public string StateColor => State switch {
        DownloadFile.StateType.Waiting => "DarkGoldenrod",
        DownloadFile.StateType.Downloading => "DarkCyan",
        DownloadFile.StateType.Finished => "DarkGreen",
        DownloadFile.StateType.Error => "DarkRed",
        _ => "#f1f1f1"
    };

    internal bool UpdateFrom(DownloadFileSnapshot snapshot) {
        if (snapshot.Id != Id) {
            throw new ArgumentException("Snapshot ID does not match the existing download item.", nameof(snapshot));
        }

        SetField(ref _name, snapshot.Name, nameof(Name));
        if (SetField(ref _filePath, snapshot.FilePath, nameof(FilePath))) {
            OnPropertyChanged(nameof(FileName));
            OnPropertyChanged(nameof(FileIcon));
        }
        SetField(ref _urlPath, snapshot.UrlPath, nameof(UrlPath));
        var stateChanged = SetField(ref _state, snapshot.State, nameof(State));
        if (stateChanged) {
            OnPropertyChanged(nameof(StateColor));
        }
        if (SetField(ref _size, snapshot.Size, nameof(Size))) {
            OnPropertyChanged(nameof(SizeStr));
        }
        SetField(ref _bytesDownloaded, snapshot.BytesDownloaded, nameof(BytesDownloaded));
        SetField(ref _errorMessage, snapshot.ErrorMessage, nameof(ErrorMessage));
        return stateChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, string propertyName) {
        if (EqualityComparer<T>.Default.Equals(field, value)) {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
