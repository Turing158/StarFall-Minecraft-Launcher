using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using StarFallMC.Entity;
using StarFallMC.Entity.Enum;
using StarFallMC.Services.Download;
using StarFallMC.Util;

namespace StarFallMC.Services.Minecraft;

public sealed record MinecraftLaunchRequest(
    MinecraftItem Minecraft,
    Player Player,
    IGameSettingsState Settings,
    string RootDirectory,
    string LauncherName,
    string LauncherVersion,
    bool IsLaunch = true,
    Func<Player, CancellationToken, Task<Player?>>? AuthenticateAsync = null);

public sealed record MinecraftLaunchResult(bool Success, bool Cancelled, string? Error = null);

public interface IMinecraftLaunchInteraction
{
    void SetStatus(string status);
    Task<bool> RetryDownloadsAsync(IReadOnlyList<DownloadFile> failedFiles, CancellationToken cancellationToken);
    Task<bool> ConfirmUnsafeJavaAsync(string versionName, string? suitableJava, CancellationToken cancellationToken);
    Task HideLaunchingAsync(bool isStop);
    void ShowLaunchError(MinecraftItem minecraft, int exitCode);
}

public sealed class NullMinecraftLaunchInteraction : IMinecraftLaunchInteraction
{
    public void SetStatus(string status) { }
    public Task<bool> RetryDownloadsAsync(IReadOnlyList<DownloadFile> failedFiles, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> ConfirmUnsafeJavaAsync(string versionName, string? suitableJava, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task HideLaunchingAsync(bool isStop) => Task.CompletedTask;
    public void ShowLaunchError(MinecraftItem minecraft, int exitCode) { }
}

/// <summary>
/// Coordinates Minecraft preparation, argument construction and the lifetime
/// of the launched process. All state belongs to this service instance.
/// </summary>
public sealed class MinecraftLaunchService : IAsyncDisposable
{
    private readonly MinecraftPathService _paths;
    private readonly MinecraftVersionResolver _resolver;
    private readonly MinecraftArgumentBuilder _arguments;
    private readonly LoaderInstallService _loader;
    private readonly JavaDiscoveryService _java;
    private readonly IMinecraftDownloadClient _downloads;
    private readonly ProcessRunner _processRunner;
    private readonly IMinecraftLaunchInteraction _interaction;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private ProcessRun? _activeRun;
    private CancellationTokenSource? _monitorCts;
    private Task _monitorTask = Task.CompletedTask;

    public bool HasActiveProcess => _activeRun is { HasExited: false };

    public MinecraftLaunchService(
        MinecraftPathService? paths = null,
        MinecraftVersionResolver? resolver = null,
        MinecraftArgumentBuilder? arguments = null,
        LoaderInstallService? loader = null,
        JavaDiscoveryService? java = null,
        IMinecraftDownloadClient? downloads = null,
        ProcessRunner? processRunner = null,
        IMinecraftLaunchInteraction? interaction = null)
    {
        _paths = paths ?? new MinecraftPathService();
        _resolver = resolver ?? new MinecraftVersionResolver();
        _java = java ?? new JavaDiscoveryService();
        _arguments = arguments ?? new MinecraftArgumentBuilder(_resolver, _java);
        _loader = loader ?? new LoaderInstallService(_paths, _resolver);
        _downloads = downloads ?? new ApplicationMinecraftDownloadClient();
        _processRunner = processRunner ?? new ProcessRunner(outputCapacity: 1500, maxLineLength: 16 * 1024);
        _interaction = interaction ?? new NullMinecraftLaunchInteraction();
    }

    public async Task<MinecraftLaunchResult> StartAsync(MinecraftLaunchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            string root = _paths.NormalizeRoot(request.RootDirectory);
            string jsonPath = _paths.GetVersionJsonPath(root, request.Minecraft.Name);
            string json = await File.ReadAllTextAsync(jsonPath, cancellationToken).ConfigureAwait(false);
            List<Lib> libs = _resolver.GetLibs(json);

            _interaction.SetStatus("检查文件完整性...");
            List<DownloadFile> libraryFiles = _loader.GetNeedLibrariesFile(libs, root);
            DownloadFile? forgeFml = _loader.GetForgeFmlDownloadFile(json, root);
            if (forgeFml != null && request.Minecraft.Loader == MinecraftLoader.Forge) libraryFiles.Add(forgeFml);
            var mapping = _loader.GetMappingsDownloadFile(json, root);
            if (mapping.Download != null) libraryFiles.Add(mapping.Download);
            IReadOnlyList<DownloadFile> assets = await _loader.GetAssetsFileAsync(json, root, cancellationToken: cancellationToken).ConfigureAwait(false);
            var pending = LoaderInstallService.GetNeedDownloadFile(assets.Concat(libraryFiles));
            if (!await DownloadFilesAsync(pending, request, cancellationToken).ConfigureAwait(false))
            {
                return new MinecraftLaunchResult(false, false, "Required files were not downloaded.");
            }

            if (mapping.Mapping != null && mapping.Path != null)
            {
                await _loader.HandleClientMappingsAsync(json, root, mapping.Mapping, mapping.Path, cancellationToken).ConfigureAwait(false);
            }

            if (request.Minecraft.Loader == MinecraftLoader.Optifine)
            {
                Lib? optifine = libs.FirstOrDefault(lib => lib.name.Contains("OptiFine", StringComparison.OrdinalIgnoreCase));
                Lib? launchWrapper = libs.FirstOrDefault(lib => lib.name.Contains("launchwrapper", StringComparison.OrdinalIgnoreCase));
                bool needsRepair = optifine != null && launchWrapper != null
                    && (!File.Exists(_paths.GetLibraryPath(root, optifine.path)) || !File.Exists(_paths.GetLibraryPath(root, launchWrapper.path)));
                if (needsRepair)
                {
                    _interaction.SetStatus("补全OptiFine文件中...");
                    if (!await _loader.RepairOptifineAsync(optifine!, root, request.Minecraft.Name, request.Settings.IsIsolation, cancellationToken).ConfigureAwait(false))
                    {
                        return new MinecraftLaunchResult(false, false, "OptiFine files could not be repaired.");
                    }
                }
            }

            if (forgeFml != null && request.Minecraft.Loader == MinecraftLoader.Forge)
            {
                _interaction.SetStatus("补全Forge文件中...");
                await _loader.RepairForgeAsync(json, forgeFml.FilePath, root, request.Minecraft.Name, cancellationToken).ConfigureAwait(false);
            }

            Player player = request.Player;
            if (request.IsLaunch && player.IsOnline && request.AuthenticateAsync != null)
            {
                _interaction.SetStatus("正版登录...");
                player = await request.AuthenticateAsync(player, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Microsoft authentication failed.");
            }

            _interaction.SetStatus("解压动态链接库...");
            _loader.CompressNative(libs, root, request.Minecraft.Name);
            _interaction.SetStatus("MC准备启动...");

            string platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" : "osx";
            string arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
            string classpath = _resolver.GetClassPaths(libs, root, request.Minecraft.Name);
            var jvmSettings = new JvmArg(root, request.Minecraft.Name, request.LauncherName, request.LauncherVersion, classpath);
            var gameSettings = new MinecraftGameArgumentSettings(request.LauncherName, request.LauncherVersion,
                request.Settings.CustomInfo ?? string.Empty, request.Settings.GameWidth, request.Settings.GameHeight, request.Settings.IsFullScreen);
            string jvmArgs = _arguments.BuildJvmArguments(json, jvmSettings, platform, arch);
            string gameArgs = _arguments.BuildMinecraftArguments(json,
                new MinecraftArg(player.Name, request.Minecraft.Name, _paths.GetGameDirectory(root, request.Minecraft.Name, request.Settings.IsIsolation), _paths.GetAssetsDirectory(root), player.UUID, player.AccessToken),
                gameSettings);
            if (request.Settings.JvmExtraAreaEnable && !string.IsNullOrWhiteSpace(request.Settings.JvmExtra)) jvmArgs += " " + request.Settings.JvmExtra;
            if (!string.IsNullOrWhiteSpace(request.Settings.GameTailArgs)) gameArgs += " " + request.Settings.GameTailArgs;

            var (java, memory) = _arguments.SelectJavaAndMemory(json, request.Settings, request.Settings.JavaVersions.ToList());
            string resolvedJava = await _java.ResolveLegacyJavaAsync(json, java, required => _arguments.SelectJavaAndMemory(json, request.Settings, request.Settings.JavaVersions.ToList(), required), cancellationToken).ConfigureAwait(false);
            bool unsafeJavaConfirmed = false;
            if (string.IsNullOrEmpty(resolvedJava))
            {
                string suitable = _resolver.GetSuitableJava(json);
                unsafeJavaConfirmed = await _interaction.ConfirmUnsafeJavaAsync(request.Minecraft.Name, suitable, cancellationToken).ConfigureAwait(false);
                if (!unsafeJavaConfirmed)
                {
                    return new MinecraftLaunchResult(false, false, "No compatible Java runtime was selected.");
                }
            }
            else
            {
                java = resolvedJava;
            }

            if (!unsafeJavaConfirmed && !java.Contains(".exe", StringComparison.OrdinalIgnoreCase))
            {
                string suitable = _resolver.GetSuitableJava(json);
                if (!await _interaction.ConfirmUnsafeJavaAsync(request.Minecraft.Name, suitable, cancellationToken).ConfigureAwait(false))
                {
                    return new MinecraftLaunchResult(false, false, "No compatible Java runtime was selected.");
                }
            }

            string commandLine = memory + " " + jvmArgs + " " + gameArgs;
            await RunPreparedAsync(request.Minecraft, java, commandLine, request.Settings.WindowTitle, request.IsLaunch, cancellationToken).ConfigureAwait(false);
            return new MinecraftLaunchResult(true, false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await StopAsync().ConfigureAwait(false);
            return new MinecraftLaunchResult(false, true, "Launch cancelled.");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"启动出现问题：{exception}");
            await _interaction.HideLaunchingAsync(false).ConfigureAwait(false);
            return new MinecraftLaunchResult(false, false, exception.Message);
        }
    }

    public async Task StopAsync()
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            _monitorCts?.Cancel();
            ProcessRun? run = Interlocked.Exchange(ref _activeRun, null);
            if (run != null) await run.DisposeAsync().ConfigureAwait(false);
            try { await _monitorTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
            _monitorTask = Task.CompletedTask;
            _monitorCts?.Dispose();
            _monitorCts = null;
        }
        finally { _lifecycleGate.Release(); }
    }

    public async Task RunPreparedAsync(
        MinecraftItem minecraft,
        string executable,
        string arguments,
        string windowTitle,
        bool isLaunch = true,
        CancellationToken cancellationToken = default)
    {
        if (!isLaunch) return;
        await StartProcessAsync(minecraft, executable, arguments, windowTitle, cancellationToken).ConfigureAwait(false);
    }

    public Task WaitForExitAsync(CancellationToken cancellationToken = default) =>
        _monitorTask.WaitAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _lifecycleGate.Dispose();
    }

