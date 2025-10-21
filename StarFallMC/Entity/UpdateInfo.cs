namespace StarFallMC.Entity;

public class UpdateInfo {
    public string Version { get; set; } = string.Empty;
    public string Contents { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string UpdateDate { get; set; } = string.Empty;
    public string UpdateUrl { get; set; } = string.Empty;
    public string DefaultUpdateUrl { get; set; } = "https://github.com/Turing158/StarFall-Minecraft-Launcher";

    public override string ToString() {
        return $"Version: {Version}, Contents: {Contents}, Title: {Title}, UpdateDate: {UpdateDate}";
    }
}