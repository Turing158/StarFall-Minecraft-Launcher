using System.IO;
using System.IO.Compression;
using Newtonsoft.Json.Linq;
using StarFallMC.Entity;
using StarFallMC.Entity.Loader;
using StarFallMC.Entity.Resource;
using StarFallMC.Services.Download;
using StarFallMC.Services.Minecraft.Models;
using StarFallMC.Util;

namespace StarFallMC.Services.Minecraft;

public sealed partial class LoaderInstallService
{
    public async Task<bool> InstallMinecraftAsync(string minecraftVersion, string versionName, string currentDir, CancellationToken cancellationToken = default)
    {
        LoaderInstallPlan plan = CreateMinecraftPlan(minecraftVersion, versionName, currentDir);
        LoaderInstallResult result = await ExecuteAsync(plan, cancellationToken).ConfigureAwait(false);
        return result.Success;
    }

    public async Task<bool> InstallOptifineAsync(string minecraftVersion, string versionName, string currentDir, OptifineLoader loader, bool isolation = false, CancellationToken cancellationToken = default)
    {
        LoaderInstallPlan plan = CreateOptifinePlan(minecraftVersion, versionName, currentDir, loader);
        string root = _paths.NormalizeRoot(currentDir);
        string installerPath = plan.Steps[0].Download!.FilePath;
        LoaderInstallResult result = await ExecuteAsync(
            plan,
            cancellationToken,
            transform: (baseJson, _) => Task.FromResult<JObject?>(_resolver.ParseVersionJson(baseJson, versionName)),
            afterDownload: async (json, _) => await InstallOptifineProfileAsync(json, installerPath, root, versionName, loader, cancellationToken).ConfigureAwait(false),
            requiredDownloads: [plan.Steps[0].Download!]).ConfigureAwait(false);
        return result.Success;
    }

    public async Task<bool> InstallLiteLoaderAsync(string minecraftVersion, string versionName, string currentDir, LiteLoader loader, CancellationToken cancellationToken = default)
    {
        LoaderInstallPlan plan = CreateLiteLoaderPlan(minecraftVersion, versionName, currentDir, loader);
        string root = _paths.NormalizeRoot(currentDir);
        string installerPath = plan.Steps[0].Download!.FilePath;
        LoaderInstallResult result = await ExecuteAsync(
            plan,
            cancellationToken,
            transform: (baseJson, _) => Task.FromResult<JObject?>(TransformLiteLoaderJson(baseJson, versionName, installerPath)),
            beforeResolve: async (_, _) => await InstallLiteLoaderArchiveAsync(installerPath, root, cancellationToken).ConfigureAwait(false),
            requiredDownloads: [plan.Steps[0].Download!]).ConfigureAwait(false);
        return result.Success;
    }

    public async Task<string?> InstallForgeAsync(
        string minecraftVersion,
        string versionName,
        string currentDir,
        ForgeLoader loader,
        OptifineLoader? needOptifine = null,
        CancellationToken cancellationToken = default,
        IReadOnlyList<DownloadFile>? extraFiles = null,
        bool isNeedAutoFinish = true,
        bool isolation = false)
    {
        LoaderInstallPlan plan = CreateForgePlan(minecraftVersion, versionName, currentDir, loader, needOptifine);
        string root = _paths.NormalizeRoot(currentDir);
        string installerPath = plan.Steps[0].Download!.FilePath;
        DownloadFile? optifineFile = needOptifine == null ? null : new DownloadFile(
            needOptifine.DisplayName,
            Path.Combine(_paths.GetVersionDirectory(root, versionName), $"{needOptifine.DisplayName}.jar"),
            $"{MinecraftManifestService.DefaultOptifineApi}/{needOptifine.Mcversion}/{needOptifine.Type}/{needOptifine.Patch}");
        LoaderInstallResult result = await ExecuteAsync(
            plan,
            cancellationToken,
            transform: (baseJson, _) => Task.FromResult<JObject?>(TransformForgeJson(baseJson, versionName, installerPath)),
            afterDownload: async (json, _) =>
            {
                await HandleMappingsForInstallAsync(json, root, minecraftVersion, false, cancellationToken).ConfigureAwait(false);
                return await InstallForgeArchiveAsync(json, installerPath, root, versionName, cancellationToken).ConfigureAwait(false);
            },
            afterPrimaryInstall: optifineFile == null ? null : async (_, _) => await InstallOptifineArchiveAsync(optifineFile.FilePath, root, versionName, isolation, cancellationToken).ConfigureAwait(false),
            extraFiles: extraFiles,
            additionalFiles: (json, installRoot) => GetForgeAdditionalDownloads(json, installRoot, minecraftVersion, optifineFile, false),
            autoFinish: isNeedAutoFinish,
            requiredDownloads: [plan.Steps[0].Download!]).ConfigureAwait(false);
        return result.Success ? result.ProcessKey : null;
    }

