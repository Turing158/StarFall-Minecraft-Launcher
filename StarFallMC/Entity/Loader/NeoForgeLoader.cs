namespace StarFallMC.Entity.Loader;

public class NeoForgeLoader {
    public string RawVersion { get; set; }
    public string Mcversion { get; set; }
    public string Version { get; set; }
    public string DisplayName {
        get => RawVersion;
    }
}