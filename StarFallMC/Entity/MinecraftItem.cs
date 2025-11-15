using StarFallMC.Entity.Enum;

namespace StarFallMC.Entity;

public class MinecraftItem {
    public string Name { get; set; }
    public MinecraftLoader Loader { get; set; }

    public string LoaderName {
        get => Loader switch {
            MinecraftLoader.Minecraft => "Minecraft",
            MinecraftLoader.Forge => "Forge",
            MinecraftLoader.Fabric => "Fabric",
            MinecraftLoader.Quilt => "Quilt",
            MinecraftLoader.NeoForge => "NeoForge",
            MinecraftLoader.LiteLoader => "LiteLoader",
            MinecraftLoader.Optifine => "Optifine",
            _ => "Unknown"
        };
    }
    public string Path { get; set; }
    public string Icon { get; set; }
    
    public MinecraftItem(){}
    
    public MinecraftItem(string name, MinecraftLoader loader, string path, string icon) {
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