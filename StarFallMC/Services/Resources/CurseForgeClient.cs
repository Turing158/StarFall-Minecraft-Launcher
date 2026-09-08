using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StarFallMC.Entity.Enum;
using StarFallMC.Util;

namespace StarFallMC.Services.Resources;

public sealed class CurseForgeClient
{
    public const string DefaultApi = "https://api.curseforge.com";
    private static readonly HashSet<string> LoaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Forge", "NeoForge", "Fabric", "Quilt", "Liteloader"
    };

    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;
    private readonly string _apiKey;
    private readonly string _userAgent;

    public CurseForgeClient(HttpClient httpClient, string apiKey, Uri? baseUri = null, string userAgent = "StarFallMC")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey ?? string.Empty;
        _baseUri = baseUri ?? new Uri(DefaultApi + "/", UriKind.Absolute);
        _userAgent = string.IsNullOrWhiteSpace(userAgent) ? "StarFallMC" : userAgent;
    }

    public async Task<ApiResult<ResourcePageDto<CommunityResourceDto>>> SearchAsync(
        ResourceQuery query,
        int categoryId = -1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["gameId"] = "432",
                ["sortField"] = "6",
                ["sortOrder"] = "desc",
                ["classId"] = GetClassId(query.ResourceType).ToString(),
                ["index"] = query.Offset(pageSize).ToString(),
                ["pageSize"] = Math.Max(1, pageSize).ToString()
            };
            if (!IsAll(query.SelectedLoader)) parameters["modLoaderType"] = GetModLoaderId(query.SelectedLoader).ToString();
            if (!IsAll(query.SelectedVersion))
            {
                string version = query.SelectedVersion;
                if (version.Contains(',')) parameters["gameVersions"] = $"[{version}]";
                else if (version.Contains(' ')) parameters["gameVersions"] = $"[{version.Replace(" ", ",")}]";
                else parameters["gameVersion"] = version;
            }
            if (!string.IsNullOrEmpty(query.SearchText)) parameters["searchFilter"] = query.SearchText;
            if (categoryId >= 0) parameters["categoryId"] = categoryId.ToString();

            using HttpResponseMessage response = await SendAsync(HttpMethod.Get, "v1/mods/search", parameters, null, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return HttpFailure<ResourcePageDto<CommunityResourceDto>>(response.StatusCode);
            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            JObject root = JObject.Parse(json);
            var items = new List<CommunityResourceDto>();
            foreach (JToken item in root["data"] as JArray ?? []) items.Add(MapSearchItem(item, query.ResourceType));
            int total = root["pagination"]?["totalCount"]?.Value<int>() ?? 0;
            return ApiResult<ResourcePageDto<CommunityResourceDto>>.Ok(new(items, total, Math.Max(1, query.Page)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ApiResult<ResourcePageDto<CommunityResourceDto>>.Fail("The request was cancelled.", ResourceErrorKind.Cancelled);
        }
        catch (OperationCanceledException)
        {
            return ApiResult<ResourcePageDto<CommunityResourceDto>>.Fail("The request timed out.", ResourceErrorKind.Timeout);
        }
        catch (JsonException)
        {
            return ApiResult<ResourcePageDto<CommunityResourceDto>>.Fail("CurseForge returned invalid JSON.", ResourceErrorKind.InvalidResponse);
        }
        catch (HttpRequestException exception)
        {
            return ApiResult<ResourcePageDto<CommunityResourceDto>>.Fail(exception.Message, ResourceErrorKind.Http);
        }
        catch (Exception exception)
        {
            return ApiResult<ResourcePageDto<CommunityResourceDto>>.Fail(exception.Message);
        }
    }

    public async Task<ApiResult<int>> ResolveProjectIdByFingerprintAsync(
        uint fingerprint,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string body = JsonConvert.SerializeObject(new { fingerprints = new[] { fingerprint } });
            using HttpResponseMessage response = await SendAsync(HttpMethod.Post, "v1/fingerprints/432", null, body, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return HttpFailure<int>(response.StatusCode);
            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            int projectId = JObject.Parse(json)["data"]?["exactMatches"]?.FirstOrDefault()?["id"]?.Value<int>() ?? 0;
            return ApiResult<int>.Ok(projectId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ApiResult<int>.Fail("The request was cancelled.", ResourceErrorKind.Cancelled);
        }
        catch (OperationCanceledException)
        {
            return ApiResult<int>.Fail("The request timed out.", ResourceErrorKind.Timeout);
        }
        catch (JsonException)
        {
            return ApiResult<int>.Fail("CurseForge returned invalid JSON.", ResourceErrorKind.InvalidResponse);
        }
        catch (HttpRequestException exception)
        {
            return ApiResult<int>.Fail(exception.Message, ResourceErrorKind.Http);
        }
        catch (Exception exception)
        {
            return ApiResult<int>.Fail(exception.Message);
        }
    }

    public async Task<ApiResult<IReadOnlyDictionary<uint, LocalModEnrichmentDto>>> MatchFingerprintsAsync(
        IEnumerable<uint> fingerprints,
        CancellationToken cancellationToken = default)
    {
        uint[] values = fingerprints.Where(value => value != 0).Distinct().ToArray();
        if (values.Length == 0) return ApiResult<IReadOnlyDictionary<uint, LocalModEnrichmentDto>>.Ok(
            new Dictionary<uint, LocalModEnrichmentDto>());
        try
        {
            string body = JsonConvert.SerializeObject(new { fingerprints = values });
            using HttpResponseMessage matchResponse = await SendAsync(
                HttpMethod.Post,
                "v1/fingerprints/432",
                null,
                body,
                cancellationToken).ConfigureAwait(false);
            if (!matchResponse.IsSuccessStatusCode) return HttpFailure<IReadOnlyDictionary<uint, LocalModEnrichmentDto>>(matchResponse.StatusCode);
            JObject root = JObject.Parse(await matchResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            var matches = new Dictionary<uint, LocalModEnrichmentDto>();
            foreach (JToken match in root["data"]?["exactMatches"] as JArray ?? [])
            {
                uint fingerprint = match["file"]?["fileFingerprint"]?.Value<uint>() ?? 0;
                int projectId = match["id"]?.Value<int>() ?? 0;
                if (fingerprint != 0) matches[fingerprint] = new LocalModEnrichmentDto(
                    fingerprint.ToString(), string.Empty, projectId, string.Empty, string.Empty, null);
            }

            int[] projectIds = matches.Values.Select(value => value.CurseForgeId).Where(value => value != 0).Distinct().ToArray();
            if (projectIds.Length != 0)
            {
                string projectBody = JsonConvert.SerializeObject(new { modIds = projectIds });
                using HttpResponseMessage projectResponse = await SendAsync(
                    HttpMethod.Post,
                    "v1/mods",
                    null,
                    projectBody,
                    cancellationToken).ConfigureAwait(false);
                if (projectResponse.IsSuccessStatusCode)
                {
                    JObject projectRoot = JObject.Parse(await projectResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
                    var projectMap = (projectRoot["data"] as JArray ?? [])
                        .ToDictionary(item => item["id"]?.Value<int>() ?? 0, MapProject);
                    foreach (uint key in matches.Keys.ToList())
                    {
                        LocalModEnrichmentDto value = matches[key];
                        projectMap.TryGetValue(value.CurseForgeId, out CommunityResourceDto? project);
                        matches[key] = value with { Resource = project };
                    }
                }
            }
            return ApiResult<IReadOnlyDictionary<uint, LocalModEnrichmentDto>>.Ok(matches);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ApiResult<IReadOnlyDictionary<uint, LocalModEnrichmentDto>>.Fail("The request was cancelled.", ResourceErrorKind.Cancelled);
        }
        catch (OperationCanceledException)
        {
            return ApiResult<IReadOnlyDictionary<uint, LocalModEnrichmentDto>>.Fail("The request timed out.", ResourceErrorKind.Timeout);
        }
        catch (JsonException)
        {
            return ApiResult<IReadOnlyDictionary<uint, LocalModEnrichmentDto>>.Fail("CurseForge returned invalid JSON.", ResourceErrorKind.InvalidResponse);
        }
        catch (HttpRequestException exception)
        {
            return ApiResult<IReadOnlyDictionary<uint, LocalModEnrichmentDto>>.Fail(exception.Message, ResourceErrorKind.Http);
        }
        catch (Exception exception)
        {
            return ApiResult<IReadOnlyDictionary<uint, LocalModEnrichmentDto>>.Fail(exception.Message);
        }
    }

    public async Task<ApiResult<IReadOnlyList<ResourceFileDto>>> GetFilesAsync(
        int projectId,
        CancellationToken cancellationToken = default)
    {
        if (projectId <= 0) return ApiResult<IReadOnlyList<ResourceFileDto>>.Ok([]);
        try
        {
            var files = new List<ResourceFileDto>();
            int index = 0;
            int totalCount;
            do
            {
                var parameters = new Dictionary<string, string>
                {
                    ["index"] = index.ToString(),
                    ["pageSize"] = "100"
                };
                using HttpResponseMessage response = await SendAsync(
                    HttpMethod.Get,
                    $"v1/mods/{projectId}/files",
                    parameters,
                    null,
                    cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return HttpFailure<IReadOnlyList<ResourceFileDto>>(response.StatusCode);
                string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                JObject root = JObject.Parse(json);
                JArray data = root["data"] as JArray ?? [];
                foreach (JToken item in data) files.Add(MapFile(item));
                totalCount = root["pagination"]?["totalCount"]?.Value<int>() ?? files.Count;
                index += data.Count;
                if (data.Count == 0) break;
            } while (index < totalCount);
            return ApiResult<IReadOnlyList<ResourceFileDto>>.Ok(files);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ApiResult<IReadOnlyList<ResourceFileDto>>.Fail("The request was cancelled.", ResourceErrorKind.Cancelled);
        }
        catch (OperationCanceledException)
        {
            return ApiResult<IReadOnlyList<ResourceFileDto>>.Fail("The request timed out.", ResourceErrorKind.Timeout);
        }
        catch (JsonException)
        {
            return ApiResult<IReadOnlyList<ResourceFileDto>>.Fail("CurseForge returned invalid JSON.", ResourceErrorKind.InvalidResponse);
        }
        catch (HttpRequestException exception)
        {
            return ApiResult<IReadOnlyList<ResourceFileDto>>.Fail(exception.Message, ResourceErrorKind.Http);
        }
        catch (Exception exception)
        {
            return ApiResult<IReadOnlyList<ResourceFileDto>>.Fail(exception.Message);
        }
    }

    public static int GetClassId(ResourceType resourceType) => resourceType switch
    {
        ResourceType.ModPack => 4471,
        ResourceType.TexturePack => 12,
        ResourceType.ShaderPack => 6552,
        ResourceType.DataPack => 6945,
        _ => 6
    };

    public static int GetModLoaderId(string loader) => loader.ToLowerInvariant() switch
    {
        "forge" => 1,
        "cauldron" => 2,
        "liteloader" => 3,
        "fabric" => 4,
        "quilt" => 5,
        "neoforge" => 6,
        _ => 0
    };

    public static string GetModLoaderName(int loader) => loader switch
    {
        1 => "Forge",
        2 => "Cauldron",
        3 => "LiteLoader",
        4 => "Fabric",
        5 => "Quilt",
        6 => "NeoForge",
        _ => "unknown"
    };

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativePath,
        IReadOnlyDictionary<string, string>? parameters,
        string? body,
        CancellationToken cancellationToken)
    {
        string query = parameters == null || parameters.Count == 0
            ? string.Empty
            : "?" + string.Join("&", parameters.Select(item =>
                $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));
        using var request = new HttpRequestMessage(method, new Uri(_baseUri, relativePath + query));
        if (!string.IsNullOrWhiteSpace(_apiKey)) request.Headers.TryAddWithoutValidation("X-API-KEY", _apiKey);
        request.Headers.UserAgent.Clear();
        request.Headers.UserAgent.Add(ProductInfoHeaderValue.Parse(_userAgent));
        if (body != null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    }

    private static CommunityResourceDto MapSearchItem(JToken item, ResourceType type)
    {
        var versions = new List<string>();
        var loaders = new List<string>();
        var categories = new List<string>();
        foreach (JToken category in item["categories"] as JArray ?? [])
        {
            string display = ResourceCategory.CurseForgeCategoriesParse(category["id"]?.Value<int>() ?? 0);
            if (!string.IsNullOrEmpty(display)) categories.Add(display);
        }
        foreach (JToken index in item["latestFilesIndexes"] as JArray ?? [])
        {
            string version = index["gameVersion"]?.ToString() ?? string.Empty;
            int loader = index["modLoader"]?.Value<int>() ?? 0;
            if (!string.IsNullOrEmpty(version) && !versions.Contains(version)) versions.Add(version);
            string loaderName = GetModLoaderName(loader);
            if (loader != 0 && !loaders.Contains(loaderName)) loaders.Add(loaderName);
        }

        string slug = item["slug"]?.ToString() ?? string.Empty;
        return new CommunityResourceDto(
            OriginalName: item["name"]?.ToString() ?? string.Empty,
            Logo: item["logo"]?["thumbnailUrl"]?.ToString() ?? string.Empty,
            Slug: slug,
            DownloadCount: item["downloadCount"]?.Value<int>() ?? 0,
            Description: item["summary"]?.ToString() ?? string.Empty,
            Type: type,
            CurseForgeId: item["id"]?.Value<int>() ?? 0,
            Author: string.Join(",", item["authors"]?.Select(author => author["name"]?.ToString()) ?? []),
            WebsiteUrl: $"https://www.curseforge.com/minecraft/{GetLinkType(type)}/{slug}",
            LastUpdated: FormatDate(item["dateModified"]?.ToString()),
            Loaders: loaders,
            GameVersions: SortVersions(versions),
            Categories: categories,
            ResourceSource: "CurseForge");
    }

    private static CommunityResourceDto MapProject(JToken item)
    {
        CommunityResourceDto resource = MapSearchItem(item, ResourceType.Mod);
        return resource with
        {
            Logo = item["logo"]?["url"]?.ToString() ?? resource.Logo,
            WebsiteUrl = item["links"]?["websiteUrl"]?.ToString() ?? resource.WebsiteUrl,
            Author = item["authors"]?.FirstOrDefault()?["name"]?.ToString() ?? resource.Author
        };
    }

    private static ResourceFileDto MapFile(JToken item)
    {
        List<string> gameVersions = item["gameVersions"]?.ToObject<List<string>>() ?? [];
        List<string> versions = gameVersions.Where(value => Regex.IsMatch(value, @"\d")).ToList();
        List<string> loaders = gameVersions.Where(value => LoaderNames.Contains(value)).ToList();
        string fileName = item["fileName"]?.ToString() ?? string.Empty;
        JToken? sha1 = (item["hashes"] as JArray)?.FirstOrDefault(hash => hash["algo"]?.Value<int>() == 1)
                       ?? item["hashes"]?.ElementAtOrDefault(1);
        return new ResourceFileDto(
            Path.GetFileNameWithoutExtension(fileName),
            string.Empty,
            versions,
            loaders,
            FormatDate(item["fileDate"]?.ToString()),
            fileName,
            sha1?["value"]?.ToString() ?? string.Empty,
            item["downloadUrl"]?.ToString() ?? string.Empty,
            item["fileLength"]?.Value<long>() ?? 1);
    }

    private static string GetLinkType(ResourceType type) => type switch
    {
        ResourceType.ModPack => "modpacks",
        ResourceType.TexturePack => "texture-packs",
        ResourceType.ShaderPack => "shaders",
        ResourceType.DataPack => "data-packs",
        _ => "mc-mods"
    };

    private static bool IsAll(string? value) => string.IsNullOrEmpty(value) || value == "全部";

    private static List<string> SortVersions(IEnumerable<string> versions) => versions
        .Select(value => (Value: value, Version: ParseVersion(value)))
        .OrderBy(item => item.Version)
        .Select(item => item.Value)
        .ToList();

    private static Version ParseVersion(string value)
    {
        string[] parts = value.Split('.');
        int[] values = parts.Take(3).Select(part => int.TryParse(part, out int parsed) ? parsed : 0).ToArray();
        return new Version(values.ElementAtOrDefault(0), values.ElementAtOrDefault(1), values.ElementAtOrDefault(2));
    }

    private static string FormatDate(string? value) =>
        DateTime.TryParse(value, out DateTime date) ? date.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty;

    private static ApiResult<T> HttpFailure<T>(HttpStatusCode statusCode) => ApiResult<T>.Fail(
        statusCode == HttpStatusCode.TooManyRequests ? "CurseForge rate limit exceeded." : $"CurseForge HTTP {(int)statusCode} ({statusCode}).",
        statusCode == HttpStatusCode.TooManyRequests ? ResourceErrorKind.RateLimited : ResourceErrorKind.Http,
        (int)statusCode);
}
