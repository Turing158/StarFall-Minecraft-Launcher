using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using StarFallMC.Util;

namespace StarFallMC.Entity.Resource;

public class TexturePackResource : INotifyPropertyChanged {
    private readonly object _iconLoadGate = new();
    private ImageSource _icon = ImageLoader.Placeholder;
    private string? _completedIconSource;
    private string? _loadingIconSource;
    private Task<ImageSource>? _iconLoadTask;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public string IconArchiveEntryName { get; set; } = string.Empty;

    public ImageSource Icon {
        get => _icon;
        private set => SetField(ref _icon, value);
    }

    public async Task EnsureIconLoadedAsync(CancellationToken cancellationToken = default) {
        var archiveEntry = IconArchiveEntryName;
        var sourcePath = string.IsNullOrWhiteSpace(archiveEntry) ? IconPath : Path;
        if (string.IsNullOrWhiteSpace(sourcePath)) {
            return;
        }

        var source = string.IsNullOrWhiteSpace(archiveEntry)
            ? sourcePath
            : $"{sourcePath}|{archiveEntry}";
        Task<ImageSource> loadTask;
        lock (_iconLoadGate) {
            if (string.Equals(_completedIconSource, source, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            if (_iconLoadTask == null ||
                !string.Equals(_loadingIconSource, source, StringComparison.OrdinalIgnoreCase)) {
                _loadingIconSource = source;
                _iconLoadTask = Task.Run(() => string.IsNullOrWhiteSpace(archiveEntry)
                    ? ImageLoader.LoadLocal(sourcePath, 96, 96)
                    : ImageLoader.LoadArchiveEntry(sourcePath, archiveEntry, 96, 96));
            }

            loadTask = _iconLoadTask;
        }

        var image = await loadTask.WaitAsync(cancellationToken);
        var currentSource = string.IsNullOrWhiteSpace(IconArchiveEntryName)
            ? IconPath
            : $"{Path}|{IconArchiveEntryName}";
        if (!string.Equals(source, currentSource, StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        lock (_iconLoadGate) {
            if (ImageLoader.IsPlaceholder(image)) {
                _loadingIconSource = null;
                _iconLoadTask = null;
            }
            else {
                _completedIconSource = source;
            }
        }

        Icon = image;
    }

    public override string ToString() {
        return $"Name: {Name}, Description: {Description}, Path: {Path}";
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
