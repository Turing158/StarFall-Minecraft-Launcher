namespace StarFallMC.Entity;

public class Download {
    public string path { get; set; }
    public string url { get; set; }
    public string sha1 { get; set; }
    public int size { get; set; }

    public Download() {
    }

    public Download(string path, string url, string sha1, int size) {
        this.path = path;
        this.url = url;
        this.sha1 = sha1;
        this.size = size;
    }
            
    public override bool Equals(object? obj) {
        if (obj is not Download other) return false;
        return path == other.path &&
               url == other.url &&
               sha1 == other.sha1 &&
               size == other.size;
    }
            
    public override int GetHashCode() {
        return HashCode.Combine(path, url, sha1, size);
    }
            
    public override string ToString() {
        return $"Download(path: {path}, url: {url}, sha1: {sha1}, size: {size})";
    }
}