using System.IO;
using System.IO.Compression;
using Newtonsoft.Json.Linq;
using StarFallMC.Entity;
using StarFallMC.Entity.Enum;
using StarFallMC.Entity.Loader;
using StarFallMC.Entity.Resource;
using StarFallMC.Services.Download;
using StarFallMC.Services.Minecraft.Models;
using StarFallMC.Util;

namespace StarFallMC.Services.Minecraft;

public interface IMinecraftDownloadClient
{
    Task<bool> DownloadSingleAsync(DownloadFile file, CancellationToken cancellationToken);
    Task<DownloadBatchHandle?> StartBatchAsync(IReadOnlyCollection<DownloadFile> files, CancellationToken cancellationToken);
}

public sealed class ApplicationMinecraftDownloadClient : IMinecraftDownloadClient
{
    private readonly DownloadCoordinator? _downloads;

    public ApplicationMinecraftDownloadClient()
    {
    }

    internal ApplicationMinecraftDownloadClient(DownloadCoordinator downloads)
    {
        _downloads = downloads ?? throw new ArgumentNullException(nameof(downloads));
    }

    public Task<bool> DownloadSingleAsync(DownloadFile file, CancellationToken cancellationToken) =>
        (_downloads ?? throw new InvalidOperationException("The application download coordinator is not configured."))
        .DownloadSingle(file, cancellationToken);

    public Task<DownloadBatchHandle?> StartBatchAsync(IReadOnlyCollection<DownloadFile> files, CancellationToken cancellationToken) =>
        (_downloads ?? throw new InvalidOperationException("The application download coordinator is not configured."))
        .StartDownloadBatch(files, cancellationToken);
}

public interface ILoaderInstallInteraction
{
    string Begin(string versionName, IReadOnlyList<string> stepNames);
    void StepCompleted(string processKey, bool advance = true);
    void StepFailed(string processKey, bool advance = false);
    void Notify(string message);
    Task<InstallRetryDecision> ChooseRetryAsync(IReadOnlyList<DownloadFile> failedFiles, CancellationToken cancellationToken);
}

public sealed class NullLoaderInstallInteraction : ILoaderInstallInteraction
{
    public string Begin(string versionName, IReadOnlyList<string> stepNames) => string.Empty;
    public void StepCompleted(string processKey, bool advance = true) { }
    public void StepFailed(string processKey, bool advance = false) { }
    public void Notify(string message) { }
    public Task<InstallRetryDecision> ChooseRetryAsync(IReadOnlyList<DownloadFile> failedFiles, CancellationToken cancellationToken) =>
        Task.FromResult(InstallRetryDecision.Skip);
}

/// <summary>
/// Builds immutable plans and owns each loader installation operation. UI and
/// download implementation details are provided through injected interfaces.
/// </summary>
public sealed partial class LoaderInstallService
{
    private readonly MinecraftPathService _paths;
    private readonly MinecraftVersionResolver _resolver;
    private readonly MinecraftManifestService _manifest;
    private readonly IMinecraftDownloadClient _downloads;
    private readonly IJavaProcessRunner _processRunner;
    private readonly ILoaderInstallInteraction _interaction;
    private readonly string _launcherName;

    public LoaderInstallService(
        MinecraftPathService? paths = null,
        MinecraftVersionResolver? resolver = null,
        MinecraftManifestService? manifest = null,
        IMinecraftDownloadClient? downloads = null,
        IJavaProcessRunner? processRunner = null,
        ILoaderInstallInteraction? interaction = null,
        string launcherName = "StarFallMC")
    {
        _paths = paths ?? new MinecraftPathService();
        _resolver = resolver ?? new MinecraftVersionResolver();
        _manifest = manifest ?? new MinecraftManifestService(paths: _paths);
        _downloads = downloads ?? new ApplicationMinecraftDownloadClient();
        _processRunner = processRunner ?? new ProcessRunnerJavaProcessRunner();
        _interaction = interaction ?? new NullLoaderInstallInteraction();
        _launcherName = string.IsNullOrWhiteSpace(launcherName) ? "StarFallMC" : launcherName;
    }

