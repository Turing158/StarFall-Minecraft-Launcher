using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Resources;
using fNbt;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.Services;
using StarFallMC.Services.Minecraft;
using StarFallMC.Services.Resources;
using StarFallMC.Navigation;
using StarFallMC.Util;
using StarFallMC.Entity.Enum;
using StarFallMC.Entity.Loader;
using StarFallMC.Entity.Resource;
using StarFallMC.SettingPages;
using MessageBox = StarFallMC.Component.MessageBox;
using MessageBoxResult = StarFallMC.Entity.Enum.MessageBoxResult;

namespace StarFallMC.Services.Resources;

public sealed class ResourceWorkflowService {

    private readonly LauncherUiCoordinator _uiCoordinator;
    private readonly MinecraftServiceContainer _minecraftServices;
    private readonly ResourceServiceContainer _resourceServices;

    private static readonly string CurseForgeAPI = "https://api.curseforge.com";
    private static readonly string ModrinthAPI = "https://api.modrinth.com";

    public List<MinecraftDownloader> LatestType { get; } = [];
    public List<MinecraftDownloader> ReleaseType { get; } = [];
    public List<MinecraftDownloader> SnapshotType { get; } = [];
    public List<MinecraftDownloader> AprilFoolsType { get; } = [];
    public List<MinecraftDownloader> OldType { get; } = [];

    // This finite catalog is loaded from the packaged ModData resource.
    public List<McModData> McModData { get; } = [];
    
    private static Dictionary<string,string> CurseForgeApiHeader = new () {
        {"X-API-KEY",KeyUtil.CURSEFORGE_API_KEY}
    };
    
    // 用于检查是否初始化
    public ResourceWorkflowService(
        LauncherUiCoordinator? uiCoordinator = null,
        MinecraftServiceContainer? minecraftServices = null,
        ResourceServiceContainer? resourceServices = null)
    {
        _uiCoordinator = uiCoordinator ?? new LauncherUiCoordinator();
        _minecraftServices = minecraftServices ?? MinecraftServices.Current;
        _resourceServices = resourceServices ?? ResourceServices.Current;
    }

    public bool IsNeedInitDownloader() {
        return LatestType == null || ReleaseType == null || SnapshotType == null || AprilFoolsType == null || OldType == null ||
               LatestType.Count == 0 || ReleaseType.Count == 0 || SnapshotType.Count == 0 || AprilFoolsType.Count == 0 || OldType.Count == 0;
    }
    
    // 清除数据
    public void ClearDownloader() {
        LatestType?.Clear();
        ReleaseType?.Clear();
        SnapshotType?.Clear();
        AprilFoolsType?.Clear();
        OldType?.Clear();
    }
    
    // 获取当前游戏
    public MinecraftItem GetMinecraftItem() {
        var currentGame = ApplicationState.GameSelection.CurrentGame;
        return string.IsNullOrEmpty(currentGame.Path) ? null : currentGame;
    }

    public string GetCurrentDir() {
        return ApplicationState.GameSelection.CurrentDir?.Path;
    }
    
