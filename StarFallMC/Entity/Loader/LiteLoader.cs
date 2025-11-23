namespace StarFallMC.Entity.Loader;

public class LiteLoader {
    public string Version { get; set; }
    public string Mcversion { get; set; }
    public long Timestamp { get; set; }
    public bool IsStable { get; set; }
    public string DisplayName {
        get => $"liteloader-{Version}";
    }
}