    public LoaderInstallPlan CreateMinecraftPlan(string minecraftVersion, string versionName, string currentDir)
    {
        string root = _paths.NormalizeRoot(currentDir);
        string versionPath = _paths.GetVersionDirectory(root, versionName);
        return CreatePlan(MinecraftLoader.Minecraft, minecraftVersion, versionName, root,
            ("下载并生成json文件", LoaderInstallStepKind.WriteVersionJson, new DownloadFile($"{versionName}.json", Path.Combine(versionPath, $"{versionName}.json"), VersionJsonUrl(minecraftVersion))),
            ("获取需要下载的文件", LoaderInstallStepKind.ResolveFiles, null),
            ("下载文件", LoaderInstallStepKind.Download, CreateMinecraftJar(minecraftVersion, versionName, versionPath)),
            ("完成安装Minecraft", LoaderInstallStepKind.Complete, null));
    }

    public LoaderInstallPlan CreateOptifinePlan(string minecraftVersion, string versionName, string currentDir, OptifineLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        string root = _paths.NormalizeRoot(currentDir);
        string versionPath = _paths.GetVersionDirectory(root, versionName);
        var installer = new DownloadFile(loader.DisplayName, Path.Combine(versionPath, $"{loader.DisplayName}-installer.jar"),
            $"{MinecraftManifestService.DefaultOptifineApi}/{loader.Mcversion}/{loader.Type}/{loader.Patch}");
        return CreatePlan(MinecraftLoader.Optifine, minecraftVersion, versionName, root,
            ("下载OptiFine Installer", LoaderInstallStepKind.Download, installer),
            ("生成Json文件", LoaderInstallStepKind.WriteVersionJson, null),
            ("获取所需下载文件", LoaderInstallStepKind.ResolveFiles, null),
            ("下载所需文件", LoaderInstallStepKind.Download, CreateMinecraftJar(minecraftVersion, versionName, versionPath)),
            ("安装OptiFine Installer", LoaderInstallStepKind.RunJava, null),
            ("完成OptiFine安装", LoaderInstallStepKind.Complete, null));
    }

    public LoaderInstallPlan CreateLiteLoaderPlan(string minecraftVersion, string versionName, string currentDir, LiteLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        string root = _paths.NormalizeRoot(currentDir);
        string versionPath = _paths.GetVersionDirectory(root, versionName);
        var installer = new DownloadFile(loader.DisplayName, Path.Combine(versionPath, $"{loader.DisplayName}-installer.jar"), TransformLiteLoaderInstallerDownloadApi(loader));
        return CreatePlan(MinecraftLoader.LiteLoader, minecraftVersion, versionName, root,
            ("下载LiteLoader Installer", LoaderInstallStepKind.Download, installer),
            ("生成Json文件", LoaderInstallStepKind.WriteVersionJson, null),
            ("安装LiteLoader Installer", LoaderInstallStepKind.CopyFiles, null),
            ("获取所需下载文件", LoaderInstallStepKind.ResolveFiles, null),
            ("下载所需文件", LoaderInstallStepKind.Download, CreateMinecraftJar(minecraftVersion, versionName, versionPath)),
            ("完成LiteLoader安装", LoaderInstallStepKind.Complete, null));
    }