    public async Task<string?> InstallFabricAsync(
        string minecraftVersion,
        string versionName,
        string currentDir,
        FabricLoader loader,
        MinecraftResource? fabricApi = null,
        CancellationToken cancellationToken = default,
        IReadOnlyList<DownloadFile>? extraFiles = null,
        bool isNeedAutoFinish = true)
    {
        LoaderInstallPlan plan = CreateFabricPlan(minecraftVersion, versionName, currentDir, loader, fabricApi);
        string root = _paths.NormalizeRoot(currentDir);
        LoaderInstallResult result = await ExecuteAsync(
            plan,
            cancellationToken,
            transform: async (baseJson, _) => await TransformFabricJsonAsync(baseJson, versionName, loader, cancellationToken).ConfigureAwait(false),
            extraFiles: extraFiles,
            additionalFiles: fabricApi == null ? null : (_, _) =>
            {
                DownloadFile? file = GetFabricApiDownload(fabricApi, root, versionName);
                return file == null ? Array.Empty<DownloadFile>() : [file];
            },
            autoFinish: isNeedAutoFinish).ConfigureAwait(false);
        return result.Success ? result.ProcessKey : null;
    }

    public async Task<string?> InstallNeoForgeAsync(
        string minecraftVersion,
        string versionName,
        string currentDir,
        NeoForgeLoader loader,
        CancellationToken cancellationToken = default,
        IReadOnlyList<DownloadFile>? extraFiles = null,
        bool isNeedAutoFinish = true)
    {
        LoaderInstallPlan plan = CreateNeoForgePlan(minecraftVersion, versionName, currentDir, loader);
        string root = _paths.NormalizeRoot(currentDir);
        string installerPath = plan.Steps[0].Download!.FilePath;
        LoaderInstallResult result = await ExecuteAsync(
            plan,
            cancellationToken,
            transform: (baseJson, _) => Task.FromResult<JObject?>(TransformForgeJson(baseJson, versionName, installerPath)),
            afterDownload: async (json, _) =>
            {
                await HandleMappingsForInstallAsync(json, root, minecraftVersion, true, cancellationToken).ConfigureAwait(false);
                return await InstallForgeArchiveAsync(json, installerPath, root, versionName, cancellationToken).ConfigureAwait(false);
            },
            extraFiles: extraFiles,
            additionalFiles: (json, installRoot) => GetForgeAdditionalDownloads(json, installRoot, minecraftVersion, null, true),
            autoFinish: isNeedAutoFinish,
            requiredDownloads: [plan.Steps[0].Download!]).ConfigureAwait(false);
        return result.Success ? result.ProcessKey : null;
    }

