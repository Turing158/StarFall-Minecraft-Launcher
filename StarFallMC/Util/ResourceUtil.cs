using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using fNbt;
using Newtonsoft.Json.Linq;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.Entity.Loader;
using StarFallMC.Entity.Resource;
using StarFallMC.SettingPages;

namespace StarFallMC.Util;

public class ResourceUtil {

    public static List<ModResource> LocalModResources;
    public static List<SavesResource> LocalSavesResources;
    public static List<TexturePackResource> LocalTexturePackResources;
    
    public static List<MinecraftDownloader> LatestType = new ();
    public static List<MinecraftDownloader> ReleaseType = new ();
    public static List<MinecraftDownloader> SnapshotType = new ();
    public static List<MinecraftDownloader> AprilFoolsType = new ();
    public static List<MinecraftDownloader> OldType = new ();

    public static bool IsNeedInitDownloader() {
        return LatestType == null || ReleaseType == null || SnapshotType == null || AprilFoolsType == null || OldType == null ||
               LatestType.Count == 0 || ReleaseType.Count == 0 || SnapshotType.Count == 0 || AprilFoolsType.Count == 0 || OldType.Count == 0;
    }
    
    public static void ClearDownloader() {
        LatestType?.Clear();
        ReleaseType?.Clear();
        SnapshotType?.Clear();
        AprilFoolsType?.Clear();
        OldType?.Clear();
    }
    
    public static void ClearLocalResources() {
        LocalModResources?.Clear();
        LocalSavesResources?.Clear();
        LocalTexturePackResources?.Clear();
    }

    public static MinecraftItem GetMinecraftItem() {
        var hvm = Home.GetViewModel?.Invoke();
        if (hvm == null) {
            return null;
        }

        var currentGame = hvm.CurrentGame;
        return string.IsNullOrEmpty(currentGame.Path) ? null : currentGame;
    }

    public static async Task<List<ModDownloader>> GetModDownloaderByModrinth(ModResource modResource, CancellationToken ct) {
        List<ModDownloader> downloaders = new();
        try {
            //通过Modrinth的API获取模组下载信息
            //GET https://api.modrinth.com/v2/project/:ModrinthProjectId/version
            var modrinth = await HttpRequestUtil.Get($"https://api.modrinth.com/v2/project/{modResource.ModrinthProjectId}/version");
            if (modrinth.IsSuccess) {
                try {
                    var fabricApiJson = JArray.Parse(modrinth.Content);
                    foreach (var i in fabricApiJson) {
                        ct.ThrowIfCancellationRequested();
                        var gameVersions = i["game_versions"].ToObject<List<string>>();
                        var loaders = i["loaders"].ToObject<List<string>>();
                        foreach (var j in i["files"] as JArray) {
                            var downloader = new ModDownloader() {
                                Name = Path.GetFileNameWithoutExtension(j["filename"]?.ToString() ?? ""),
                                Version = i["version_number"]?.ToString(),
                            };
                            downloader.McVersion.AddRange(gameVersions);
                            downloader.ModLoader.AddRange(loaders);
                            try {
                                downloader.Date = DateTime.Parse(i["date_published"]?.ToString()).ToString("yyyy-MM-dd HH:mm:ss");
                            }
                            catch (Exception e){
                                Console.WriteLine(e);
                            }
                            downloader.File = new DownloadFile() {
                                Name = j["filename"]?.ToString(),
                                Sha1 = j["sha1"]?.ToString(),
                                UrlPath = j["url"]?.ToString(),
                                Size = long.Parse(j["size"]?.ToString() ?? "1"),
                            };
                            downloaders.Add(downloader);
                        }
                    }
                }
                catch (Exception e) {
                    Console.WriteLine($"Modrinth获取下载列表失败：{e}");
                }
            }
            else {
                Console.WriteLine($"获取Modrinth模组 {modResource.ModrinthProjectId} 版本信息失败");
            }
        }
        catch (Exception e) {
            Console.WriteLine(e);
        }
        return downloaders;
    }
    
    public static async Task<List<ModDownloader>> GetModDownloaderByCurseForge(ModResource modResource, CancellationToken ct) {
        List<ModDownloader> downloaders = new();
        var modLoader = new HashSet<string> {
            "Forge",
            "NeoForge",
            "Fabric",
            "Quilt",
        };
        try {
            if (modResource.CurseForgeId == 0) {
                var list = await GetCurseForgeArgs(new List<ModResource> {modResource},null,0,1,true);
                if (list.Count == 0) {
                    return downloaders;
                }
                modResource.CurseForgeId = list[0].CurseForgeId;
            }
            bool hasMore = true;
            while (hasMore) {
                var curseForge = await HttpRequestUtil.Get(
                    $"https://api.curseforge.com/v1/mods/{modResource.CurseForgeId}/files",
                    args: new Dictionary<string, Object> {
                        {"index", 0 * 100},
                        {"pageSize", 100},
                    },
                    headers: new Dictionary<string, string> {
                        {"X-API-KEY", KeyUtil.CURSEFORGE_API_KEY}
                    });
                if (curseForge.IsSuccess) {
                    var root = JObject.Parse(curseForge.Content);
                    foreach (var i in root["data"] as JArray) {
                        var gameVersions = i["gameVersions"].ToObject<List<string>>();
                        var versions = gameVersions.Where(x => Regex.IsMatch(x, @"\d")).ToList();
                        var loaders = gameVersions.Where(x => modLoader.Contains(x)).ToList();
                        var downloader = new ModDownloader() {
                            Name = Path.GetFileNameWithoutExtension(i["fileName"]?.ToString() ?? ""),
                        };
                        downloader.McVersion.AddRange(versions);
                        downloader.ModLoader.AddRange(loaders);
                        try {
                            downloader.Date = DateTime.Parse(i["fileDate"]?.ToString()).ToString("yyyy-MM-dd HH:mm:ss");
                        }
                        catch (Exception e){
                            Console.WriteLine(e);
                        }
                        downloader.File = new DownloadFile() {
                            Name = i["fileName"]?.ToString(),
                            Sha1 = i["hashes"][1]["value"]?.ToString(),
                            UrlPath = i["downloadUrl"]?.ToString(),
                            Size = long.Parse(i["fileLength"]?.ToString() ?? "1"),
                        };
                        downloaders.Add(downloader);
                    }

                    if (downloaders.Count >= root["pagination"]?["totalCount"]?.ToObject<int>()) {
                        hasMore = false;
                    }
                }
                else {
                    Console.WriteLine($"CurseForge获取下载列表失败：{curseForge.ErrorMessage}");
                }
            }
        }
        catch (Exception e) {
            Console.WriteLine($"CurseForge获取下载列表失败：{e}");
        }
        return downloaders;
    }