    public LoaderInstallPlan CreateForgePlan(string minecraftVersion, string versionName, string currentDir, ForgeLoader loader, OptifineLoader? optifine = null)
    {
        ArgumentNullException.ThrowIfNull(loader);
        string root = _paths.NormalizeRoot(currentDir);
        string versionPath = _paths.GetVersionDirectory(root, versionName);
        string forgeUrl = !string.IsNullOrEmpty(loader.Build)
            ? $"{MinecraftManifestService.DefaultApi}/forge/download/{loader.Build}"
            : $"{MinecraftManifestService.DefaultApi}/forge/download?mcversion={loader.Mcversion}&version={loader.Version}&category=installer&format=jar";
        var installer = new DownloadFile($"Forge Installer-{versionName}", Path.Combine(versionPath, $"forge-{minecraftVersion}-installer.jar"), forgeUrl);
        var steps = new List<(string, LoaderInstallStepKind, DownloadFile?)> {
            ("下载Forge安装器", LoaderInstallStepKind.Download, installer),
            ("生成json文件", LoaderInstallStepKind.WriteVersionJson, null),
            ("获取需要下载的文件", LoaderInstallStepKind.ResolveFiles, null),
            ("下载文件", LoaderInstallStepKind.Download, CreateMinecraftJar(minecraftVersion, versionName, versionPath)),
            ("安装Forge", LoaderInstallStepKind.RunJava, null)
        };
        if (optifine != null) steps.Add(($"安装模组{optifine.DisplayName}", LoaderInstallStepKind.RunJava, null));
        steps.Add(("完成安装", LoaderInstallStepKind.Complete, null));
        return CreatePlan(MinecraftLoader.Forge, minecraftVersion, versionName, root, steps.ToArray());
    }

    public LoaderInstallPlan CreateFabricPlan(string minecraftVersion, string versionName, string currentDir, FabricLoader loader, MinecraftResource? fabricApi = null)
    {
        ArgumentNullException.ThrowIfNull(loader);
        string root = _paths.NormalizeRoot(currentDir);
        string versionPath = _paths.GetVersionDirectory(root, versionName);
        return CreatePlan(MinecraftLoader.Fabric, minecraftVersion, versionName, root,
            ("生成Fabric Json文件", LoaderInstallStepKind.WriteVersionJson, new DownloadFile(loader.DisplayName, Path.Combine(versionPath, $"{versionName}.json"), $"{MinecraftManifestService.DefaultApi}/fabric-meta/v2/versions/loader/{loader.Mcversion}/{loader.Version}/profile/json")),
            ("获取所需下载文件", LoaderInstallStepKind.ResolveFiles, null),
            ("下载所需文件", LoaderInstallStepKind.Download, CreateMinecraftJar(minecraftVersion, versionName, versionPath)),
            ("完成Fabric安装", LoaderInstallStepKind.Complete, null));
    }

    public LoaderInstallPlan CreateNeoForgePlan(string minecraftVersion, string versionName, string currentDir, NeoForgeLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        string root = _paths.NormalizeRoot(currentDir);
        string versionPath = _paths.GetVersionDirectory(root, versionName);
        var installer = new DownloadFile($"NeoForge Installer-{versionName}", Path.Combine(versionPath, $"neoforge-{loader.DisplayName}-installer.jar"),
            $"{MinecraftManifestService.DefaultApi}/neoforge/version/{loader.Version}/download/installer.jar");
        return CreatePlan(MinecraftLoader.NeoForge, minecraftVersion, versionName, root,
            ("下载NeoForge安装器", LoaderInstallStepKind.Download, installer),
            ("生成json文件", LoaderInstallStepKind.WriteVersionJson, null),
            ("获取需要下载的文件", LoaderInstallStepKind.ResolveFiles, null),
            ("下载文件", LoaderInstallStepKind.Download, CreateMinecraftJar(minecraftVersion, versionName, versionPath)),
            ("安装NeoForge", LoaderInstallStepKind.RunJava, null),
            ("完成安装", LoaderInstallStepKind.Complete, null));
    }

    public LoaderInstallPlan CreateQuiltPlan(string minecraftVersion, string versionName, string currentDir, QuiltLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        return new LoaderInstallPlan(
            MinecraftLoader.Quilt,
            minecraftVersion,
            versionName,
            _paths.NormalizeRoot(currentDir),
            false,
            Array.Empty<LoaderInstallStep>(),
            "暂不支持安装QuiltLoader");
    }

    public List<Lib> GetLibs(string json) => _resolver.GetLibs(json);

