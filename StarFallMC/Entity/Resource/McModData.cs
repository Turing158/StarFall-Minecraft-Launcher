namespace StarFallMC.Entity.Resource;

public class McModData {
    public int WikiId { get; set; }
    public string ModrinthSlug { get; set; }
    public string CurseForgeSlug { get; set; }
    public string AllName { get; set; }
    public string[] ChineseName {
        get => !string.IsNullOrEmpty(AllName) ? AllName.Split("(")[0].Split("/") : Array.Empty<string>();
    }

    public string[] EnglishName {
        get => !string.IsNullOrEmpty(AllName) ? AllName.Split("(")[1].Split(")")[0].Split("/") : Array.Empty<string>();
    }

    public override string ToString() {
        return $"WikiId: {WikiId}, ModrinthSlug: {ModrinthSlug}, CurseForgeSlug: {CurseForgeSlug}, AllName: {AllName}";
    }
}