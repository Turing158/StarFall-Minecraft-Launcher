namespace StarFallMC.Entity;

public class Lib {
    public string name { get; set; }

    public string nameOutVersion {
        get {
            var list = name.Split(":");
            return string.Join(":", list.Take(list.Length - 1));
        }
    }
    public string nameLast {
        get {
            var list = name.Split(":");
            return list[list.Length - 1];
        }
    }
    
    public string path { get; set; }
    public List<LibRule> rules { get;set; } = new ();
    public Download artifact { get; set; }
    public Dictionary<string,Download> classifiers { get; set; }
    public Lib() {
    }
    public override bool Equals(object? obj) {
        if (obj is not Lib other) return false;
        return name == other.name &&
               path == other.path;
    }
    public override int GetHashCode() {
        return HashCode.Combine(name, path, artifact, classifiers);
    }
    public override string ToString() {
        return $"Lib(name: {name}, path: {path},\n " +
               // $"isNativeLinux: {isNativeLinux}, isNativeWindows: {isNativeWindows}, isNativeMacos: {isNativeMacos},\n " +
               $"rules: {string.Join(", ", rules.Select(r => $"{r.Os}: {r.IsAllow}"))},\n " +
               $"artifact: {artifact},\n " +
               $"classifiers: {string.Join(", ", classifiers.Select(kv => $"{kv.Key}: {kv.Value}"))})";
    }
}