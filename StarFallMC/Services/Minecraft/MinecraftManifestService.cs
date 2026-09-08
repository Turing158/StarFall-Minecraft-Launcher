using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StarFallMC.Entity;

namespace StarFallMC.Services.Minecraft;

public sealed record ManifestResult<T>(bool Success, T? Value, string? Error, bool FromCache = false)
{
    public static ManifestResult<T> Failure(string error) => new(false, default, error);
    public static ManifestResult<T> Ok(T value, bool fromCache = false) => new(true, value, null, fromCache);
}

/// <summary>
/// Owns long-lived HTTP access and small manifest/asset-index cache entries.
/// Failed or cancelled requests never enter the cache.
/// </summary>
public sealed class MinecraftManifestService : IDisposable
{
    public const string DefaultApi = "https://bmclapi2.bangbang93.com";
    public const string DefaultAssetsApi = DefaultApi + "/assets";
    public const string DefaultMavenApi = DefaultApi + "/maven/";
    public const string DefaultOptifineApi = DefaultApi + "/optifine";

    private readonly HttpClient _httpClient;
    private readonly MinecraftPathService _paths;
    private readonly Uri _apiBase;
    private readonly Uri _assetsBase;
    private readonly TimeSpan _cacheLifetime;
    private readonly int _cacheCapacity;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private int _disposed;

    public MinecraftManifestService(
        HttpClient? httpClient = null,
        MinecraftPathService? paths = null,
        Uri? apiBase = null,
        Uri? assetsBase = null,
        TimeSpan? cacheLifetime = null,
        int cacheCapacity = 64)
    {
        _httpClient = httpClient ?? new HttpClient();
        _paths = paths ?? new MinecraftPathService();
        _apiBase = (apiBase ?? new Uri(DefaultApi + "/", UriKind.Absolute));
        _assetsBase = (assetsBase ?? new Uri(DefaultAssetsApi + "/", UriKind.Absolute));
        _cacheLifetime = cacheLifetime ?? TimeSpan.FromMinutes(10);
        _cacheCapacity = Math.Max(1, cacheCapacity);
    }

    public Task<ManifestResult<string>> GetVersionJsonAsync(string version, CancellationToken cancellationToken = default) =>
        GetTextAsync(new Uri(_apiBase, $"version/{Uri.EscapeDataString(version)}/json"), cancellationToken);

    public Task<ManifestResult<string>> GetJsonAsync(Uri uri, CancellationToken cancellationToken = default) =>
        GetTextAsync(uri, cancellationToken);