    private async Task<bool> DownloadFilesAsync(IReadOnlyList<DownloadFile> files, MinecraftLaunchRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<DownloadFile> pending = files;
        while (pending.Count > 0)
        {
            DownloadBatchHandle? handle = await _downloads.StartBatchAsync(pending, cancellationToken).ConfigureAwait(false);
            if (handle == null) return false;
            DownloadBatchResult result = await handle.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
            pending = result.Files.Where(file => !file.Success && !file.Cancelled).Select(file => file.File).ToArray();
            if (pending.Count == 0) return result.Success;
            if (request is null || !await _interaction.RetryDownloadsAsync(pending, cancellationToken).ConfigureAwait(false)) return false;
        }
        return true;
    }

    private async Task StartProcessAsync(MinecraftItem minecraft, string java, string arguments, string windowTitle, CancellationToken cancellationToken)
    {
        await StopAsync().ConfigureAwait(false);
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var startInfo = new ProcessStartInfo(java.Trim('"'))
            {
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            ProcessRun run = _processRunner.Start(startInfo, cancellationToken);
            _activeRun = run;
            _monitorCts = new CancellationTokenSource();
            _interaction.SetStatus("等待窗口中...");
            _monitorTask = MonitorAsync(minecraft, run, windowTitle, _monitorCts.Token);
        }
        finally { _lifecycleGate.Release(); }
    }