    private async Task<LoaderInstallResult> ExecuteAsync(
        LoaderInstallPlan plan,
        CancellationToken cancellationToken,
        Func<string, string, Task<JObject?>>? transform = null,
        Func<string, string, Task<bool>>? beforeResolve = null,
        Func<string, string, Task<bool>>? afterDownload = null,
        Func<string, string, Task<bool>>? afterPrimaryInstall = null,
        IReadOnlyList<DownloadFile>? extraFiles = null,
        Func<string, string, IReadOnlyList<DownloadFile>>? additionalFiles = null,
        bool autoFinish = true,
        IReadOnlyList<DownloadFile>? requiredDownloads = null)
    {
        string processKey = _interaction.Begin(plan.VersionName, plan.Steps.Select(step => step.Name).ToArray());
        string root = plan.RootDirectory;
        string versionPath = _paths.GetVersionDirectory(root, plan.VersionName);
        Directory.CreateDirectory(versionPath);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var required = (requiredDownloads ?? Array.Empty<DownloadFile>()).Where(file => !File.Exists(file.FilePath)).ToList();
            if (required.Count > 0 && !await DownloadFilesAsync(required, cancellationToken).ConfigureAwait(false))
            {
                _interaction.StepFailed(processKey);
                return new LoaderInstallResult(false, false, processKey, "Installer download failed.");
            }
            if (requiredDownloads is { Count: > 0 }) _interaction.StepCompleted(processKey);

            ManifestResult<string> manifest = await _manifest.GetVersionJsonAsync(plan.MinecraftVersion, cancellationToken).ConfigureAwait(false);
            if (!manifest.Success || manifest.Value == null)
            {
                _interaction.StepFailed(processKey);
                return new LoaderInstallResult(false, false, processKey, manifest.Error);
            }

            JObject versionJson = transform == null
                ? _resolver.ParseVersionJson(manifest.Value, plan.VersionName)
                : await transform(manifest.Value, versionPath).ConfigureAwait(false) ?? throw new InvalidDataException("Loader profile was not returned.");
            string json = versionJson.ToString();
            await File.WriteAllTextAsync(_paths.GetVersionJsonPath(root, plan.VersionName), json, cancellationToken).ConfigureAwait(false);
            _interaction.StepCompleted(processKey);

            if (beforeResolve != null)
            {
                if (!await beforeResolve(json, root).ConfigureAwait(false))
                {
                    _interaction.StepFailed(processKey);
                    return new LoaderInstallResult(false, false, processKey, "Loader installer failed.");
                }
                _interaction.StepCompleted(processKey);
            }

            List<Lib> libs = GetLibs(json);
            List<DownloadFile> libraryFiles = GetNeedLibrariesFile(libs, root);
            List<DownloadFile> assets = (await GetAssetsFileAsync(json, root, true, cancellationToken).ConfigureAwait(false)).ToList();
            var files = GetNeedDownloadFile(assets.Concat(libraryFiles));
            files.Insert(0, CreateMinecraftJar(plan.MinecraftVersion, plan.VersionName, versionPath));
            if (additionalFiles != null) files.AddRange(additionalFiles(json, root));
            if (extraFiles != null) files.AddRange(extraFiles);
            _interaction.StepCompleted(processKey);
            if (!await DownloadFilesAsync(files, cancellationToken).ConfigureAwait(false))
            {
                _interaction.StepFailed(processKey);
                return new LoaderInstallResult(false, false, processKey, "One or more files failed to download.");
            }
            _interaction.StepCompleted(processKey);

            if (afterDownload != null && !await afterDownload(json, root).ConfigureAwait(false))
            {
                _interaction.StepFailed(processKey);
                return new LoaderInstallResult(false, false, processKey, "Loader installer failed.");
            }
            if (afterDownload != null) _interaction.StepCompleted(processKey);
            if (afterPrimaryInstall != null)
            {
                if (!await afterPrimaryInstall(json, root).ConfigureAwait(false))
                {
                    _interaction.StepFailed(processKey);
                    return new LoaderInstallResult(false, false, processKey, "Optional loader installation failed.");
                }
                _interaction.StepCompleted(processKey);
            }
            if (autoFinish) _interaction.StepCompleted(processKey);
            return new LoaderInstallResult(true, false, processKey);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new LoaderInstallResult(false, true, processKey, "Installation cancelled.");
        }
        catch (Exception exception)
        {
            _interaction.StepFailed(processKey);
            _interaction.Notify(exception.Message);
            return new LoaderInstallResult(false, false, processKey, exception.Message);
        }
    }

    private async Task<bool> DownloadFilesAsync(IReadOnlyList<DownloadFile> files, CancellationToken cancellationToken)
    {
        if (files.Count == 0) return true;
        IReadOnlyList<DownloadFile> pending = files;
        while (pending.Count > 0)
        {
            DownloadBatchHandle? handle = await _downloads.StartBatchAsync(pending, cancellationToken).ConfigureAwait(false);
            if (handle == null) return false;
            DownloadBatchResult result = await handle.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
            pending = result.Files.Where(file => !file.Success && !file.Cancelled).Select(file => file.File).ToArray();
            if (pending.Count == 0) return result.Success;
            InstallRetryDecision decision = await _interaction.ChooseRetryAsync(pending, cancellationToken).ConfigureAwait(false);
            if (decision == InstallRetryDecision.SkipAndExport) NetworkUtil.ExportErrorFile(pending.ToList());
            if (decision != InstallRetryDecision.Retry) return false;
        }
        return true;
    }

    private IReadOnlyList<DownloadFile> GetForgeAdditionalDownloads(
        string json,
        string root,
        string minecraftVersion,
        DownloadFile? optifineFile,
        bool isNeoForge)
    {
        var files = new List<DownloadFile>();
        DownloadFile? forgeFml = GetForgeFmlDownloadFile(json, root);
        if (forgeFml != null) files.Add(forgeFml);
        var mapping = GetMappingsDownloadFile(json, root, minecraftVersion, isNeoForge);
        if (mapping.Download != null && !File.Exists(mapping.Download.FilePath)) files.Add(mapping.Download);
        if (optifineFile != null && !File.Exists(optifineFile.FilePath)) files.Add(optifineFile);
        return files;
    }

    private async Task HandleMappingsForInstallAsync(
        string json,
        string root,
        string minecraftVersion,
        bool isNeoForge,
        CancellationToken cancellationToken)
    {
        var mapping = GetMappingsDownloadFile(json, root, minecraftVersion, isNeoForge);
        if (mapping.Mapping != null && mapping.Path != null)
        {
            await HandleClientMappingsAsync(json, root, mapping.Mapping, mapping.Path, cancellationToken).ConfigureAwait(false);
        }
    }

    private static DownloadFile? GetFabricApiDownload(MinecraftResource resource, string root, string versionName)
    {
        if (resource.Downloaders.Count == 0) return null;
        DownloadFile file = resource.Downloaders[0].File;
        string directory = Path.Combine(root, "versions", versionName, "mods");
        file.FilePath = Path.Combine(directory, file.Name);
        return file;
    }

    private async Task<bool> InstallOptifineProfileAsync(
        string baseJson,
        string installerPath,
        string root,
        string versionName,
        OptifineLoader loader,
        CancellationToken cancellationToken)
    {
        string tempMinecraftRoot = Path.Combine(DirFileUtil.RoamingPath, ".minecraft");
        string tempVersionPath = Path.Combine(tempMinecraftRoot, "versions", loader.Mcversion);
        try
        {
            Directory.CreateDirectory(tempVersionPath);
            File.Copy(_paths.GetVersionJarPath(root, versionName), Path.Combine(tempVersionPath, $"{loader.Mcversion}.jar"), true);
            File.Copy(_paths.GetVersionJsonPath(root, versionName), Path.Combine(tempVersionPath, $"{loader.Mcversion}.json"), true);
            CheckAndGenerateLauncherProfile(root);
            ProcessResult result = await _processRunner.RunAsync("java", $"-cp \"{installerPath}\" optifine.Installer", cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0) return false;
            string outputLine = result.Output.FirstOrDefault(line => line.Contains("Dir version MC-OF: ", StringComparison.Ordinal)) ?? string.Empty;
            string outputPath = outputLine.Replace("Dir version MC-OF: ", string.Empty, StringComparison.Ordinal).Trim();
            if (string.IsNullOrEmpty(outputPath)) return false;
            string outputJsonPath = Path.Combine(outputPath, $"{Path.GetFileName(outputPath)}.json");
            if (!File.Exists(outputJsonPath)) return false;
            JObject transformed = TransformOptiFineJson(baseJson, versionName, await File.ReadAllTextAsync(outputJsonPath, cancellationToken).ConfigureAwait(false));
            await File.WriteAllTextAsync(_paths.GetVersionJsonPath(root, versionName), transformed.ToString(), cancellationToken).ConfigureAwait(false);

            string optifineVersion = $"{loader.Mcversion}_{loader.Type}_{loader.Patch}";
            string relativeLibrary = Path.Combine("optifine", "OptiFine", optifineVersion, $"OptiFine-{optifineVersion}.jar");
            string source = Path.Combine(tempMinecraftRoot, "libraries", relativeLibrary);
            string target = _paths.GetLibraryPath(root, relativeLibrary);
            if (!File.Exists(source)) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, true);
            return true;
        }
        finally
        {
            TryDeleteFile(installerPath);
            TryDeleteDirectory(tempVersionPath);
            TryDeleteDirectory(Path.Combine(tempMinecraftRoot, "libraries", "optifine"));
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private async Task<bool> InstallOptifineArchiveAsync(string installerPath, string root, string versionName, bool isolation, CancellationToken cancellationToken)
    {
        string gameDirectory = _paths.GetGameDirectory(root, versionName, isolation);
        string mods = Path.Combine(gameDirectory, "mods");
        Directory.CreateDirectory(mods);
        if (!File.Exists(installerPath)) return false;
        string versionJar = _paths.GetVersionJarPath(root, versionName);
        string output = Path.Combine(mods, Path.GetFileName(installerPath));
        string arguments = $"-cp \"{installerPath}\" optifine.Patcher \"{versionJar}\" \"{installerPath}\" \"{output}\"";
        ProcessResult result = await _processRunner.RunAsync("java", arguments, cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    private async Task<bool> InstallLiteLoaderArchiveAsync(string installerPath, string root, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CheckAndGenerateLauncherProfile(root);
        using ZipArchive archive = ZipFile.OpenRead(installerPath);
        ZipArchiveEntry? profileEntry = archive.GetEntry("install_profile.json");
        if (profileEntry == null) return false;
        using StreamReader reader = new(profileEntry.Open());
        JObject profile = JObject.Parse(await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false));
        string mcVersion = profile["install"]?["minecraft"]?.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(mcVersion)) return false;
        foreach (ZipArchiveEntry entry in archive.Entries.Where(entry => entry.FullName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) && entry.FullName.Contains($"liteloader-{mcVersion}", StringComparison.OrdinalIgnoreCase)))
        {
            string target = Path.Combine(root, "libraries", "com", "mumfrey", "liteloader", mcVersion, Path.GetFileName(entry.FullName));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, true);
            return true;
        }
        return false;
    }

    private async Task<bool> InstallForgeArchiveAsync(string json, string installerPath, string root, string versionName, CancellationToken cancellationToken)
    {
        CheckAndGenerateLauncherProfile(root);
        JObject installerJson;
        using (ZipArchive archive = ZipFile.OpenRead(installerPath))
        {
            ZipArchiveEntry? profile = archive.GetEntry("install_profile.json");
            if (profile == null) return false;
            using StreamReader reader = new(profile.Open());
            installerJson = JObject.Parse(await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false));
        }
        string tempVersion = installerJson["install"]?["version"]?.ToString() ?? installerJson["version"]?.ToString() ?? string.Empty;
        string tempMinecraft = installerJson["install"]?["minecraft"]?.ToString() ?? installerJson["minecraft"]?.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(tempVersion) || string.IsNullOrEmpty(tempMinecraft)) return false;
        string tempRoot = Path.Combine(root, "versions_tmp");
        string tempMinecraftPath = Path.Combine(root, "versions", tempMinecraft);
        string tempVersionPath = Path.Combine(root, "versions", tempVersion);
        Directory.CreateDirectory(tempRoot);
        bool oldMinecraft = DirFileUtil.MoveExistVersionDirToTmpDir(tempMinecraftPath, tempRoot);
        bool oldVersion = DirFileUtil.MoveExistVersionDirToTmpDir(tempVersionPath, tempRoot);
        try
        {
            Directory.CreateDirectory(tempMinecraftPath);
            string sourceJar = _paths.GetVersionJarPath(root, versionName);
            File.Copy(sourceJar, Path.Combine(tempMinecraftPath, $"{tempMinecraft}.jar"), true);
            string forgeInstaller = DirFileUtil.GetAndCompressAssetResource("forge-install.jar");
            string arguments = $"-cp \"{forgeInstaller};{installerPath}\" com.bangbang93.ForgeInstaller \"{root}\"";
            ProcessResult result = await _processRunner.RunAsync("java", arguments, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0 || !result.Output.Any(line => line.Contains("true", StringComparison.OrdinalIgnoreCase))) return false;
            return true;
        }
        finally
        {
            DirFileUtil.MoveTmpVersionDirToVersionDir(tempMinecraftPath, tempRoot, oldMinecraft);
            DirFileUtil.MoveTmpVersionDirToVersionDir(tempVersionPath, tempRoot, oldVersion);
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }
}
