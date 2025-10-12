namespace StarFallMC.Entity;

public class MinecraftDownloader {

    public string Name{ get; set; }
    public string Type{ get; set; }
    public string Description{ get; set; }
    public DownloadFile Downloader{ get; set; }
    
    public override bool Equals(object? obj) {
        return obj is MinecraftDownloader downloader &&
               Name == downloader.Name &&
               Type == downloader.Type &&
               Description == downloader.Description &&
               EqualityComparer<DownloadFile>.Default.Equals(Downloader, downloader.Downloader);
    }

    public override int GetHashCode() {
        return HashCode.Combine(Name, Type, Description, Downloader);
    }

    public override string ToString() {
        return $"MinecraftDownloader(Name={Name}, Type={Type}, Description={Description}, Downloader={Downloader})";
    }
}