    public static async Task<(List<ForgeLoader>, List<LiteLoader>, List<NeoForgeLoader>, List<OptifineLoader>, List<FabricLoader>, List<ModResource>, List<QuiltLoader>)> GetAllLoaderByMinecraftDownloader(string version, CancellationToken ct, IProgress<int> progress = null) {
        var forgeLoaders = new List<ForgeLoader>();
        var liteLoaders = new List<LiteLoader>();
        var neoForgeLoaders = new List<NeoForgeLoader>();
        var optifineLoaders = new List<OptifineLoader>();
        var fabricLoaders = new List<FabricLoader>();
        var fabricApiVersions = new List<ModResource>();
        var quiltLoaders = new List<QuiltLoader>();
        
        try {
            progress.Report(1);
            ct.ThrowIfCancellationRequested();
            //获取Forge列表，返回Json的数组，数组为空则为Forge不支持该版本
            //需要单独Object的build[下载forge标识之一]，version[forge版本]，mcversion[游戏版本]和modified[时间字段]
            //GET https://bmclapi2.bangbang93.com/forge/minecraft/:id
            var forgeResult = await HttpRequestUtil.Get($"https://bmclapi2.bangbang93.com/forge/minecraft/{version}");
            if (forgeResult.IsSuccess) {
                try {
                    var forgeJson = JArray.Parse(forgeResult.Content);
                    foreach (var item in forgeJson) {
                        ct.ThrowIfCancellationRequested();
                        forgeLoaders.Add(new ForgeLoader {
                            Build = item["build"]?.ToString(),
                            Version = item["version"]?.ToString(),
                            Mcversion = item["mcversion"]?.ToString(),
                            Modified = DateTime.Parse(item["modified"]?.ToString())
                        });
                    }

                    forgeLoaders.Sort((a, b) => DateTime.Compare(b.Modified, a.Modified));
                }
                catch (Exception e) {
                    Console.WriteLine($"获取Forge列表失败：{e}");
                }
            }
            else {
                Console.WriteLine($"获取Forge列表失败：{forgeResult.ErrorMessage}");
            }
            ct.ThrowIfCancellationRequested();
            progress.Report(17);
            //获取Liteloader，返回Json，json为空则为Liteloader不支持该版本
            //需要version[Liteloader版本]，mcversion[游戏版本]和build-timestamp[时间字段]
            //GET https://bmclapi2.bangbang93.com/liteloader/list?mcversion=:id
            var liteloaderResult =
                await HttpRequestUtil.Get($"https://bmclapi2.bangbang93.com/liteloader/list?mcversion={version}");
            if (liteloaderResult.IsSuccess) {
                if (!string.IsNullOrEmpty(liteloaderResult.Content)) {
                    try {
                        var liteJson = JObject.Parse(liteloaderResult.Content);
                        liteLoaders.Add(new LiteLoader {
                            Version = liteJson["version"]?.ToString(),
                            Mcversion = liteJson["mcversion"]?.ToString(),
                            Timestamp = liteJson["build"]?["timestamp"]?.ToObject<long>() ?? 1,
                        });
                    }
                    catch (Exception e) {
                        Console.WriteLine($"获取Liteloader列表失败：{e}");
                    }

                    liteLoaders.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
                }
            }
            else {
                Console.WriteLine($"获取Liteloader列表失败：{liteloaderResult.ErrorMessage}");
            }
            ct.ThrowIfCancellationRequested();
            progress.Report(33);
            //获取Neoforge，返回Json的数组，数组为空则为Neoforge不支持该版本
            //需要单独Object的rawVersion[下载neoforge标识之一]，version[forge版本]，mcversion[游戏版本]
            //GET https://bmclapi2.bangbang93.com/neoforge/list/:id
            var neoforgeResult = await HttpRequestUtil.Get($"https://bmclapi2.bangbang93.com/neoforge/list/{version}");
            if (neoforgeResult.IsSuccess) {
                try {
                    var neoJson = JArray.Parse(neoforgeResult.Content);
                    foreach (var item in neoJson) {
                        ct.ThrowIfCancellationRequested();
                        neoForgeLoaders.Add(new NeoForgeLoader {
                            RawVersion = item["rawVersion"]?.ToString(),
                            Version = item["version"]?.ToString(),
                            Mcversion = item["mcversion"]?.ToString(),
                        });
                    }

                    neoForgeLoaders.Reverse();
                }
                catch (Exception e) {
                    Console.WriteLine($"获取Neoforge列表失败：{e}");
                }
            }
            else {
                Console.WriteLine($"获取Neoforge列表失败：{neoforgeResult.ErrorMessage}");
            }
            ct.ThrowIfCancellationRequested();
            progress.Report(49);
            //获取Optifine，返回Json的数组，数组为空则为Optifine不支持该版本
            //需要单独Object的mcversion[游戏版本]，patch[下载optifine标识之一]，type[Optifine版本类型]，forge[需要forge版本]
            //GET https://bmclapi2.bangbang93.com/optifine/:id
            var optifineResult = await HttpRequestUtil.Get($"https://bmclapi2.bangbang93.com/optifine/{version}");
            if (optifineResult.IsSuccess) {
                try {
                    var optJson = JArray.Parse(optifineResult.Content);
                    foreach (var i in optJson) {
                        ct.ThrowIfCancellationRequested();
                        var item = new OptifineLoader {
                            Mcversion = i["mcversion"]?.ToString(),
                            Type = i["type"]?.ToString(),
                            Patch = i["patch"]?.ToString(),
                        };
                        var forgeBuild = i["forge"];
                        if (forgeBuild != null && forgeBuild.ToString() != "Forge N/A") {
                            item.NeedForge =
                                forgeLoaders.FirstOrDefault(x => x.Build == forgeBuild.ToString().Split("#")[^1]);
                        }

                        optifineLoaders.Add(item);
                    }

                    optifineLoaders.Reverse();
                }
                catch (Exception e) {
                    Console.WriteLine($"获取Optifine列表失败：{e}");
                }
            }
            else {
                Console.WriteLine($"获取Optifine列表失败：{optifineResult.ErrorMessage}");
            }
            ct.ThrowIfCancellationRequested();
            progress.Report(65);
            //获取Fabric Loader，返回Json的数组，若404且code=COMMON_NO_SUCH_OBJECT
            //需要单独Object的loader-maven[fabric loader版本]
            //GET https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/:id
            var fabricLoaderResult =
                await HttpRequestUtil.Get($"https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{version}");
            if (fabricLoaderResult.IsSuccess) {
                try {
                    var fabricJson = JArray.Parse(fabricLoaderResult.Content);
                    foreach (var item in fabricJson) {
                        ct.ThrowIfCancellationRequested();
                        var loader = item["loader"];
                        if (loader != null) {
                            fabricLoaders.Add(new FabricLoader {
                                Version = loader["version"]?.ToString(),
                                Build = loader["build"]?.ToString(),
                                Maven = loader["maven"]?.ToString(),
                                Mcversion = version
                            });
                        }
                    }
                }
                catch (Exception e) {
                    Console.WriteLine($"获取Fabric Loader列表失败：{e}");
                }
            }
            else {
                Console.WriteLine($"获取Fabric Loader列表失败：{fabricLoaderResult.ErrorMessage}");
            }
            ct.ThrowIfCancellationRequested();
            progress.Report(72);
            if (fabricLoaders.Count > 0) {
                //获取FabricAPI，返回Json的数组，若fabric不支持该版本跳过这里
                //GET https://api.modrinth.com/v2/project/P7dR8mSH/version
                //获取到json的数组
                var fabricApiModrinthResult =
                    await HttpRequestUtil.Get("https://api.modrinth.com/v2/project/P7dR8mSH/version");
                if (fabricApiModrinthResult.IsSuccess) {
                    try {
                        var fabricApiJson = JArray.Parse(fabricApiModrinthResult.Content);
                        foreach (var i in fabricApiJson) {
                            ct.ThrowIfCancellationRequested();
                            var gameVersions = i["game_versions"].ToObject<List<string>>();

                            if (gameVersions == null || gameVersions.Contains(version)) {
                                var modResource = new ModResource() {
                                    DisplayName = i["version_number"]?.ToString().Split("+")[0],
                                    ResourceVersion = i["version_number"]?.ToString(),
                                };
                                foreach (var j in i["files"] as JArray) {
                                    
                                    var downloader = new ModDownloader() {
                                        Name = $"Fabric API {modResource.ResourceVersion}",
                                        Version = modResource.ResourceVersion,
                                    };
                                    downloader.McVersion.Add(version);
                                    downloader.ModLoader.Add("Fabric");
                                    try {
                                        downloader.Date = DateTime.Parse(i["date_published"]?.ToString()).ToString("yyyy-MM-dd HH:mm:ss");
                                    }
                                    catch (Exception e){
                                        Console.WriteLine(e);
                                    }
                                    downloader.File = new DownloadFile() {
                                        Name = j["filename"]?.ToString(),
                                        Sha1 = j["sha1"]?.ToString(),
                                        UrlPath = j["url"]?.ToString(),
                                        Size = long.Parse(j["size"]?.ToString() ?? "1"),
                                    };
                                    modResource.Downloaders.Add(downloader);
                                }

                                fabricApiVersions.Add(modResource);
                            }
                        }
                    }
                    catch (Exception e) {
                        Console.WriteLine($"Modrinth获取Fabric API列表失败：{e}");
                    }
                }
                else {
                    Console.WriteLine($"Modrinth获取Fabric API列表失败：{fabricApiModrinthResult.ErrorMessage}");
                }
                ct.ThrowIfCancellationRequested();
                if (fabricApiVersions.Count == 0) {
                    //若modrinth找不到该模组，则使用curseforge获取
                    //GET https://api.curseforge.com/v1/mods/306612/files
                    //headers需要添加X-API-KEY
                    var fabricApiCurseForgeResult = await HttpRequestUtil.Get(
                        "https://api.curseforge.com/v1/mods/306612/files",
                        headers: new Dictionary<string, string>() {
                            { "X-API-KEY", KeyUtil.CURSEFORGE_API_KEY }
                        });
                    if (fabricApiCurseForgeResult.IsSuccess) {
                        try {
                            var fabricApiCurseForgeJson = JObject.Parse(fabricApiCurseForgeResult.Content);
                            foreach (var i in fabricApiCurseForgeJson["data"] as JArray) {
                                ct.ThrowIfCancellationRequested();
                                var gameVersion = i["gameVersions"]?.ToObject<List<string>>();

                                if (gameVersion == null || gameVersion.Contains(version)) {
                                    var modResource = new ModResource() {
                                        DisplayName = i["displayName"]?.ToString().Split(" ")[^1].Split("+")[0],
                                        ResourceVersion = i["displayName"]?.ToString().Split(" ")[^1],
                                    };
                                    var downloader = new ModDownloader() {
                                        Name = $"Fabric API {modResource.ResourceVersion}",
                                        Version = modResource.ResourceVersion,
                                    };
                                    downloader.McVersion.Add(version);
                                    downloader.ModLoader.Add("Fabric");
                                    try {
                                        downloader.Date = DateTime.Parse(i["fileDate"]?.ToString()).ToString("yyyy-MM-dd HH:mm:ss");
                                    }
                                    catch (Exception e){
                                        Console.WriteLine(e);
                                    }
                                    downloader.File = new DownloadFile() {
                                        Name = i["fileName"]?.ToString(),
                                        Sha1 = i["hashes"] is JArray hashes && hashes.Count > 0
                                            ? hashes[0].ToString()
                                            : string.Empty,
                                        UrlPath = i["downloadUrl"]?.ToString(),
                                        Size = i["fileLength"]?.ToObject<long>() ?? 1,
                                    };
                                    modResource.Downloaders.Add(downloader);
                                    fabricApiVersions.Add(modResource);
                                }
                            }
                        }
                        catch (Exception e) {
                            Console.WriteLine($"CurseForge获取Fabric API列表失败：{e}");
                        }
                    }
                    else {
                        Console.WriteLine($"CurseForge获取Fabric API列表失败：{fabricApiCurseForgeResult.ErrorMessage}");
                    }
                }
            }
            ct.ThrowIfCancellationRequested();
            progress.Report(81);
            //暂时无法使用BMCLAPI获取Quilt Loader
            //需要单独Object的loader-maven[quilt loader版本]
            //GET https://meta.quiltmc.org/v3/versions/loader/:id
            var quiltLoaderResult = await HttpRequestUtil.Get($"https://meta.quiltmc.org/v3/versions/loader/{version}");
            if (quiltLoaderResult.IsSuccess) {
                try {
                    var quiltJson = JArray.Parse(quiltLoaderResult.Content);
                    foreach (var item in quiltJson) {
                        ct.ThrowIfCancellationRequested();
                        var loader = item["loader"];
                        if (loader != null) {
                            if ($"{loader["version"]}".Contains("beta")) {
                                continue;
                            }

                            quiltLoaders.Add(new QuiltLoader {
                                Version = loader["version"]?.ToString(),
                                Build = loader["build"]?.ToString(),
                                Maven = loader["maven"]?.ToString(),
                                Mcversion = version
                            });
                        }
                    }
                }
                catch (Exception e) {
                    Console.WriteLine($"获取Quilt Loader列表失败：{e}");
                }
            }
            else {
                Console.WriteLine($"获取Quilt Loader列表失败：{quiltLoaderResult.ErrorMessage}");
            }
            ct.ThrowIfCancellationRequested();
            progress.Report(96);
            progress.Report(97);
        }
        catch (Exception e){
            Console.WriteLine(e);
        }
        progress.Report(100);
        return (forgeLoaders, liteLoaders, neoForgeLoaders, optifineLoaders, fabricLoaders, fabricApiVersions ,quiltLoaders);
    }
    
