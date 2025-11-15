using StarFallMC.Entity.Enum;

namespace StarFallMC.Entity;

public class LibRule{
    public bool IsAllow { get;set; }
    public DeviceOs Os { get;set; }

    public override bool Equals(object? obj) {
        return obj is LibRule other &&
               IsAllow == other.IsAllow &&
               Os == other.Os;
    }

    public override int GetHashCode() {
        return HashCode.Combine(IsAllow, Os);
    }

    public override string ToString() {
        return $"LibRule(IsAllow: {IsAllow}, Os: {Os})";
    }
}