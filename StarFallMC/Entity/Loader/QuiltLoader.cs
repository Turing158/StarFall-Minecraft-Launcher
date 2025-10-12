namespace StarFallMC.Entity.Loader;

public class QuiltLoader {
    public string Version { get; set; }
    public string Build { get; set; }
    public string Maven { get; set; }
    public string Mcversion { get; set; }
    public string DisplayName {
        get => $"quilt-loader:{Version}";
    }
}