    // 获取本地汉化模组数据
    public void GetMcModDataInit() {
        McModData?.Clear();
        Uri uri = new Uri("pack://application:,,,/StarFallMC;component/assets/ModData");
        StreamResourceInfo info = Application.GetResourceStream(uri);
        string data = "";
        if(info != null){
            using (StreamReader reader = new StreamReader(info.Stream)){
                data = reader.ReadToEnd();
            }
        }
        if (string.IsNullOrEmpty(data)) {
            return;
        }
        string[] lines = data.Replace("\r\n", "\n").Replace("\r", "").Split('\n');
        int index = 0;
        foreach (var i in lines) {
            index++;
            if (i == "") {
                continue;
            }
            foreach (var j in i.Split("\u00a8")) {
                var modData = new McModData() {
                    WikiId = index,
                };
                var splitLine = j.Split("|");
                if (splitLine[0].StartsWith("@")) {
                    modData.CurseForgeSlug = string.Empty;
                    modData.ModrinthSlug = splitLine[0].Replace("@", "");
                }
                else if (splitLine[0].EndsWith("@")) {
                    modData.CurseForgeSlug = splitLine[0].TrimEnd('@');
                    modData.ModrinthSlug = modData.CurseForgeSlug;
                }
                else if (splitLine[0].Contains("@")) {
                    modData.CurseForgeSlug = splitLine[0].Split("@")[0];
                    modData.ModrinthSlug = splitLine[0].Split("@")[1];
                }
                else {
                    modData.CurseForgeSlug = splitLine[0];
                    modData.ModrinthSlug = string.Empty;
                }
                if (splitLine.Length >= 2) {
                    modData.AllName = splitLine[1];
                    if (modData.AllName.Contains("*")) {
                        var slug = modData.CurseForgeSlug ?? modData.ModrinthSlug ?? "";
                        var slugToName = slug.Replace("-", " ");
                        modData.AllName = modData.AllName.Replace("*", 
                            $" ({char.ToUpper(slugToName[0]) + slugToName.Substring(1).ToLower()})");
                    }
                }
                McModData.Add(modData);
            }
        }
        _resourceServices.SetLocalizations(McModData);
    }

