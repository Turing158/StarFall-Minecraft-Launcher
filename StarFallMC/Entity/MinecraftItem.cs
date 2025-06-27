namespace StarFallMC.Entity;

public class MinecraftItem {
    public string Name { get; set; }
    public string Version { get; set; }
    public string Path { get; set; }
    public string Icon { get; set; }
    
    public MinecraftItem(){}
    
    public MinecraftItem(string name, string version, string path, string icon) {
        Name = name;
        Version = version;
        Path = path;
        Icon = icon;
    }

    public override string ToString() {
        return "Name:" +Name + "\tVersion:" + Version + "\tPath:" + Path + "\tIcon:" + Icon;
    }
    
    public override bool Equals(object? obj) {
        if (obj is MinecraftItem other) {
            return other.Name == Name && other.Version == Version && other.Path == Path && other.Icon == Icon;
        }
        return false;
    }
    public override int GetHashCode() {
        return HashCode.Combine(Name, Version, Path, Icon);
    }
}