    public static async Task GetMinecraftDownloader(CancellationToken ct, IProgress<int> progress = null) {
        progress.Report(5);
        try {
            ct.ThrowIfCancellationRequested();
            var result = await HttpRequestUtil.Get("https://bmclapi2.bangbang93.com/mc/game/version_manifest.json");
            ct.ThrowIfCancellationRequested();
            JObject root;
            if (result.IsSuccess) {
                var json = result.Content;
                root = JObject.Parse(json);
            }
            else {
                progress.Report(100);
                return;
            }
            ct.ThrowIfCancellationRequested();
            var latestType = new List<MinecraftDownloader>();
            var releaseType = new List<MinecraftDownloader>();
            var snapshotType = new List<MinecraftDownloader>();
            var aprilFoolsType = new List<MinecraftDownloader>();
            var oldType = new List<MinecraftDownloader>();
            progress.Report(6);
            ct.ThrowIfCancellationRequested();
            var latest = root["latest"];
            var release = string.Empty;
            var snapshot = string.Empty;
            if (latest != null) {
                release = latest["release"]?.ToString();
                snapshot = latest["snapshot"]?.ToString();
            }
            progress.Report(7);
            ct.ThrowIfCancellationRequested();
            var versions = root["versions"] as JArray;
            if (versions != null) {
                progress.Report(4);
                var AprilFoolsVersionName = new List<string>() {
                    "15w14a",
                    "1.RV-Pre1",
                    "3D Shareware v1.34",
                    "20w14infinite",
                    "22w13oneBlockAtATime",
                    "23w13a_or_b",
                    "24w14potato",
                    "25w14craftmine"
                };
                var ApriFlFoolsDescription = new List<string>() {
                    "2015 | 爱与抱抱更新 (The Love and Hugs Update)",
                    "2016 | 时尚更新 (Trendy Update)",
                    "2019 | Minecraft 3D - 20世纪90年代电子游戏",
                    "2020 | 终极内容更新 (Ultimate Content Update)",
                    "2022 | 一次一个方块更新 (One Block at a Time Update)",
                    "2023 | 投票更新 (The Vote Update)",
                    "2024 | 毒马铃薯更新 (Poisonous Potato Update)",
                    "2025 | 探险和升级Minecraft (The Craftmine Update)"
                };
                progress.Report(8);
                ct.ThrowIfCancellationRequested();
                foreach (var i in versions) {
                    var name = i["id"]?.ToString();
                    var type = i["type"]?.ToString();
                    var url = i["url"]?.ToString();
                    var sha1 = url != null ? url.Split("/")[^2] : string.Empty;
                    var jsonReleaseTime = i["releaseTime"];
                    var releaseTime = jsonReleaseTime != null
                        ? DateTimeOffset.Parse(jsonReleaseTime.ToString()).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                        : string.Empty;
                    MinecraftDownloader downloader = new MinecraftDownloader() {
                        Name = name,
                        Type = type,
                        Description = releaseTime,
                        Downloader = new DownloadFile() {
                            Name = $"Minecraft {name}",
                            UrlPath = url,
                            Sha1 = sha1,
                            FileDate = releaseTime,
                        }
                    };
                    if (name == release) {
                        downloader.Description = $"最新发行正式版 | {releaseTime}";
                        latestType.Add(downloader);
                    }
                    else if (name == snapshot) {
                        downloader.Description = $"最新发行快照版 | {releaseTime}";
                        latestType.Add(downloader);
                    }
                    else if (AprilFoolsVersionName.Contains(name)) {
                        downloader.Description = ApriFlFoolsDescription[AprilFoolsVersionName.IndexOf(name)];
                        aprilFoolsType.Add(downloader);
                    }
                    else if (type == "release") {
                        releaseType.Add(downloader);
                    }
                    else if (type == "snapshot") {
                        snapshotType.Add(downloader);
                    }
                    else if (type.Contains("old_")) {
                        oldType.Add(downloader);
                    }
                    ct.ThrowIfCancellationRequested();
                }
                ct.ThrowIfCancellationRequested();
            }
            progress.Report(98);
            ct.ThrowIfCancellationRequested();
            LatestType.AddRange(latestType);
            ReleaseType.AddRange(releaseType);
            SnapshotType.AddRange(snapshotType);
            AprilFoolsType.AddRange(aprilFoolsType);
            OldType.AddRange(oldType);
            progress.Report(99);
        }
        catch (Exception e){
            Console.WriteLine(e);
        }
        progress.Report(100);
    }
    