    public List<DownloadFile> GetNeedLibrariesFile(IReadOnlyList<Lib> libs, string currentDir)
    {
        string root = _paths.NormalizeRoot(currentDir);
        var result = new List<DownloadFile>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Lib lib in libs)
        {
            if (!names.Add(lib.name) || (lib.name.Contains("forge", StringComparison.OrdinalIgnoreCase) && lib.name.EndsWith("client", StringComparison.Ordinal))) continue;
            if (lib.rules.Count > 0 && lib.rules.Any(rule => rule.Os == DeviceOs.Windows && rule.IsAllow))
            {
                foreach (var classifier in lib.classifiers.Where(pair => pair.Key.Contains("windows", StringComparison.OrdinalIgnoreCase)))
                {
                    AddLibraryDownload(result, paths, Path.GetFileName(classifier.Value.path), _paths.GetLibraryPath(root, classifier.Value.path),
                        MinecraftManifestService.DefaultMavenApi + classifier.Value.path, classifier.Value.url, classifier.Value.size);
                    break;
                }
                if (!string.IsNullOrEmpty(lib.path))
                {
                    string relativePath = !string.IsNullOrEmpty(lib.artifact?.path) ? lib.artifact.path : lib.path;
                    AddLibraryDownload(result, paths, lib.name, _paths.GetLibraryPath(root, relativePath),
                        MinecraftManifestService.DefaultMavenApi + relativePath, lib.artifact?.url, lib.artifact?.size ?? -1);
                }
            }
            if (lib.rules.Count == 0)
            {
                string relativePath = lib.path;
                string url = MinecraftManifestService.DefaultMavenApi + relativePath;
                string filePath;
                if (lib.name.Contains("optifine", StringComparison.OrdinalIgnoreCase) && !lib.name.Contains("launchwrapper-of", StringComparison.OrdinalIgnoreCase))
                {
                    (string mcVersion, string type, string patch) = FormatOptifineName(lib.name);
                    url = $"{MinecraftManifestService.DefaultOptifineApi}/{mcVersion}/{type}/{patch}";
                    string parent = Path.GetDirectoryName(_paths.GetLibraryPath(root, relativePath)) ?? root;
                    filePath = Path.Combine(parent, $"{Path.GetFileNameWithoutExtension(relativePath)}-installer.jar");
                }
                else if (lib.name.Contains("launchwrapper-of", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                else
                {
                    filePath = _paths.GetLibraryPath(root, relativePath);
                }
                if (!string.IsNullOrEmpty(lib.artifact?.path))
                {
                    relativePath = lib.artifact.path;
                    filePath = _paths.GetLibraryPath(root, relativePath);
                    url = MinecraftManifestService.DefaultMavenApi + relativePath;
                }
                AddLibraryDownload(result, paths, lib.name, filePath, url, lib.artifact?.url, lib.artifact?.size ?? -1);
            }
        }
        return result.Where(file => !string.Equals(file.UrlPath, MinecraftManifestService.DefaultMavenApi, StringComparison.Ordinal)).ToList();
    }

    public static List<DownloadFile> GetNeedDownloadFile(IEnumerable<DownloadFile> files) =>
        files.Where(file => !File.Exists(file.FilePath)).ToList();

    public DownloadFile? GetForgeFmlDownloadFile(string json, string currentDir)
    {
        (string forgeVersion, string mcVersion, string mcpVersion) = GetForgeFmlArgs(json);
        if (string.IsNullOrEmpty(forgeVersion) || string.IsNullOrEmpty(mcVersion) || string.IsNullOrEmpty(mcpVersion)) return null;
        string root = _paths.NormalizeRoot(currentDir);
        var paths = new List<string> {
            $"net/minecraftforge/forge/{mcVersion}-{forgeVersion}/forge-{mcVersion}-{forgeVersion}-client.jar",
            $"net/minecraftforge/forge/{mcVersion}-{forgeVersion}/forge-{mcVersion}-{forgeVersion}-universal.jar",
            $"net/minecraft/client/{mcVersion}-{mcpVersion}/client-{mcVersion}-{mcpVersion}-srg.jar",
            $"net/minecraft/client/{mcVersion}-{mcpVersion}/client-{mcVersion}-{mcpVersion}-extra.jar"
        };
        paths.AddRange(new[] { "fmlcore", "javafmllanguage", "mclanguage" }.Select(name =>
            $"net/minecraftforge/{name}/{mcVersion}-{forgeVersion}/{name}-{mcVersion}-{forgeVersion}.jar"));
        if (paths.All(path => File.Exists(_paths.GetLibraryPath(root, path)))) return null;
        string relative = $"net/minecraftforge/forge/{mcVersion}-{forgeVersion}/forge-{mcVersion}-{forgeVersion}-installer.jar";
        return new DownloadFile(Path.GetFileName(relative), _paths.GetLibraryPath(root, relative), MinecraftManifestService.DefaultMavenApi + relative);
    }

    public (JToken? Mapping, string? Path, DownloadFile? Download) GetMappingsDownloadFile(string json, string currentDir, string? minecraftVersion = null, bool isNeoForge = false)
    {
        JObject root = JObject.Parse(json);
        minecraftVersion ??= root["inheritsFrom"]?.ToString();
        if (string.IsNullOrEmpty(minecraftVersion) && root["patches"] is JArray patches && patches.Count > 1)
        {
            minecraftVersion = patches.LastOrDefault()?["inheritsFrom"]?.ToString();
        }
        minecraftVersion ??= GetForgeFmlArgs(json).MinecraftVersion;
        JToken? mapping = root["downloads"]?["client_mappings"];
        if (mapping == null || string.IsNullOrEmpty(minecraftVersion)) return (null, null, null);
        string rootDir = _paths.NormalizeRoot(currentDir);
        string path = Path.Combine(_paths.GetLibrariesDirectory(rootDir), "net", "minecraft", "client", minecraftVersion, $"client-{minecraftVersion}-mappings.tsrg");
        var file = new DownloadFile($"{minecraftVersion}-mappings", path, mapping["url"]?.ToString() ?? string.Empty);
        return (mapping, path, file);
    }

    public async Task<IReadOnlyList<DownloadFile>> GetAssetsFileAsync(string json, string currentDir, bool forceDownload = false, CancellationToken cancellationToken = default)
    {
        DownloadFile index = _manifest.CreateAssetIndexDownloadFile(json, currentDir);
        if (forceDownload || !File.Exists(index.FilePath))
        {
            DownloadBatchHandle? handle = await _downloads.StartBatchAsync([index], cancellationToken).ConfigureAwait(false);
            bool success = handle != null && (await handle.Completion.WaitAsync(cancellationToken).ConfigureAwait(false)).Success;
            if (!success) return Array.Empty<DownloadFile>();
        }
        ManifestResult<IReadOnlyList<DownloadFile>> result = await _manifest.GetAssetFilesAsync(json, currentDir, false, cancellationToken).ConfigureAwait(false);
        return result.Success && result.Value != null ? result.Value : Array.Empty<DownloadFile>();
    }

    public bool CompressNative(IReadOnlyList<Lib> libs, string currentDir, string versionName, bool overwrite = false)
    {
        try
        {
            string root = _paths.NormalizeRoot(currentDir);
            string outputPath = _paths.GetNativesDirectory(root, versionName);
            foreach (Lib lib in libs.Where(lib => lib.rules.Any(rule => rule.Os == DeviceOs.Windows && rule.IsAllow)))
            {
                foreach (var classifier in lib.classifiers.Where(pair => pair.Key.Contains("windows", StringComparison.OrdinalIgnoreCase)))
                {
                    string path = _paths.GetLibraryPath(root, classifier.Value.path);
                    if (!File.Exists(path)) return false;
                    DirFileUtil.CompressZip(path, outputPath, overwrite);
                }
                if (lib.name.Contains("natives-windows", StringComparison.OrdinalIgnoreCase))
                {
                    string path = _paths.GetLibraryPath(root, lib.path);
                    if (!File.Exists(path)) return false;
                    DirFileUtil.CompressZip(path, outputPath, overwrite);
                }
            }
            DirFileUtil.DeleteDirAllContent(Path.Combine(outputPath, "META-INF"));
            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            return false;
        }
    }

    public bool CheckAndGenerateLauncherProfile(string currentDir)
    {
        string profilePath = Path.Combine(_paths.NormalizeRoot(currentDir), "launcher_profiles.json");
        var root = new JObject {
            ["clientToken"] = "12138121381213812138121381213888",
            ["profiles"] = new JObject {
                ["SFMCL"] = new JObject {
                    ["icon"] = "ShulkerBox",
                    ["name"] = _launcherName,
                    ["lastVersionId"] = "latest-release",
                    ["type"] = "latest-release",
                    ["lastUsed"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                }
            }
        };
        if (!File.Exists(profilePath)) { File.WriteAllText(profilePath, root.ToString()); return true; }
        try
        {
            string text = File.ReadAllText(profilePath);
            if (!(text.StartsWith('{') && text.EndsWith('}'))) { File.WriteAllText(profilePath, root.ToString()); return true; }
            JObject existing = JObject.Parse(text);
            existing["clientToken"] ??= root["clientToken"];
            existing["profiles"] ??= new JObject();
            ((JObject)existing["profiles"]!)["SFMCL"] ??= root["profiles"]!["SFMCL"];
            File.WriteAllText(profilePath, existing.ToString());
            return true;
        }
        catch { return false; }
    }

    public JObject TransformOptiFineJson(string json, string versionName, string outputJson)
    {
        JObject result = JObject.Parse(json);
        JObject installer = JObject.Parse(outputJson);
        var patches = new JArray(_resolver.ParseVersionJson(json, versionName, true));
        result["mainClass"] = installer["mainClass"];
        _resolver.VersionArgumentParse(result, installer);
        JArray libraries = result["libraries"] as JArray ?? new JArray();
        foreach (JToken library in installer["libraries"] as JArray ?? []) libraries.Add(library);
        patches.Add(installer);
        result["patches"] = patches;
        return result;
    }

    public JObject TransformLiteLoaderJson(string json, string versionName, string installerPath)
    {
        JObject result = _resolver.ParseVersionJson(json, versionName);
        using ZipArchive archive = ZipFile.OpenRead(installerPath);
        ZipArchiveEntry entry = archive.GetEntry("install_profile.json") ?? throw new InvalidDataException("install_profile.json not found in LiteLoader installer.");
        using StreamReader reader = new(entry.Open());
        JObject profile = JObject.Parse(reader.ReadToEnd())["versionInfo"] as JObject
            ?? throw new InvalidDataException("LiteLoader installer has no versionInfo.");
        var patches = new JArray(_resolver.ParseVersionJson(json, versionName, true));
        result["mainClass"] = profile["mainClass"];
        _resolver.VersionArgumentParse(result, profile);
        JArray libraries = result["libraries"] as JArray ?? new JArray();
        foreach (JToken library in profile["libraries"] as JArray ?? []) libraries.Add(library);
        patches.Add(profile);
        result["patches"] = patches;
        return result;
    }

    public JObject TransformForgeJson(string json, string versionName, string installerPath)
    {
        JObject result = _resolver.ParseVersionJson(json, versionName);
        using ZipArchive archive = ZipFile.OpenRead(installerPath);
        var patches = new JArray(_resolver.ParseVersionJson(json, versionName, true));
        ZipArchiveEntry? entry = archive.GetEntry("version.json");
        JObject? profile = null;
        if (entry != null)
        {
            using StreamReader reader = new(entry.Open());
            profile = JObject.Parse(reader.ReadToEnd());
            profile.Remove("_comment_");
        }
        else if ((entry = archive.GetEntry("install_profile.json")) != null)
        {
            using StreamReader reader = new(entry.Open());
            JObject install = JObject.Parse(reader.ReadToEnd());
            profile = install["versionInfo"] as JObject;
            if (profile == null) return result;
            if (install["install"]?["version"] is JToken version) profile["version"] = version.ToString().Split('-')[0].TrimStart("Forge ".ToCharArray());
        }
        if (profile == null) return result;
        result["mainClass"] = profile["mainClass"]?.ToString() ?? string.Empty;
        _resolver.VersionArgumentParse(result, profile);
        JArray libraries = result["libraries"] as JArray ?? new JArray();
        foreach (JToken library in profile["libraries"] as JArray ?? []) libraries.Add(library);
        patches.Add(profile);
        result["patches"] = patches;
        return result;
    }

    public async Task<JObject?> TransformFabricJsonAsync(string json, string versionName, FabricLoader loader, CancellationToken cancellationToken = default)
    {
        Uri uri = new($"{MinecraftManifestService.DefaultApi}/fabric-meta/v2/versions/loader/{loader.Mcversion}/{loader.Version}/profile/json");
        ManifestResult<string> response = await _manifest.GetJsonAsync(uri, cancellationToken).ConfigureAwait(false);
        if (!response.Success || response.Value == null) return null;
        JObject fabric = JObject.Parse(response.Value);
        JObject result = _resolver.ParseVersionJson(json, versionName);
        var patches = new JArray(_resolver.ParseVersionJson(json, versionName, true));
        result["mainClass"] = fabric["mainClass"];
        _resolver.VersionArgumentParse(result, fabric);
        JArray libraries = result["libraries"] as JArray ?? new JArray();
        foreach (JToken library in fabric["libraries"] as JArray ?? []) libraries.Add(library);
        patches.Add(fabric);
        result["patches"] = patches;
        return result;
    }

    public string TransformLiteLoaderInstallerDownloadApi(LiteLoader loader)
    {
        string mcVersion = loader.Mcversion is "1.8" or "1.9" ? $"{loader.Mcversion}.0" : loader.Mcversion;
        string snapshot = ($"{loader.Version.Replace("-SNAPSHOT", string.Empty)}-00-SNAPSHOT").Replace("1.9", "1.9.0");
        string stable = (loader.Version.Contains('_') ? loader.Version : $"{mcVersion}_00").Replace('_', '-');
        return loader.IsStable
            ? $"http://dl.liteloader.com/redist/{mcVersion}/liteloader-installer-{stable}.jar"
            : $"http://jenkins.liteloader.com/job/LiteLoaderInstaller%20{loader.Mcversion}/lastSuccessfulBuild/artifact/build/libs/liteloader-installer-{snapshot}.jar";
    }

    /// <summary>Repairs an existing OptiFine profile during game startup.</summary>
    public async Task<bool> RepairOptifineAsync(Lib optifine, string root, string versionName, bool isolation = false, CancellationToken cancellationToken = default)
    {
        string installerPath = GetNeedLibrariesFile([optifine], root)
            .FirstOrDefault(file => file.FilePath.EndsWith("-installer.jar", StringComparison.OrdinalIgnoreCase))?.FilePath
            ?? string.Empty;
        return await InstallOptifineArchiveAsync(installerPath, _paths.NormalizeRoot(root), versionName, isolation, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Repairs an existing Forge profile during game startup.</summary>
    public Task<bool> RepairForgeAsync(string json, string installerPath, string root, string versionName, CancellationToken cancellationToken = default) =>
        InstallForgeArchiveAsync(json, installerPath, _paths.NormalizeRoot(root), versionName, cancellationToken);

    public async Task HandleClientMappingsAsync(string json, string currentDir, JToken mapping, string mappingsPath, CancellationToken cancellationToken = default)
    {
        var (_, minecraftVersion, mcpVersion) = GetForgeFmlArgs(json);
        if (string.IsNullOrEmpty(minecraftVersion) || string.IsNullOrEmpty(mcpVersion)) return;
        cancellationToken.ThrowIfCancellationRequested();
        string first = Path.Combine(_paths.GetLibrariesDirectory(currentDir), "net", "minecraft", "client", minecraftVersion, $"client-{minecraftVersion}-mappings.txt");
        string second = Path.Combine(_paths.GetLibrariesDirectory(currentDir), "net", "minecraft", "client", $"{minecraftVersion}-{mcpVersion}", $"client-{minecraftVersion}-{mcpVersion}-mappings.txt");
        if (!File.Exists(mappingsPath))
        {
            var download = new DownloadFile($"{minecraftVersion}-mappings", first, mapping["url"]?.ToString() ?? string.Empty);
            DownloadBatchHandle? handle = await _downloads.StartBatchAsync([download], cancellationToken).ConfigureAwait(false);
            if (handle == null || !(await handle.Completion.WaitAsync(cancellationToken).ConfigureAwait(false)).Success) return;
            mappingsPath = first;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(first)!);
        Directory.CreateDirectory(Path.GetDirectoryName(second)!);
        if (!string.Equals(mappingsPath, first, StringComparison.OrdinalIgnoreCase)) File.Copy(mappingsPath, first, true);
        File.Copy(first, second, true);
    }

    private (string ForgeVersion, string MinecraftVersion, string McpVersion) GetForgeFmlArgs(string json)
    {
        string forge = string.Empty;
        string minecraft = string.Empty;
        string mcp = string.Empty;
        try
        {
            if (JObject.Parse(json)["arguments"]?["game"] is not JArray args) return (forge, minecraft, mcp);
            for (int index = 0; index + 1 < args.Count; index++)
            {
                string value = args[index]?.ToString() ?? string.Empty;
                if (value.Contains("--fml.neoForgeVersion", StringComparison.Ordinal) || value.Contains("--fml.forgeVersion", StringComparison.Ordinal)) forge = args[++index]?.ToString() ?? string.Empty;
                else if (value.Contains("--fml.mcVersion", StringComparison.Ordinal)) minecraft = args[++index]?.ToString() ?? string.Empty;
                else if (value.Contains("--fml.neoFormVersion", StringComparison.Ordinal) || value.Contains("--fml.mcpVersion", StringComparison.Ordinal)) mcp = args[++index]?.ToString() ?? string.Empty;
            }
        }
        catch (Exception exception) { Console.WriteLine(exception.Message); }
        return (forge, minecraft, mcp);
    }

    private static (string MinecraftVersion, string Type, string Patch) FormatOptifineName(string name)
    {
        string[] split = name.Split(':');
        string[] trueName = split[^1].Split('_');
        string[] version = trueName[0].Split('.');
        string minecraft = version.Length <= 2 && int.TryParse(version.ElementAtOrDefault(1), out int minor) && minor < 10
            ? $"{trueName[0]}.0"
            : trueName[0];
        return (minecraft, string.Join('_', trueName.Skip(1).Take(Math.Max(0, trueName.Length - 2))), trueName[^1]);
    }

    private static void AddLibraryDownload(List<DownloadFile> files, HashSet<string> paths, string name, string filePath, string urlPath, string? fallbackUrl, long size)
    {
        if (!paths.Add(filePath)) return;
        var file = new DownloadFile(name, filePath, urlPath) { Size = size };
        if (!string.IsNullOrEmpty(fallbackUrl)) file.UrlPaths.Add(fallbackUrl);
        files.Add(file);
    }

    private LoaderInstallPlan CreatePlan(
        MinecraftLoader loader,
        string minecraftVersion,
        string versionName,
        string root,
        params (string Name, LoaderInstallStepKind Kind, DownloadFile? Download)[] values)
    {
        LoaderInstallStep[] steps = values.Select((value, index) => new LoaderInstallStep(
            index + 1,
            $"{index + 1}. {value.Name}",
            value.Kind,
            value.Download)).ToArray();
        return new LoaderInstallPlan(loader, minecraftVersion, versionName, root, true, steps);
    }

    private static DownloadFile CreateMinecraftJar(string minecraftVersion, string versionName, string versionPath) => new() {
        Name = $"{versionName}-jar",
        UrlPath = $"{MinecraftManifestService.DefaultApi}/version/{minecraftVersion}/client",
        UrlPaths = [$"{MinecraftManifestService.DefaultApi}/version/{minecraftVersion}/client"],
        FilePath = Path.Combine(versionPath, $"{versionName}.jar")
    };

    private static string VersionJsonUrl(string minecraftVersion) =>
        $"{MinecraftManifestService.DefaultApi}/version/{minecraftVersion}/json";
}
