using System.IO;
using System.Windows.Media.Imaging;

namespace StarFallMC.Entity.Resource;

public class TexturePackResource {
    public string Name { get; set; }
    public string Description { get; set; }
    public string Path { get; set; }

    public BitmapImage Icon {
        get {
            BitmapImage bitmap = new();
            if (!string.IsNullOrEmpty(IconPath) && File.Exists(IconPath)) {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(IconPath);
                bitmap.EndInit();
            }
            return bitmap;
        }
    }

    public string IconPath { get; set; }

    public override string ToString() {
        return $"Name: {Name}, Description: {Description}, Path: {Path}";
    }
}