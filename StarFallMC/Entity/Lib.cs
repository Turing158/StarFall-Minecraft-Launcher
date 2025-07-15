namespace StarFallMC.Entity;

public class Lib {
    public string name { get; set; }
            public string path { get; set; }
            public bool isNativeLinux { get; set; }
            public bool isNativeWindows { get; set; }
            public bool isNativeMacos { get; set; }
            public Download artifact { get; set; }
            public Dictionary<string,Download> classifiers { get; set; }

            public Lib() {
            }

            public Lib(string name, string path, bool isNativeLinux, bool isNativeWindows, bool isNativeMacos, Download artifact, Dictionary<string, Download> classifiers) {
                this.name = name;
                this.path = path;
                this.isNativeLinux = isNativeLinux;
                this.isNativeWindows = isNativeWindows;
                this.isNativeMacos = isNativeMacos;
                this.artifact = artifact;
                this.classifiers = classifiers;
            }

            public override bool Equals(object? obj) {
                if (obj is not Lib other) return false;
                return name == other.name &&
                       path == other.path &&
                       isNativeLinux == other.isNativeLinux &&
                       isNativeWindows == other.isNativeWindows &&
                       isNativeMacos == other.isNativeMacos &&
                       EqualityComparer<Download>.Default.Equals(artifact, other.artifact) &&
                       EqualityComparer<Dictionary<string, Download>>.Default.Equals(classifiers, other.classifiers);
            }
            
            public override int GetHashCode() {
                return HashCode.Combine(name, path, isNativeLinux, isNativeWindows, isNativeMacos, artifact, classifiers);
            }
            
            public override string ToString() {
                return $"Lib(name: {name}, path: {path},\n " +
                       $"isNativeLinux: {isNativeLinux}, isNativeWindows: {isNativeWindows}, isNativeMacos: {isNativeMacos},\n " +
                       $"artifact: {artifact},\n " +
                       $"classifiers: {string.Join(", ", classifiers.Select(kv => $"{kv.Key}: {kv.Value}"))})";
            }
}