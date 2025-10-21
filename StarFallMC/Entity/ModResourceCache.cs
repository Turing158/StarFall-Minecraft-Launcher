using StarFallMC.Entity.Resource;

namespace StarFallMC.Entity;

public class ModResourceCache {
    public string SearchText{ get; set; } = string.Empty;
    public string SelectedLoader { get; set; } = "全部";
    public string SelectedVersion { get; set; } = "全部";
    public string SelectedCategory { get; set; } = "全部";
    public bool UseCurseForge { get; set; } = false;
    public List<ModResource> List { get; set; } = new();
    public int TotalCount { get; set; } = 0;
    public int CurrentPage { get; set; } = 1;
}