    public async Task<ManifestResult<string>> GetVersionJsonFromFileOrRemoteAsync(
        string? existingPath,
        string version,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(existingPath) && File.Exists(existingPath))
        {
            try
            {
                string content = await File.ReadAllTextAsync(existingPath, cancellationToken).ConfigureAwait(false);
                JObject.Parse(content);
                return ManifestResult<string>.Ok(content, fromCache: true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // A damaged local file is not cached and may be repaired remotely.
            }
        }

        return await GetVersionJsonAsync(version, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ManifestResult<IReadOnlyList<DownloadFile>>> GetAssetFilesAsync(
        string json,
        string currentDir,
        bool forceDownload = false,
        CancellationToken cancellationToken = default)
    {
        JObject root;
        try
        {
            root = JObject.Parse(json);
            JToken asset = root["assetIndex"] ?? throw new InvalidDataException("Version JSON has no assetIndex.");
            string assetId = asset["id"]?.ToString() ?? throw new InvalidDataException("Asset index has no id.");
            string assetUrl = asset["url"]?.ToString() ?? throw new InvalidDataException("Asset index has no URL.");
            string assetPath = _paths.GetAssetIndexPath(currentDir, assetId);
            var assetFile = new DownloadFile($"assets/indexes/{assetId}.json", assetPath, BuildAssetMirrorUrl(assetUrl));
            assetFile.UrlPaths.Add(assetUrl);

            if (forceDownload || !File.Exists(assetPath))
            {
                return ManifestResult<IReadOnlyList<DownloadFile>>.Failure(
                    "Asset index is not available locally; the caller must submit the asset-index DownloadFile.");
            }

            string assetJson = await File.ReadAllTextAsync(assetPath, cancellationToken).ConfigureAwait(false);
            JObject assetRoot = JObject.Parse(assetJson);
            if (assetRoot["objects"] is not JObject objects)
            {
                throw new InvalidDataException("Asset index has no objects.");
            }

            var files = new List<DownloadFile>();
            foreach (JProperty property in objects.Properties())
            {
                string hash = property.Value["hash"]?.ToString() ?? throw new InvalidDataException("Asset object has no hash.");
                string objectPath = _paths.GetAssetObjectPath(currentDir, hash);
                var file = new DownloadFile(property.Name, objectPath, new Uri(_assetsBase, $"{hash[..2]}/{hash}").ToString()) {
                    Size = property.Value["size"]?.Value<long>() ?? -1
                };
                files.Add(file);
            }

            return ManifestResult<IReadOnlyList<DownloadFile>>.Ok(files, fromCache: !forceDownload);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ManifestResult<IReadOnlyList<DownloadFile>>.Failure(exception.Message);
        }
    }

    public DownloadFile CreateAssetIndexDownloadFile(string json, string currentDir)
    {
        JObject root = JObject.Parse(json);
        JToken asset = root["assetIndex"] ?? throw new InvalidDataException("Version JSON has no assetIndex.");
        string assetId = asset["id"]?.ToString() ?? throw new InvalidDataException("Asset index has no id.");
        string assetUrl = asset["url"]?.ToString() ?? throw new InvalidDataException("Asset index has no URL.");
        var file = new DownloadFile($"assets/indexes/{assetId}.json", _paths.GetAssetIndexPath(currentDir, assetId), BuildAssetMirrorUrl(assetUrl));
        file.UrlPaths.Add(assetUrl);
        file.Size = asset["size"]?.Value<long>() ?? -1;
        return file;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _httpClient.Dispose();
            _cache.Clear();
        }
    }

    private async Task<ManifestResult<string>> GetTextAsync(Uri uri, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        string key = uri.ToString();
        if (_cache.TryGetValue(key, out CacheEntry? cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return ManifestResult<string>.Ok(cached.Content, fromCache: true);
        }

        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return ManifestResult<string>.Failure($"HTTP {(int)response.StatusCode} ({response.StatusCode}).");
            }

            string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            JObject.Parse(content);
            AddCacheEntry(key, content);
            return ManifestResult<string>.Ok(content);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (JsonException exception)
        {
            return ManifestResult<string>.Failure($"Invalid manifest JSON: {exception.Message}");
        }
        catch (HttpRequestException exception)
        {
            return ManifestResult<string>.Failure(exception.Message);
        }
        catch (Exception exception)
        {
            return ManifestResult<string>.Failure(exception.Message);
        }
    }

    private string BuildAssetMirrorUrl(string originalUrl)
    {
        if (!Uri.TryCreate(originalUrl, UriKind.Absolute, out Uri? uri))
        {
            throw new InvalidDataException("Asset index URL is invalid.");
        }

        return new Uri(_apiBase, uri.AbsolutePath.TrimStart('/') + uri.Query).ToString();
    }

    private void AddCacheEntry(string key, string content)
    {
        if (_cache.Count >= _cacheCapacity)
        {
            string? oldestKey = _cache.OrderBy(item => item.Value.CreatedAt).Select(item => item.Key).FirstOrDefault();
            if (oldestKey != null) _cache.TryRemove(oldestKey, out _);
        }
        _cache[key] = new CacheEntry(content, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow + _cacheLifetime);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }

    private sealed record CacheEntry(string Content, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
}