    private async Task MonitorAsync(MinecraftItem minecraft, ProcessRun run, string windowTitle, CancellationToken cancellationToken)
    {
        bool windowShown = false;
        try
        {
            while (!run.HasExited)
            {
                await Task.Delay(windowShown ? TimeSpan.FromSeconds(1) : TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
                bool hasWindow = false;
                if (!windowShown)
                {
                    try { hasWindow = ProcessUtil.HasProcessWindow(run.Process.Id); }
                    catch (ArgumentException) { }
                    catch (InvalidOperationException) { }
                }
                if (!windowShown && hasWindow)
                {
                    windowShown = true;
                    if (!string.IsNullOrWhiteSpace(windowTitle)) ProcessUtil.SetWindowTitle(run.Process.Id, windowTitle);
                    await _interaction.HideLaunchingAsync(false).ConfigureAwait(false);
                }
            }

            ProcessResult completion = await run.Completion.ConfigureAwait(false);
            if (!cancellationToken.IsCancellationRequested && completion.ExitCode != 0)
            {
                await _interaction.HideLaunchingAsync(false).ConfigureAwait(false);
                _interaction.ShowLaunchError(minecraft, completion.ExitCode);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) { Console.WriteLine($"Minecraft进程监控失败：{exception.Message}"); }
        finally
        {
            if (run.HasExited && ReferenceEquals(_activeRun, run))
            {
                Interlocked.CompareExchange(ref _activeRun, null, run);
                await run.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
