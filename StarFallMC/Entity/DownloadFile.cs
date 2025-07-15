namespace StarFallMC.Entity;

public class DownloadFile {
    public string Name { get; set; }
    public string FilePath { get; set; }
    public string UrlPath { get; set; }

    public DownloadFile() {
    }

    public DownloadFile(string name, string filePath, string urlPath) {
        Name = name;
        FilePath = filePath;
        UrlPath = urlPath;
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
        return $"Name: {Name}, FilePath: {FilePath}, UrlPath: {UrlPath}";
    }
}