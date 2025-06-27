namespace StarFallMC.Entity;

public class JvmArg {
    public string currentDir { get; set; }
    public string versionName { get; set; }
    public string nativesDirectory { get; }
    public string launcherName { get; set; }
    public string launcherVersion { get; set; }
    public string classpath { get; set; }
    public string libraryDirectory { get; }
    public string primaryJarName { get; }

    public JvmArg(string currentDir, string versionName, string launcherName, string launcherVersion, string classpath) {
        this.currentDir = currentDir;
        this.versionName = versionName;
        this.launcherName = launcherName;
        this.launcherVersion = launcherVersion;
        this.classpath = classpath;
        nativesDirectory = $"{currentDir}/versions/{versionName}/{versionName}-natives";
        libraryDirectory = $"{currentDir}/libraries";
        primaryJarName = $"{versionName}.jar";
    }
}