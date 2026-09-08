using System.Net.Http;
using StarFallMC.Util;

namespace StarFallMC.Services.Minecraft;

/// <summary>
/// Application composition root for Minecraft services. Pages depend on this
/// container through the application root instead of static Minecraft helpers.
/// </summary>
public sealed class MinecraftServiceContainer : IDisposable
{
    public MinecraftServiceContainer(
        HttpClient? httpClient = null,
        ILoaderInstallInteraction? installInteraction = null,
        IMinecraftLaunchInteraction? launchInteraction = null,
        IMinecraftDownloadClient? downloadClient = null)
    {
        Paths = new MinecraftPathService();
        Resolver = new MinecraftVersionResolver();
        Manifest = new MinecraftManifestService(httpClient, Paths);
        Java = new JavaDiscoveryService();
        Memory = new SystemMemoryService();
        Arguments = new MinecraftArgumentBuilder(Resolver, Java, Memory);
        Loader = new LoaderInstallService(
            Paths,
            Resolver,
            Manifest,
            downloads: downloadClient,
            interaction: installInteraction,
            launcherName: PropertiesUtil.LauncherName);
        Launch = new MinecraftLaunchService(Paths, Resolver, Arguments, Loader, Java, interaction: launchInteraction ?? new NullMinecraftLaunchInteraction());
    }

    public MinecraftPathService Paths { get; }
    public MinecraftVersionResolver Resolver { get; }
    public MinecraftManifestService Manifest { get; }
    public JavaDiscoveryService Java { get; }
    public SystemMemoryService Memory { get; }
    public MinecraftArgumentBuilder Arguments { get; }
    public LoaderInstallService Loader { get; }
    public MinecraftLaunchService Launch { get; }

    public void Dispose()
    {
        Launch.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Manifest.Dispose();
    }
}

public static class MinecraftServices
{
    private static readonly object Sync = new();
    private static MinecraftServiceContainer _current = new();

    public static MinecraftServiceContainer Current
    {
        get
        {
            lock (Sync) return _current;
        }
    }

    public static void Configure(MinecraftServiceContainer services)
    {
        ArgumentNullException.ThrowIfNull(services);
        lock (Sync)
        {
            MinecraftServiceContainer previous = _current;
            _current = services;
            if (!ReferenceEquals(previous, services)) previous.Dispose();
        }
    }
}
