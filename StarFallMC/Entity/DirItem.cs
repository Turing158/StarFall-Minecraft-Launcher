namespace StarFallMC.Entity;

public class DirItem {
    public string Name { get; set; }
    public string Path { get; set; }

    public DirItem() { }
    
    public DirItem(string name, string path) {
        Name = name;
        Path = path;
    }

    public override string ToString() {
        return $"DirItem : {Name} ({Path})";
    }

    public override bool Equals(object? obj) {
        if (obj is DirItem other) {
            return other.Name == Name && other.Path == Path;
        }
        return false;
    }

    public override int GetHashCode() {
        return HashCode.Combine(Name, Path);
    }
}