using StarFallMC.Util;
using System.Net.Http;
using StarFallMC.Entity.Resource;

namespace StarFallMC.Services.Resources;

public sealed class ResourceServiceContainer : IDisposable
{
    private readonly bool _ownsHttpClient;
    private int _disposed;

    public ResourceServiceContainer(
        HttpClient? httpClient = null,
        Uri? modrinthBaseUri = null,
        Uri? curseForgeBaseUri = null,
        string? curseForgeApiKey = null,
        ResourceCache? cache = null)
    {
        HttpClient = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient == null;
        Fingerprints = new ResourceFingerprintService();
        Scanner = new LocalResourceScanner(Fingerprints);
        Mapper = new ResourceDtoMapper();
        Cache = cache ?? new ResourceCache();
        Modrinth = new ModrinthClient(HttpClient, modrinthBaseUri, PropertiesUtil.UserAgent);
        CurseForge = new CurseForgeClient(
            HttpClient,
            curseForgeApiKey ?? KeyUtil.CURSEFORGE_API_KEY,
            curseForgeBaseUri,
            PropertiesUtil.UserAgent);
        LocalCatalog = new LocalResourceCatalogService(this);
    }

    public HttpClient HttpClient { get; }
    public ResourceFingerprintService Fingerprints { get; }
    public LocalResourceScanner Scanner { get; }
    public ResourceDtoMapper Mapper { get; }
    public ResourceCache Cache { get; }
    public ModrinthClient Modrinth { get; }
    public CurseForgeClient CurseForge { get; }
    public LocalResourceCatalogService LocalCatalog { get; }
    public IReadOnlyList<McModData> Localizations { get; private set; } = [];

    public void SetLocalizations(IEnumerable<McModData> localizations)
    {
        Localizations = localizations?.ToList() ?? [];
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && _ownsHttpClient) HttpClient.Dispose();
    }
}

public static class ResourceServices
{
    private static readonly object Sync = new();
    private static ResourceServiceContainer _current = new();

    public static ResourceServiceContainer Current
    {
        get
        {
            lock (Sync) return _current;
        }
    }

    public static void Configure(ResourceServiceContainer services)
    {
        ArgumentNullException.ThrowIfNull(services);
        lock (Sync)
        {
            ResourceServiceContainer previous = _current;
            _current = services;
            if (!ReferenceEquals(previous, services)) previous.Dispose();
        }
    }
}
