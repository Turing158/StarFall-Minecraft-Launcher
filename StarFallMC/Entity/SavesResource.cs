using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using fNbt;

namespace StarFallMC.Entity;

public class SavesResource : INotifyPropertyChanged {
    public NbtCompound nbt { get; set; }

    public string WorldName {
        get => nbt.TryGet("LevelName",out NbtString levelName) ? levelName.Value : string.Empty;
    }

    private string _dirName;
    public string DirName {
        get => _dirName;
        set => SetField(ref _dirName, value);
    }
    private string _path;
    public string Path {
        get => _path;
        set => SetField(ref _path, value);
    }

    public string IconPath {
        get {
            var iconPath = System.IO.Path.Combine(Path, "icon.png");
            return File.Exists(iconPath) ? iconPath : string.Empty;
        }
    }
    
    public BitmapImage Icon {
        get {
            BitmapImage bitmapImage = new BitmapImage();
            if (!File.Exists(IconPath)) {
                return bitmapImage;
            }
            bitmapImage.BeginInit();
            bitmapImage.UriSource = new Uri(IconPath, UriKind.RelativeOrAbsolute);
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmapImage.EndInit();
            return bitmapImage;
        }
    }
    private string _refreshDate;
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

    public override bool Equals(object? obj) {
        return base.Equals(obj);
    }

    public override int GetHashCode() {
        return base.GetHashCode();
    }

    public override string ToString() {
        return $"SavesResource:(WorldName:{WorldName},DirName:{DirName},Path:{Path},Icon:{Icon},RefreshDate:{RefreshDate})";
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