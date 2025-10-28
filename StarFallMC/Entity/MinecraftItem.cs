namespace StarFallMC.Entity;

public class MinecraftItem {
    public string Name { get; set; }
    public string Loader { get; set; }
    public string Path { get; set; }
    public string Icon { get; set; }
    
    public MinecraftItem(){}
    
    public MinecraftItem(string name, string loader, string path, string icon) {
        Name = name;
        Loader = loader;
        Path = path;
        Icon = icon;
    }

    public override string ToString() {
        return "Name:" +Name + "\tLoader:" + Loader + "\tPath:" + Path + "\tIcon:" + Icon;
    }
    
    public override bool Equals(object? obj) {
        if (obj is MinecraftItem other) {
            return other.Name == Name && other.Loader == Loader && other.Path == Path && other.Icon == Icon;
        }
        return false;
    }
    public override int GetHashCode() {
        return HashCode.Combine(Name, Loader, Path, Icon);
    }
    
    public MinecraftItem Clone() {
        return new MinecraftItem(Name, Loader, Path, Icon);
    }
}