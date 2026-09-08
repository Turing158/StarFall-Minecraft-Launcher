using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using fNbt;
using StarFallMC.Util;

namespace StarFallMC.Entity.Resource;

public class SavesResource : INotifyPropertyChanged {
    private readonly object _iconLoadGate = new();
    private ImageSource _icon = ImageLoader.Placeholder;
    private string _iconPath = string.Empty;
    private string? _completedIconSource;
    private string? _loadingIconSource;
    private Task<ImageSource>? _iconLoadTask;

    public NbtCompound nbt { get; set; } = new();

    public string WorldName {
        get => nbt.TryGet("LevelName", out NbtString? levelName) ? levelName?.Value ?? string.Empty : string.Empty;
    }

    private string _dirName = string.Empty;
    public string DirName {
        get => _dirName;
        set => SetField(ref _dirName, value);
    }

    private string _path = string.Empty;
    public string Path {
        get => _path;
        set => SetField(ref _path, value);
    }

    public string IconPath {
        get => _iconPath;
        set {
            if (SetField(ref _iconPath, value ?? string.Empty)) {
                ResetIconLoad();
            }
        }
    }

    public ImageSource Icon {
        get => _icon;
        private set => SetField(ref _icon, value);
    }

    private string _refreshDate = string.Empty;
    public string RefreshDate {
        get => _refreshDate;
        set => SetField(ref _refreshDate, value);
    }

    public SavesResource() {
    }

    public SavesResource(NbtCompound nbt, string dirName, string path, string refreshDate) {
        this.nbt = nbt;
        DirName = dirName;
        Path = path;
        RefreshDate = refreshDate;
    }

    public async Task EnsureIconLoadedAsync(CancellationToken cancellationToken = default) {
        var source = IconPath;
        if (string.IsNullOrWhiteSpace(source)) {
            return;
        }

        Task<ImageSource> loadTask;
        lock (_iconLoadGate) {
            if (string.Equals(_completedIconSource, source, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            if (_iconLoadTask == null ||
                !string.Equals(_loadingIconSource, source, StringComparison.OrdinalIgnoreCase)) {
                _loadingIconSource = source;
                _iconLoadTask = Task.Run(() => ImageLoader.LoadLocal(source, 96, 96));
            }

            loadTask = _iconLoadTask;
        }

        var image = await loadTask.WaitAsync(cancellationToken);
        if (!string.Equals(source, IconPath, StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        lock (_iconLoadGate) {
            if (!string.Equals(source, IconPath, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

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

    internal void CopyIconStateFrom(SavesResource source) {
        IconPath = source.IconPath;
        Icon = source.Icon;
        lock (_iconLoadGate) {
            _completedIconSource = source._completedIconSource;
        }
    }

    public override bool Equals(object? obj) {
        return base.Equals(obj);
    }

    public override int GetHashCode() {
        return base.GetHashCode();
    }

    public override string ToString() {
        return $"SavesResource:(WorldName:{WorldName},DirName:{DirName},Path:{Path},Icon:{Icon},RefreshDate:{RefreshDate})";
    }

    private void ResetIconLoad() {
        lock (_iconLoadGate) {
            _completedIconSource = null;
            _loadingIconSource = null;
            _iconLoadTask = null;
        }

        Icon = ImageLoader.Placeholder;
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