    public async Task<ModPackInstallResult> InstallModPack(string modPackPath,CancellationToken ct) {
        string json = string.Empty;
        bool isCurseForge = false;

        try {
            using ZipArchive archive = ZipFile.OpenRead(modPackPath);
            var modrinthIndexJsonEntry = archive.GetEntry("modrinth.index.json");
            if (modrinthIndexJsonEntry != null) {
                json = ReadJsonEntry(modrinthIndexJsonEntry);
                isCurseForge = false;
            }

            var curseForgeManifestEntry = archive.GetEntry("manifest.json");
            if (curseForgeManifestEntry != null) {
                json = ReadJsonEntry(curseForgeManifestEntry);
                isCurseForge = true;
            }

            if (string.IsNullOrEmpty(json)) {
                return ModPackInstallResult.IsNotModPack;
            }
            ModPack modPack;
            if (isCurseForge) {
                modPack = await CurseForgeModPackInstallParse(json);
            }
            else {
                modPack = ModrinthModPackInstallParse(json);
            }
            if (modPack == null) {
                return ModPackInstallResult.Failed;
            }
            
            bool isCancel = false; 
            string versionPath = Path.Combine(GetCurrentDir(), "versions", modPack.VersionName);
            string versionJsonPath = Path.Combine(versionPath, $"{modPack.VersionName}.json");
            string versionJarPath = Path.Combine(versionPath, $"{modPack.VersionName}.jar");
            bool isVersionExists = File.Exists(versionJsonPath) || File.Exists(versionJarPath);
            string newName = string.Empty;
            int renameCount = 0;
            while (isVersionExists) {
                renameCount++;
                newName = $"{modPack.VersionName}_P{renameCount}";
                versionPath = Path.Combine(GetCurrentDir(), "versions", newName);
                versionJsonPath = Path.Combine(versionPath, $"{newName}.json");
                versionJarPath = Path.Combine(versionPath, $"{newName}.jar");
                isVersionExists = File.Exists(versionJsonPath) || File.Exists(versionJarPath);
            }
            string msgContent = renameCount != 0 
                ? $"该版本 {modPack.VersionName} 已存在，是否将游戏名称更改为\n=> {newName} \n并安装该版本" 
                : $"确认安装 {modPack.VersionName} 整合包吗?";
            await MessageBox.ShowAsync(msgContent,"安装整合包", 
                callback: r => {
                    if (r == MessageBoxResult.Cancel) {
                        isCancel = true;
                    }
                }, 
                btnType: MessageBoxBtnType.ConfirmAndCancel,
                confirmBtnText: isVersionExists ? "改名安装" : "安装");
            if (renameCount >= 1) {
                modPack.Version += $"_P{renameCount}";
            }
            if (!Directory.Exists(versionPath)) {
                Directory.CreateDirectory(versionPath);
            }
            if (isCancel) {
                return ModPackInstallResult.Cancel;
            }
            Console.WriteLine("开始安装整合包");
            if (!Directory.Exists(versionPath)) {
                Directory.CreateDirectory(versionPath);
            }
            
            string processKey = string.Empty;
            
            await _uiCoordinator.GoBackAsync();
            _uiCoordinator.ShowDownloadPage();
            //判断模组加载器后进行安装
            if (modPack.Loader == MinecraftLoader.Fabric) {
                FabricLoader loader = new FabricLoader() {
                    Version = modPack.LoaderVersion,
                    Mcversion = modPack.MinecraftVersion
                };
                processKey = await _minecraftServices.Loader.InstallFabricAsync(
                    modPack.MinecraftVersion,
                    modPack.VersionName,
                    GetCurrentDir(),
                    loader,
                    cancellationToken: ct,
                    extraFiles: modPack.Files,
                    isNeedAutoFinish: false) ?? string.Empty;
            }
            else if (modPack.Loader == MinecraftLoader.NeoForge) {
                NeoForgeLoader loader = new NeoForgeLoader() { 
                    Version = modPack.LoaderVersion,
                    Mcversion = modPack.MinecraftVersion
                };
                processKey = await _minecraftServices.Loader.InstallNeoForgeAsync(
                    modPack.MinecraftVersion,
                    modPack.VersionName,
                    GetCurrentDir(),
                    loader,
                    cancellationToken: ct,
                    extraFiles: modPack.Files,
                    isNeedAutoFinish: false) ?? string.Empty;
            }
            else if (modPack.Loader == MinecraftLoader.Forge) {
                ForgeLoader loader = new ForgeLoader() { 
                    Version = modPack.LoaderVersion,
                    Mcversion = modPack.MinecraftVersion
                };
                processKey = await _minecraftServices.Loader.InstallForgeAsync(
                    modPack.MinecraftVersion,
                    modPack.VersionName,
                    GetCurrentDir(),
                    loader,
                    cancellationToken: ct,
                    extraFiles: modPack.Files,
                    isNeedAutoFinish: false) ?? string.Empty;
            }

            if (string.IsNullOrEmpty(processKey)) {
                return ModPackInstallResult.Failed;
            }
            //安装完后，将整合包的overrides文件内的所有文件覆盖到游戏目录下
            foreach (var entry in archive.Entries) {
                string entryFullName = entry.FullName;
                if (entryFullName.StartsWith("overrides/", StringComparison.OrdinalIgnoreCase) 
                    || entryFullName.StartsWith("overrides\\", StringComparison.OrdinalIgnoreCase)) {
                    if (entryFullName.StartsWith("overrides/", StringComparison.OrdinalIgnoreCase)) {
                        entryFullName = entryFullName.Substring("overrides/".Length);
                    }
                    if (entryFullName.StartsWith("overrides\\", StringComparison.OrdinalIgnoreCase)) {
                        entryFullName = entryFullName.Substring("overrides\\".Length);
                    }
                    if (string.IsNullOrEmpty(entryFullName) || entryFullName.EndsWith("/") || entryFullName.EndsWith("\\")) {
                        continue;
                    }
                    string filePath = Path.Combine(versionPath, entryFullName);
                    string directoryPath = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(directoryPath)) {
                        Directory.CreateDirectory(directoryPath);
                    }
                    entry.ExtractToFile(filePath, true);
                }
            }
            
            _uiCoordinator.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
        }
        catch (NotSupportedException notSupportedException) {
            
            return ModPackInstallResult.IsNotModPack;
        }
        catch (InvalidDataException invalidDataException) {
            return ModPackInstallResult.IsNotModPack;
        }
        catch (Exception e) {
            Console.WriteLine(e);
        }
        return ModPackInstallResult.Success;
    }
    
    private static string ReadJsonEntry(ZipArchiveEntry jsonEntry) {
        using Stream stream = jsonEntry.Open();
        using StreamReader reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    
    private ModPack ModrinthModPackInstallParse(string json) {
        ModPack modPack;
        try {
            JObject modrinthIndex = JObject.Parse(json);
            string name = modrinthIndex["name"].ToString();
            string version = modrinthIndex["versionId"].ToString();
            List<DownloadFile> files = new List<DownloadFile>();
            string currentDir = GetCurrentDir();
            foreach (var fileJson in modrinthIndex["files"] as JArray ?? new JArray()) {
                string path = Path.Combine(currentDir, "versions", $"{name} {version}", fileJson["path"].ToString());
                var file = new DownloadFile() {
                    Name = Path.GetFileNameWithoutExtension(path),
                    FilePath = path,
                    UrlPaths = fileJson["downloads"]?.ToObject<List<string>>(),
                    Size = fileJson["fileSize"]?.ToObject<long>() ?? 0,
                    Sha1 = fileJson["hashes"]?["sha1"]?.ToString(),
                };
                file.UrlPath = file.UrlPaths != null && file.UrlPaths.Count > 0 ? file.UrlPaths[0] : "";
                files.Add(file);
            }

            var dependencies = modrinthIndex["dependencies"] as JObject;
            
            var minecraftVersion = dependencies["minecraft"]?.ToString();
            MinecraftLoader loader = MinecraftLoader.Unknown;
            string loaderVersion = string.Empty;
            if (dependencies.ContainsKey("fabric-loader")) {
                loader = MinecraftLoader.Fabric;
                loaderVersion = dependencies["fabric-loader"]?.ToString();
            }
            else if (dependencies.ContainsKey("quilt-loader")) {
                loader = MinecraftLoader.Quilt;
                loaderVersion = minecraftVersion;
            }
            else if (dependencies.ContainsKey("neoforge")) {
                loader = MinecraftLoader.NeoForge;
                loaderVersion = dependencies["neoforge"]?.ToString();
            }
            else if (dependencies.ContainsKey("forge")) {
                loader = MinecraftLoader.Forge;
                loaderVersion = dependencies["forge"]?.ToString();
            }
            
            modPack = new ModPack() {
                Name = name,
                Version = version,
                Files = files,
                MinecraftVersion = minecraftVersion,
                Loader = loader,
                LoaderVersion = loaderVersion,
            };
        }
        catch (Exception e){
            return null;
        }
        return modPack;
    }

    private async Task<ModPack> CurseForgeModPackInstallParse(string json) {
        ModPack modPack;
        try {
            JObject manifest = JObject.Parse(json);
            string name = manifest["name"].ToString();
            string version = manifest["version"].ToString();
            List<long> fileIds = new List<long>();
            foreach (var file in manifest["files"] as JArray) {
                long id = file["fileID"]?.ToObject<long>() ?? 0;
                if (id != 0) {
                    fileIds.Add(id);
                }
            }
            
            Dictionary<string, Object> fileRequestBody = new();
            fileRequestBody["fileIds"] = fileIds;
            string modsPath = Path.Combine(GetCurrentDir(), "versions", $"{name} {version}", "mods");
            // 获取所有Mod文件Id的下载地址，并且拼接正常的游戏路径
            // POST https://api.curseforge.com/v1/mods/files
            // 需要传入一个fileIds的数组
            var filesResult = await HttpRequestUtil.Post($"{CurseForgeAPI}/v1/mods/files",headers:CurseForgeApiHeader,args:fileRequestBody);
            if (!filesResult.IsSuccess) {
                Console.WriteLine("获取CurseForge整合包模组文件失败，请重试！");
                return null;
            }
            var filesResultJson = JObject.Parse(filesResult.Content);
            List<DownloadFile> files = new List<DownloadFile>();
            foreach (var fileObj in filesResultJson["data"] as JArray) {
                string fileName = fileObj["fileName"]?.ToString();
                var file = new DownloadFile {
                    Name = fileName,
                    FilePath = Path.Combine(modsPath, fileName),
                    UrlPath = fileObj["downloadUrl"]?.ToString(),
                    Size = fileObj["fileLength"]?.ToObject<long>() ?? 0,
                    Sha1 = fileObj["hashes"]?[1]?["value"]?.ToString(),
                };
                if (string.IsNullOrEmpty(file.UrlPath)) {
                    file.UrlPath = await NetworkUtil.GetNeedJavaScriptRedirectUrl(
                        $"https://www.curseforge.com/api/v1/mods/{fileObj["modId"]}/files/{fileObj["id"]}/download"
                        , "mediafilez.");
                    Console.WriteLine($"https://www.curseforge.com/api/v1/mods/{fileObj["modId"]}/files/{fileObj["id"]}/download");
                    Console.WriteLine(file.UrlPath);
                }
                files.Add(file);
            }
            var minercraftVersion = manifest["minecraft"]?["version"]?.ToString();
            MinecraftLoader loader = MinecraftLoader.Unknown;
            string loaderVersion = string.Empty;
            foreach (var modLoader in manifest["minecraft"]?["modLoaders"] as JArray) {
                if (modLoader["primary"] == null || modLoader["primary"].ToObject<bool>().Equals(false)) {
                    continue;
                }
                var loaderStr = modLoader["id"]?.ToString();
                var loaderParts = loaderStr.Split("-");
            
                if (loaderParts[0].Contains("fabric")) {
                    loader = MinecraftLoader.Fabric;
                    loaderVersion = string.Join("-", loaderParts.Skip(1));
                }
                else if (loaderParts[0].Contains("neoforge")) {
                    loader = MinecraftLoader.NeoForge;
                    loaderVersion = string.Join("-", loaderParts.Skip(1));
                }
                else if (loaderParts[0].Contains("forge")) {
                    loader = MinecraftLoader.Forge;
                    loaderVersion = string.Join("-", loaderParts.Skip(1));
                }
                else if (loaderParts[0].Contains("quilt")) {
                    loader = MinecraftLoader.Quilt;
                    loaderVersion = string.Join("-", loaderParts.Skip(1));;
                }
            }
            modPack = new ModPack() {
                Name = name,
                Version = version,
                Files = files,
                MinecraftVersion = minercraftVersion,
                Loader = loader,
                LoaderVersion = loaderVersion,
            };
        }
        catch (Exception e) {
            Console.WriteLine(e);
            return null;
        }
        return modPack;
    }
    
    public class CurseForgeModIdAndFile {
        public long ModId { get; set; }
        public long FileId { get; set; }
        public DownloadFile file;
    }
    
    // 获取Minecraft所支持的所有模组加载器（之后打算优化分开获取）
    public async Task<(
        List<ForgeLoader>, 
        List<LiteLoader>, 
        List<NeoForgeLoader>, 
        List<OptifineLoader>, 
        List<FabricLoader>, 
        List<MinecraftResource>,
        List<QuiltLoader>)> GetAllLoaderByMinecraftDownloader(
            string version,
            CancellationToken ct,
            IProgress<int>? progress = null) {
        progress ??= new Progress<int>(_ => { });
        var forgeLoaders = new List<ForgeLoader>();
        var liteLoaders = new List<LiteLoader>();
        var neoForgeLoaders = new List<NeoForgeLoader>();
        var optifineLoaders = new List<OptifineLoader>();
        var fabricLoaders = new List<FabricLoader>();
        var fabricApiVersions = new List<MinecraftResource>();
        var quiltLoaders = new List<QuiltLoader>();
        
        try {
            progress.Report(1);
            ct.ThrowIfCancellationRequested();
            //获取Forge列表，返回Json的数组，数组为空则为Forge不支持该版本
            //需要单独Object的build[下载forge标识之一]，version[forge版本]，mcversion[游戏版本]和modified[时间字段]
            //GET https://bmclapi2.bangbang93.com/forge/minecraft/:id
            var forgeResult = await HttpRequestUtil.Get($"https://bmclapi2.bangbang93.com/forge/minecraft/{version}",cancellationToken:ct);
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
                await HttpRequestUtil.Get($"https://bmclapi2.bangbang93.com/liteloader/list?mcversion={version}",cancellationToken:ct);
            if (liteloaderResult.IsSuccess) {
                if (!string.IsNullOrEmpty(liteloaderResult.Content)) {
                    try {
                        var liteJson = JObject.Parse(liteloaderResult.Content);
                        liteLoaders.Add(new LiteLoader {
                            Version = liteJson["version"]?.ToString(),
                            Mcversion = liteJson["mcversion"]?.ToString(),
                            Timestamp = liteJson["build"]?["timestamp"]?.ToObject<long>() ?? 1,
                            IsStable = liteJson["type"]?.ToString() == "RELEASE"
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
            var neoforgeResult = await HttpRequestUtil.Get($"https://bmclapi2.bangbang93.com/neoforge/list/{version}",cancellationToken:ct);
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
            var optifineResult = await HttpRequestUtil.Get($"https://bmclapi2.bangbang93.com/optifine/{version}",cancellationToken:ct);
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
                await HttpRequestUtil.Get($"https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{version}",cancellationToken:ct);
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
                    await HttpRequestUtil.Get($"{ModrinthAPI}/v2/project/P7dR8mSH/version",cancellationToken:ct);
                if (fabricApiModrinthResult.IsSuccess) {
                    try {
                        var fabricApiJson = JArray.Parse(fabricApiModrinthResult.Content);
                        foreach (var i in fabricApiJson) {
                            ct.ThrowIfCancellationRequested();
                            var gameVersions = i["game_versions"].ToObject<List<string>>();

                            if (gameVersions == null || gameVersions.Contains(version)) {
                                var modResource = new MinecraftResource() {
                                    OriginalName = i["version_number"]?.ToString().Split("+")[0],
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
                        $"{CurseForgeAPI}/v1/mods/306612/files",
                        headers: new Dictionary<string, string>() {
                            { "X-API-KEY", KeyUtil.CURSEFORGE_API_KEY }
                        },cancellationToken:ct);
                    if (fabricApiCurseForgeResult.IsSuccess) {
                        try {
                            var fabricApiCurseForgeJson = JObject.Parse(fabricApiCurseForgeResult.Content);
                            foreach (var i in fabricApiCurseForgeJson["data"] as JArray) {
                                ct.ThrowIfCancellationRequested();
                                var gameVersion = i["gameVersions"]?.ToObject<List<string>>();

                                if (gameVersion == null || gameVersion.Contains(version)) {
                                    var modResource = new MinecraftResource() {
                                        OriginalName = i["displayName"]?.ToString().Split(" ")[^1].Split("+")[0],
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
            var quiltLoaderResult = await HttpRequestUtil.Get($"https://meta.quiltmc.org/v3/versions/loader/{version}",cancellationToken:ct);
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
    
    // 获取Minecraft下载列表
    public async Task GetMinecraftDownloader(CancellationToken ct, IProgress<int>? progress = null) {
        progress ??= new Progress<int>(_ => { });
        progress.Report(5);
        try {
            ct.ThrowIfCancellationRequested();
            var result = await HttpRequestUtil.Get("https://bmclapi2.bangbang93.com/mc/game/version_manifest.json",cancellationToken:ct);
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
                    else if (name != null && AprilFoolsVersionName.Contains(name)) {
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
    
}