    public static async Task<List<TexturePackResource>> GetTexturePack(CancellationToken ct,IProgress<int> progress = null) {
        List<TexturePackResource> resources = new();
        var item = GetMinecraftItem();
        if (item == null) {
            progress.Report(100);
            return resources;
        }
        string versionPath = item.Path;
        LocalModResources?.Clear();
        progress.Report(0);
        var gsvm = GameSetting.GetViewModel?.Invoke();
        bool isIsolate = gsvm == null
            ? PropertiesUtil.loadJson["gameArgs"]["isolation"].ToObject<bool>()
            : gsvm.IsIsolation;
        progress.Report(2);
        if (!isIsolate) {
            versionPath = Path.GetDirectoryName(Path.GetDirectoryName(versionPath));
        }
        string resourcePath = Path.Combine(versionPath, "resourcepacks");
        string tmpDir = Path.Combine(resourcePath,".tmp");
        if (!Directory.Exists(tmpDir)) {
            Directory.CreateDirectory(tmpDir);
        }
        try {
            ct.ThrowIfCancellationRequested();
            progress.Report(4);
            if (Directory.Exists(resourcePath)) {
                string[] files = Directory.GetFiles(resourcePath, "*.zip");
                foreach (var i in files) {
                    ct.ThrowIfCancellationRequested();
                    var resource = await GetTexturePack(i,tmpDir).ConfigureAwait(false);
                    resources.Add(resource);
                    progress.Report((int)Math.Round((double)(resources.Count) / files.Length * 98) + 4);
                }
            }
            if (resources.Count > 50) {
                MessageTips.Show("获取的材质包数量比较多，可能会造成一小会的卡顿");
            }
            progress.Report(99);
            LocalTexturePackResources = resources;
            
        }
        catch (Exception e) {
            
        }
        progress.Report(100);
        return resources;
    }

