using StarFallMC.Entity.Enum;

namespace StarFallMC.Entity;

public class ModPack {
    public string Name { get; set; }
    public string Version { get; set; }

    public string VersionName {
        get => $"{Name} {Version}";
    }
    public List<DownloadFile> Files { get; set; }
    public string MinecraftVersion { get; set; }
    public MinecraftLoader Loader { get; set; }
    public string LoaderVersion { get; set; }

    public override string ToString() {
        return $"name: {Name}, version: {Version}, minecraftVersion: {MinecraftVersion}, loader: {Loader} {LoaderVersion}, files total: {Files.Count}";
    }
}