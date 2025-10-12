namespace StarFallMC.Entity.Loader;

public class ForgeLoader {
    public string Build { get; set; }
    public string Version { get; set; }
    public string Mcversion { get; set; }
    public DateTime Modified { get; set; }

    public string DisplayName {
        get => $"forge-{Mcversion}-{Version}";
    }
}