    private static async Task<TexturePackResource> GetTexturePack(string filePath,string tmpDir) {
        var resource = new TexturePackResource();
        await Task.Run(() => {
            FileInfo fileInfo = new FileInfo(filePath);
            resource.Name = Path.GetFileNameWithoutExtension(filePath);
            resource.Path = filePath;
            using (ZipArchive archive = ZipFile.OpenRead(filePath)) {
                ZipArchiveEntry iconEntry = archive.GetEntry("pack.png");
                if (iconEntry != null) {
                    using (Stream stream = iconEntry.Open()) {
                        string tmpIconPath = Path.Combine(tmpDir, resource.Name + ".png");
                        FileStream fileStream = new FileStream(tmpIconPath, FileMode.Create, FileAccess.Write);
                        stream.CopyTo(fileStream);
                        fileStream.Close();
                        resource.IconPath = tmpIconPath;
                    }
                }

                ZipArchiveEntry mateEntry = archive.GetEntry("pack.mcmeta");
                if (mateEntry != null) {
                    using (Stream stream = mateEntry.Open()) {
                        StreamReader reader = new StreamReader(stream);
                        string json = reader.ReadToEnd();
                        JObject root = JObject.Parse(json);
                        if (root["pack"]?["description"] != null) {
                            resource.Description = root["pack"]["description"].ToString().Replace("\n", " ");
                        }
                    }
                }
            }
        });
        return resource;
    }
    
    public static async Task<List<SavesResource>> GetSavesResource(CancellationToken ct,IProgress<int> progress = null) {
        List<SavesResource> resources = new();
        try {
            
            LocalSavesResources?.Clear();
            progress.Report(0);
            var gsvm = GameSetting.GetViewModel?.Invoke();
            bool isIsolate = gsvm == null
                ? PropertiesUtil.loadJson["gameArgs"]["isolation"].ToObject<bool>()
                : gsvm.IsIsolation;
            progress.Report(2);
            var mcItem = GetMinecraftItem();
            if (mcItem == null) {
                progress.Report(100);
                return resources;
            }
            string versionPath = mcItem.Path;
            ct.ThrowIfCancellationRequested();
            if (!isIsolate) {
                versionPath = Path.GetDirectoryName(Path.GetDirectoryName(versionPath));
            }

            progress.Report(5);
            string resourcePath = Path.Combine(versionPath, "saves");
            if (Directory.Exists(resourcePath)) {
                string[] files = Directory.GetDirectories(resourcePath);

                for (int i = 0; i < files.Length; i++) {
                    ct.ThrowIfCancellationRequested();
                    DirectoryInfo dirInfo = new DirectoryInfo(files[i]);
                    string levelDatPath = Path.Combine(dirInfo.FullName, "level.dat");
                    if (File.Exists(levelDatPath)) {
                        var item = new SavesResource();
                        item.DirName = dirInfo.Name;
                        item.Path = dirInfo.FullName;
                        item.RefreshDate = dirInfo.LastWriteTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                        NbtFile nbt = new NbtFile();
                        nbt.LoadFromFile(levelDatPath);
                        NbtCompound rootTag = nbt.RootTag;
                        if (rootTag.TryGet("Data", out NbtCompound dataTag)) {
                            item.nbt = dataTag;
                        }

                        resources.Add(item);
                    }

                    progress.Report((int)Math.Round((double)(i + 1) / files.Length * 100));
                }
            }
            if (resources.Count > 50) {
                MessageTips.Show("获取的地图数量比较多，可能会造成一小会的卡顿");
            }
            progress.Report(100);
            LocalSavesResources = resources;
            return resources;
        }
        catch (Exception e) {
            Console.WriteLine(e);
            MessageTips.Show("取消获取地图列表");
            LocalSavesResources = new List<SavesResource>();
            progress.Report(100);
            return resources;
        }
    }

