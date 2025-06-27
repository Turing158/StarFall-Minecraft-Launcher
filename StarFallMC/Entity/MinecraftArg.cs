namespace StarFallMC.Entity;

public class MinecraftArg {
    public string username { get; set; }
    public string version { get; set; }
    public string gameDir { get; set; }
    public string assetsDir { get; set; }
    public string assetIndex { get; set; }
    public string uuid { get; set; }
    public string accessToken { get; set; }
    public string userType { get; set; }
    public string versionType { get; set; }
    public string width { get; set; }
    public string height { get; set; }
    public bool fullscreen { get; set; }

    public MinecraftArg(string username, string version, string gameDir, string assetsDir, string assetIndex, string uuid, string accessToken, string userType, string versionType, string width, string height, bool fullscreen) {
        this.username = username;
        this.version = version;
        this.gameDir = gameDir;
        this.assetsDir = assetsDir;
        this.assetIndex = assetIndex;
        this.uuid = uuid;
        this.accessToken = accessToken;
        this.userType = userType;
        this.versionType = versionType;
        this.width = width;
        this.height = height;
        this.fullscreen = fullscreen;
    }
}