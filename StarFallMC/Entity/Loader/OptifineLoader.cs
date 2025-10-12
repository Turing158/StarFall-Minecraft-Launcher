namespace StarFallMC.Entity.Loader;

public class OptifineLoader {
    public string Mcversion { get; set; }
    public string Type { get; set; }
    public string Patch { get; set; }
    public ForgeLoader NeedForge { get; set; }
    public string DisplayName {
        get => $"optifine_{Mcversion}_{Type}_{Patch}";
    }

    public override bool Equals(object? obj) {
        return obj is OptifineLoader loader &&
               Mcversion == loader.Mcversion &&
               Type == loader.Type &&
               Patch == loader.Patch &&
               EqualityComparer<ForgeLoader>.Default.Equals(NeedForge, loader.NeedForge);
    }

    public override int GetHashCode() {
        return HashCode.Combine(Mcversion, Type, Patch, NeedForge);
    }

    public override string ToString() {
        return DisplayName;
    }
}