    public static async Task<List<ModResource>> GetModResources(CancellationToken ct,IProgress<int> progress = null) {
        
        try {
            LocalModResources?.Clear();
            progress.Report(0);
            var gsvm = GameSetting.GetViewModel?.Invoke();
            bool isIsolate = gsvm == null
                ? PropertiesUtil.loadJson["gameArgs"]["isolation"].ToObject<bool>()
                : gsvm.IsIsolation;
            var mcItem = GetMinecraftItem();
            if (mcItem == null) {
                progress.Report(100);
                return null;
            }
            string versionPath = mcItem.Path;
            ct.ThrowIfCancellationRequested();
            progress.Report(5);
            List<ModResource> elementResource = await GetModResourcesElement(versionPath, isIsolate,progress,5,34);
            if (elementResource.Count == 0) {
                progress.Report(100);
                return elementResource;
            }
            progress.Report(35);
            ct.ThrowIfCancellationRequested();
            List<ModResource> modrinthResource = await GetModrinthArgs(elementResource,progress,36,61);
            ct.ThrowIfCancellationRequested();
            List<ModResource> notEntireModResources = new();
            List<ModResource> entireModResources = new();
            int progressCount = 0;
            progress.Report(62);
            foreach (var i in modrinthResource) {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(i.ResourceSource) || string.IsNullOrEmpty(i.WebsiteUrl) ||
                    string.IsNullOrEmpty(i.Description)) {
                    notEntireModResources.Add(i);
                }
                else {
                    entireModResources.Add(i);
                }
                progressCount++;
                progress.Report(62 + (int)Math.Round((double)(70 - 62) / modrinthResource.Count * progressCount));
                await Task.Delay(5);
            }
            if (notEntireModResources.Count == 0) {
                LocalModResources = modrinthResource;
                progress.Report(100);
                return modrinthResource;
            }
            ct.ThrowIfCancellationRequested();
            progress.Report(71);
            List<ModResource> curseForgeResource = await GetCurseForgeArgs(notEntireModResources,progress,71,90).ConfigureAwait(false);
            Console.WriteLine($"GetCurseForgeArgs 完成 {curseForgeResource.Count} 个模组");
            ct.ThrowIfCancellationRequested();
            List<ModResource> allModResources = new();
            allModResources.AddRange(entireModResources);
            allModResources.AddRange(curseForgeResource);
            Console.WriteLine($"GetModResources 合并完成 {allModResources.Count} 个模组");
            int NotFoundNumber = 0;
            ct.ThrowIfCancellationRequested();
            progress.Report(91);
            if (allModResources.Count > 50) {
                MessageTips.Show("获取的Mod数量比较多，可能会造成一小会的卡顿");
            }
            for (int i = 0; i < allModResources.Count; i++) {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(allModResources[i].ResourceSource) || string.IsNullOrEmpty(allModResources[i].WebsiteUrl) ||
                    string.IsNullOrEmpty(allModResources[i].Description)) {
                    if (string.IsNullOrEmpty(allModResources[i].DisplayName)) {
                        allModResources[i].DisplayName = allModResources[i].FileNameWithExtension;
                    }
                    NotFoundNumber++;
                }
                progress.Report(91 + (int)Math.Round((double)(98 - 91) / allModResources.Count * i));
                await Task.Delay(2);
            }
            LocalModResources = allModResources;
            progress.Report(99);
            await Task.Delay(1000);
            progress.Report(100);
            return allModResources;
        }
        catch (Exception e){
            Console.WriteLine(e);
            MessageTips.Show("取消获取Mod列表");
            LocalModResources = new List<ModResource>();
            progress.Report(100);
            return LocalModResources;
        }
    }
    
    private static async Task<List<ModResource>> GetModResourcesElement(string versionPath,bool isIsolate,IProgress<int> progress,int start,int end) {
        List<ModResource> resources = new();
        if (!isIsolate) {
            versionPath = Path.GetDirectoryName(Path.GetDirectoryName(versionPath));
        }
        string resourcePath = Path.Combine(versionPath, "mods");
        if (Directory.Exists(resourcePath)) {
            List<string> files = await Task.Run(()=> Directory.GetFiles(resourcePath, "*.jar", SearchOption.AllDirectories).ToList());
            Console.WriteLine($"获取到{files.Count}个Mods资源");
            using SHA1 sha1 = SHA1.Create();
            int filesIndex = 0;
            foreach (var i in files) {
                filesIndex++;
                resources.Add(await GetModResource(i,sha1).ConfigureAwait(false));
                int progressCount = start + (int)((double)filesIndex / files.Count * (end - start));
                progress.Report(progressCount);
            }
        }
        return resources;
    }

    private static async Task<ModResource> GetModResource(string filePath,SHA1 sha1) {
        using FileStream file = File.OpenRead(filePath);
        ModResource resource = new ModResource();
        FileInfo fileInfo = new FileInfo(filePath);
        resource.FilePath = filePath;
        if (Path.GetFileName(Path.GetDirectoryName(filePath)) == ".disabled") {
            resource.Disabled = true;
        }
        // string hashStr = $"{fileInfo.Name}-{fileInfo.LastWriteTime.ToLongTimeString()}-{fileInfo.Length}-C";
        await Task.Run(() => {
            resource.ModrinthSha1 = BitConverter.ToString(sha1.ComputeHash(file)).Replace("-", "").ToLower();
            resource.CurseForgeSha1 = GetMurmurHash2(filePath);
        });
        await Task.Run(async () => {
            using (ZipArchive zipArchive = ZipFile.OpenRead(filePath)) {
                ZipArchiveEntry entry = zipArchive.GetEntry("META-INF/mods.toml");
                if (entry != null) {
                    using (StreamReader reader = new StreamReader(entry.Open())){
                        string modsToml = await reader.ReadToEndAsync();
                        Dictionary<string, string> modInfo =
                            GetModstomlValue(modsToml, "mods", ["version", "displayName", "authors"]);
                        resource.ResourceVersion = modInfo.Keys.Contains("version") ? modInfo["version"] : "";
                        resource.DisplayName = modInfo.Keys.Contains("displayName") ? modInfo["displayName"] : "";
                        resource.Author = modInfo.Keys.Contains("authors") ? modInfo["authors"] : "";
                    }
                }
                if (string.IsNullOrEmpty(resource.ResourceVersion) ||
                    resource.ResourceVersion == "${file.jarVersion}") {
                    ZipArchiveEntry MFentry = zipArchive.GetEntry("META-INF/MANIFEST.MF");
                    if (MFentry != null) {
                        using (StreamReader reader = new StreamReader(MFentry.Open())) {
                            string manifest = await reader.ReadToEndAsync();
                            string versionLine = manifest.Split("\n")
                                .FirstOrDefault(i => i.StartsWith("Implementation-Version:"));
                            if (!string.IsNullOrEmpty(versionLine)) {
                                resource.ResourceVersion = versionLine.Split(":")[1].Trim();
                            }
                        }
                    }
                }
            }
        });
        return resource;
    }
    
    private static Dictionary<string,string> GetModstomlValue(string content,string titleKey,string[] valueKeys) {
        string[] lines = content.Split('\n');
        bool inTitleSection = false;
        Dictionary<string, string> value = new Dictionary<string, string>();
        foreach (var line in lines) {
            string outLine = line;
            int extraIndex = line.IndexOf("#");
            if (extraIndex >= 0) {
                outLine = line.Substring(0, extraIndex);
            }
            string trimmedLine = outLine.Trim();
        
            if (trimmedLine == $"[[{titleKey}]]") {
                inTitleSection = true;
                continue;
            }
        
            if (inTitleSection && trimmedLine.StartsWith("[[")) {
                break;
            }
        
            if (inTitleSection) {
                foreach (var key in valueKeys) {
                    if (trimmedLine.StartsWith($"{key}=")) {
                        value[key] = GetValueFromLine(trimmedLine);
                        break;
                    }
                }
            }
        }
        return value;
    }
    
    private static string GetValueFromLine(string line) {
        int startIndex = line.IndexOf('"') + 1;
        int endIndex = line.LastIndexOf('"');
    
        if (startIndex > 0 && endIndex > startIndex) {
            return line.Substring(startIndex, endIndex - startIndex);
        }
        return string.Empty;
    }
    
    private static uint GetMurmurHash2(string filepath) {
        byte[] fileBytes = File.ReadAllBytes(filepath);
        List<byte> data = new List<byte>();
        foreach (byte b in fileBytes) {
            if (b == 9 || b == 10 || b == 13 || b == 32) continue;
            data.Add(b);
        }
        int length = data.Count;
        uint h = 1u ^ (uint)length;
        
        int i;
        for (i = 0; i <= length - 4; i += 4) {
            uint k = data[i] | 
                     ((uint)data[i + 1] << 8) | 
                     ((uint)data[i + 2] << 16) | 
                     ((uint)data[i + 3] << 24);
            
            k = (k * 0x5BD1E995u) & 0xFFFFFFFFu;
            k = k ^ (k >> 24);
            k = (k * 0x5BD1E995u) & 0xFFFFFFFFu;
            h = (h * 0x5BD1E995u) & 0xFFFFFFFFu;
            h = h ^ k;
        }
        switch (length - i) {
            case 3:
                h = h ^ (data[i] | ((uint)data[i + 1] << 8));
                h = h ^ ((uint)data[i + 2] << 16);
                h = (h * 0x5BD1E995u) & 0xFFFFFFFFu;
                break;
            case 2:
                h = h ^ (data[i] | ((uint)data[i + 1] << 8));
                h = (h * 0x5BD1E995u) & 0xFFFFFFFFu;
                break;
            case 1:
                h = h ^ data[i];
                h = (h * 0x5BD1E995u) & 0xFFFFFFFFu;
                break;
        }
            
        h = h ^ (h >> 13);
        h = (h * 0x5BD1E995u) & 0xFFFFFFFFu;
        h = h ^ (h >> 15);
        
        return h;
    }
    
    private class ModrinthPostProfileArg {
        public string ProjectId { get; set; }
        public string AuthorId { get; set; }
        public string VersionNumber { get; set; }
    }
    
    private static async Task<List<ModResource>> GetModrinthArgs(List<ModResource> resources,IProgress<int> progress,int start,int end) {
        var ModrinthSha1s = resources.Select(i => i.ModrinthSha1).ToList();
        //POST https://api.modrinth.com/v2/version_files
        //body {"hashes":["ModrinthSha1"],"algorithm": "sha1"}
        Dictionary<string, Object> body = new() {
            { "hashes", ModrinthSha1s},
            { "algorithm", "sha1" }
        };
        int progressCount = 0;
        int progressFirstEnd = start + (int)((end - start) /2);
        var modrinthProfileResult = await HttpRequestUtil.Post("https://api.modrinth.com/v2/version_files",body);
        Dictionary<string,ModrinthPostProfileArg> modrinthHashAndId = new ();
        if (modrinthProfileResult.IsSuccess) {
            var json = JObject.Parse(modrinthProfileResult.Content);
            int jsonIndex = 0;
            foreach (var (k,v) in json) {
                jsonIndex++;
                ModrinthPostProfileArg arg = new ModrinthPostProfileArg();
                arg.ProjectId = v["project_id"].ToString();
                arg.AuthorId = v["author_id"].ToString();
                arg.VersionNumber = v["version_number"].ToString();
                modrinthHashAndId[k] = arg;
                int hashIndex = resources.FindIndex(i => i.ModrinthSha1 == k);
                resources[hashIndex].ModrinthProjectId = arg.ProjectId;
                resources[hashIndex].ModrinthAuthorId = arg.AuthorId;
                string modVersion = resources[hashIndex].ResourceVersion;
                if (string.IsNullOrEmpty(modVersion) || modVersion != arg.VersionNumber) {
                    resources[hashIndex].ResourceVersion = arg.VersionNumber;
                }
                progressCount = start + (int)((double)jsonIndex / json.Count * (progressFirstEnd - start));
                progress?.Report(progressCount);
                await Task.Delay(5);
            }
        }
        else {
            progress?.Report(end);
            return resources;
        }
        progress?.Report(progressFirstEnd);
        //GET https://api.modrinth.com/v2/projects?ids=[ModrinthSha1,...]
        StringBuilder args = new StringBuilder();
        foreach (var i in modrinthHashAndId.Values) {
            args.Append($"\"{i.ProjectId}\",");
        }
        var modrinthProjectResult = await HttpRequestUtil.Get($"https://api.modrinth.com/v2/projects?ids=[{args.ToString().TrimEnd(',')}]");
        if (modrinthProjectResult.IsSuccess) {
            JArray modrinthProjectJArray = JArray.Parse(modrinthProjectResult.Content);
            int modrinthProjectJArrayIndex = 0;
            foreach (var i in modrinthProjectJArray) {
                modrinthProjectJArrayIndex++;
                int currentIndex = resources.FindIndex(j => j.ModrinthProjectId == i["id"].ToString());
                ModResource minecraftResource = resources[currentIndex];
                if (minecraftResource.DisplayName != i["title"].ToString()) {
                    minecraftResource.DisplayName = i["title"].ToString();
                }
                minecraftResource.slug = i["slug"].ToString();
                minecraftResource.WebsiteUrl = "https://modrinth.com/"+i["project_type"]+"/"+i["slug"];
                minecraftResource.Description = i["description"].ToString().Trim().Replace("\n"," ");
                minecraftResource.Logo = i["icon_url"].ToString();
                minecraftResource.ResourceSource = "Modrinth";
                resources[currentIndex] = minecraftResource;
                progressCount = progressFirstEnd + (int)((double)modrinthProjectJArrayIndex / modrinthProjectJArray.Count * (end - progressFirstEnd));
                progress?.Report(progressCount);
                await Task.Delay(5);
            }
        }
        else {
            progress?.Report(end);
            return resources;
        }
        progress?.Report(end);
        return resources;
    }
    
    private static async Task<List<ModResource>> GetCurseForgeArgs(List<ModResource> resources,IProgress<int> progress,int start,int end,bool JustId = false) {
        //POST https://api.curseforge.com/v1/fingerprints/432
        //header X-API-KEY $2a$10$.kgOA4jo8lw4LTxMMVJ8x.ZPdziizi72Gok2pzA5HYj3qZ6fnONs6[示例，已废弃]
        //body {"fingerprints" :[CurseForgeSha1,...]}
        int progressCount = 0;
        int progressFirstEnd = start + (int)((end - start) /2);
        Dictionary<string,string> header = new () {
            {"X-API-KEY",KeyUtil.CURSEFORGE_API_KEY}
        };
        List<uint> curseForgeSha1s = resources.Select(i => i.CurseForgeSha1).ToList();
        Dictionary<string,Object> curseForgeIdJsonBody = new () {
            {"fingerprints",curseForgeSha1s}
        };
        var curseForgeIdJson = await HttpRequestUtil.Post("https://api.curseforge.com/v1/fingerprints/432",curseForgeIdJsonBody,header);
        Dictionary<uint, int> curseForgeHashAndId = new ();
        if (curseForgeIdJson.IsSuccess) {
            JObject data = JObject.Parse(curseForgeIdJson.Content);
            var matches = data["data"]?["exactMatches"] as JArray;
            if (matches != null) {
                int matchesIndex = 0;
                foreach (var i in matches) {
                    matchesIndex++;
                    uint sha1 = i["file"]["fileFingerprint"].ToObject<uint>();
                    curseForgeHashAndId[sha1] = i["id"].ToObject<int>();
                    resources[resources.FindIndex(j => j.CurseForgeSha1 == sha1)].CurseForgeId =
                        i["id"].ToObject<int>();
                    progressCount = start + (int)((double)matchesIndex / matches.Count * (progressFirstEnd - start));
                    progress?.Report(progressCount);
                    await Task.Delay(2);
                }
            }
        }
        else {
            Console.WriteLine(curseForgeIdJson.ErrorMessage);
            return resources;
        }
        progress?.Report(progressFirstEnd);
        if (JustId) {
            progress?.Report(end);
            return resources;
        }
        //POST https://api.curseforge.com/v1/mods
        //header X-API-KEY $2a$10$.kgOA4jo8lw4LTxMMVJ8x.ZPdziizi72Gok2pzA5HYj3qZ6fnONs6[示例，已废弃]
        //body {"modIds":[上一个请求的id,...]}
        Dictionary<string,Object> curseForgeModJsonBody = new () {
            {"modIds",curseForgeHashAndId.Values}
        };
        var curseForgeModJson = await HttpRequestUtil.Post("https://api.curseforge.com/v1/mods",curseForgeModJsonBody,header);
        if (curseForgeModJson.IsSuccess) {
            JObject data = JObject.Parse(curseForgeModJson.Content);
            var modArray = data["data"] as JArray;
            if (modArray != null) {
                int modArrayIndex = 0;
                foreach (var i in modArray) {
                    modArrayIndex++;
                    int currentIndex = resources.FindIndex(j => j.CurseForgeId == i["id"].ToObject<int>());
                    ModResource resource = resources[currentIndex];
                    resource.DisplayName = i["name"].ToString();
                    resource.Logo = i["logo"]?["url"].ToString();
                    resource.Description = i["summary"].ToString();
                    resource.WebsiteUrl = i["links"]?["websiteUrl"].ToString();
                    resource.Author = i["authors"]?[0]?["name"].ToString();
                    resource.slug = i["slug"].ToString();
                    resource.ResourceSource = "CurseForge";
                    resources[currentIndex] = resource;
                    progressCount = progressFirstEnd + (int)((double)modArrayIndex / modArray.Count * (end - progressFirstEnd));
                    progress?.Report(progressCount);
                    await Task.Delay(5);
                }
            }
        }
        else {
            progress?.Report(end);
            Console.WriteLine(curseForgeModJson.ErrorMessage);
            return resources;
        }
        progress?.Report(end);
        return resources;
    }
}