namespace StarFallMC.Entity;

public class JavaItem {
    public string Name { get; set; }
    public string Path { get; set; }
    public string Version { get; set; }

    public JavaItem(){}
    
    public JavaItem(string name, string path, string version) {
        Name = name;
        Path = path;
        Version = version;
    }
    
    public override string ToString() {
        return $"JavaItem : {Name} ({Path})";
    }

    public override bool Equals(object? obj) {
        if (obj is JavaItem other) {
            return other.Name == Name && other.Path == Path && other.Version == Version;
        }
        return false;
    }

    public override int GetHashCode() {
        return HashCode.Combine(Name, Path, Version);
    }
}