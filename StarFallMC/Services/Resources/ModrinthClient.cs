using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StarFallMC.Entity.Enum;
using StarFallMC.Util;

namespace StarFallMC.Services.Resources;

public sealed class ModrinthClient
{
    public const string DefaultApi = "https://api.modrinth.com";
    private static readonly HashSet<string> LoaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Forge", "NeoForge", "Fabric", "Quilt", "Liteloader"
    };

    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;
    private readonly string _userAgent;

    public ModrinthClient(HttpClient httpClient, Uri? baseUri = null, string userAgent = "StarFallMC")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUri = baseUri ?? new Uri(DefaultApi + "/", UriKind.Absolute);
        _userAgent = string.IsNullOrWhiteSpace(userAgent) ? "StarFallMC" : userAgent;
    }

    public async Task<ApiResult<ResourcePageDto<CommunityResourceDto>>> SearchAsync(
        ResourceQuery query,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var facets = new JArray();
            if (!IsAll(query.SelectedVersion)) facets.Add(new JArray($"versions:{query.SelectedVersion}"));
            var categories = new JArray();
            if (!IsAll(query.SelectedLoader)) categories.Add($"categories:{query.SelectedLoader}");
            if (!IsAll(query.SelectedCategory)) categories.Add($"categories:{query.SelectedCategory}");
            if (categories.Count != 0) facets.Add(categories);
            facets.Add(new JArray($"project_type:{GetProjectType(query.ResourceType)}"));

            var parameters = new Dictionary<string, string>
            {
                ["limit"] = Math.Max(1, pageSize).ToString(),
                ["offset"] = query.Offset(pageSize).ToString(),
                ["facets"] = facets.ToString(Formatting.None)
            };
            if (!string.IsNullOrEmpty(query.SearchText)) parameters["query"] = query.SearchText;

            using HttpResponseMessage response = await SendGetAsync("v2/search", parameters, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return HttpFailure<ResourcePageDto<CommunityResourceDto>>(response.StatusCode);
            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            JObject root = JObject.Parse(json);
            var items = new List<CommunityResourceDto>();
            foreach (JToken hit in root["hits"] as JArray ?? []) items.Add(MapSearchHit(hit, query.ResourceType));
            int total = root["total_hits"]?.Value<int>() ?? 0;
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
            return ApiResult<ResourcePageDto<CommunityResourceDto>>.Fail("Modrinth returned invalid JSON.", ResourceErrorKind.InvalidResponse);
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

    public async Task<ApiResult<IReadOnlyList<ResourceFileDto>>> GetFilesAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectId)) return ApiResult<IReadOnlyList<ResourceFileDto>>.Ok([]);
        try
        {
            using HttpResponseMessage response = await SendGetAsync(
                $"v2/project/{Uri.EscapeDataString(projectId)}/version",
                null,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return HttpFailure<IReadOnlyList<ResourceFileDto>>(response.StatusCode);
            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var result = new List<ResourceFileDto>();
            foreach (JToken version in JArray.Parse(json))
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<string> gameVersions = version["game_versions"]?.ToObject<List<string>>() ?? [];
                IReadOnlyList<string> loaders = version["loaders"]?.ToObject<List<string>>() ?? [];
                string date = FormatDate(version["date_published"]?.ToString());
                foreach (JToken file in version["files"] as JArray ?? [])
                {
                    string fileName = file["filename"]?.ToString() ?? string.Empty;
                    result.Add(new ResourceFileDto(
                        Path.GetFileNameWithoutExtension(fileName),
                        version["version_number"]?.ToString() ?? string.Empty,
                        gameVersions,
                        loaders,
                        date,
                        fileName,
                        file["hashes"]?["sha1"]?.ToString() ?? file["sha1"]?.ToString() ?? string.Empty,
                        file["url"]?.ToString() ?? string.Empty,
                        file["size"]?.Value<long>() ?? 1));
                }
            }
            return ApiResult<IReadOnlyList<ResourceFileDto>>.Ok(result);
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
            return ApiResult<IReadOnlyList<ResourceFileDto>>.Fail("Modrinth returned invalid JSON.", ResourceErrorKind.InvalidResponse);
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

    public async Task<ApiResult<IReadOnlyDictionary<string, LocalModEnrichmentDto>>> MatchFilesAsync(
        IEnumerable<string> sha1Hashes,
        CancellationToken cancellationToken = default)
    {
        string[] hashes = sha1Hashes.Where(hash => !string.IsNullOrWhiteSpace(hash)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (hashes.Length == 0) return ApiResult<IReadOnlyDictionary<string, LocalModEnrichmentDto>>.Ok(
            new Dictionary<string, LocalModEnrichmentDto>());
        try
        {
            string body = JsonConvert.SerializeObject(new { hashes, algorithm = "sha1" });
            using var request = CreateRequest(HttpMethod.Post, "v2/version_files", null);
            request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return HttpFailure<IReadOnlyDictionary<string, LocalModEnrichmentDto>>(response.StatusCode);
            JObject matches = JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            var values = new Dictionary<string, LocalModEnrichmentDto>(StringComparer.OrdinalIgnoreCase);
            foreach (JProperty property in matches.Properties())
            {
                values[property.Name] = new LocalModEnrichmentDto(
                    property.Name,
                    property.Value["project_id"]?.ToString() ?? string.Empty,
                    0,
                    property.Value["author_id"]?.ToString() ?? string.Empty,
                    property.Value["version_number"]?.ToString() ?? string.Empty,
                    null);
            }

            string[] projectIds = values.Values.Select(value => value.ProjectId).Where(value => value.Length != 0).Distinct().ToArray();
            var projectMap = new Dictionary<string, CommunityResourceDto>(StringComparer.OrdinalIgnoreCase);
            if (projectIds.Length != 0)
            {
                using HttpResponseMessage projectResponse = await SendGetAsync(
                    "v2/projects",
                    new Dictionary<string, string> { ["ids"] = JsonConvert.SerializeObject(projectIds) },
                    cancellationToken).ConfigureAwait(false);
                if (projectResponse.IsSuccessStatusCode)
                {
                    foreach (JToken project in JArray.Parse(await projectResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)))
                    {
                        CommunityResourceDto dto = MapProject(project);
                        projectMap[project["id"]?.ToString() ?? string.Empty] = dto;
                    }
                }
            }

            string[] authorIds = values.Values.Select(value => value.AuthorId).Where(value => value.Length != 0).Distinct().ToArray();
            var authors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (authorIds.Length != 0)
            {
                using HttpResponseMessage authorResponse = await SendGetAsync(
                    "v2/users",
                    new Dictionary<string, string> { ["ids"] = JsonConvert.SerializeObject(authorIds) },
                    cancellationToken).ConfigureAwait(false);
                if (authorResponse.IsSuccessStatusCode)
                {
                    foreach (JToken author in JArray.Parse(await authorResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)))
                    {
                        authors[author["id"]?.ToString() ?? string.Empty] = author["username"]?.ToString() ?? string.Empty;
                    }
                }
            }

            foreach (string key in values.Keys.ToList())
            {
                LocalModEnrichmentDto value = values[key];
                projectMap.TryGetValue(value.ProjectId, out CommunityResourceDto? project);
                if (project != null && authors.TryGetValue(value.AuthorId, out string? author)) project = project with { Author = author };
                values[key] = value with { Resource = project };
            }
            return ApiResult<IReadOnlyDictionary<string, LocalModEnrichmentDto>>.Ok(values);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ApiResult<IReadOnlyDictionary<string, LocalModEnrichmentDto>>.Fail("The request was cancelled.", ResourceErrorKind.Cancelled);
        }
        catch (OperationCanceledException)
        {
            return ApiResult<IReadOnlyDictionary<string, LocalModEnrichmentDto>>.Fail("The request timed out.", ResourceErrorKind.Timeout);
        }
        catch (JsonException)
        {
            return ApiResult<IReadOnlyDictionary<string, LocalModEnrichmentDto>>.Fail("Modrinth returned invalid JSON.", ResourceErrorKind.InvalidResponse);
        }
        catch (HttpRequestException exception)
        {
            return ApiResult<IReadOnlyDictionary<string, LocalModEnrichmentDto>>.Fail(exception.Message, ResourceErrorKind.Http);
        }
        catch (Exception exception)
        {
            return ApiResult<IReadOnlyDictionary<string, LocalModEnrichmentDto>>.Fail(exception.Message);
        }
    }

    private async Task<HttpResponseMessage> SendGetAsync(
        string relativePath,
        IReadOnlyDictionary<string, string>? parameters,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, relativePath, parameters);
        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string relativePath,
        IReadOnlyDictionary<string, string>? parameters)
    {
        string query = parameters == null || parameters.Count == 0
            ? string.Empty
            : "?" + string.Join("&", parameters.Select(item =>
                $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));
        var request = new HttpRequestMessage(method, new Uri(_baseUri, relativePath + query));
        request.Headers.UserAgent.Clear();
        request.Headers.UserAgent.Add(ProductInfoHeaderValue.Parse(_userAgent));
        return request;
    }

    private static CommunityResourceDto MapSearchHit(JToken hit, ResourceType type)
    {
        var loaders = new List<string>();
        var categories = new List<string>();
        foreach (JToken categoryToken in hit["categories"] as JArray ?? [])
        {
            string value = categoryToken.ToString();
            if (LoaderNames.Contains(value)) loaders.Add(value);
            else
            {
                string display = ResourceCategory.ModrinthCategoriesParse(value);
                if (!string.IsNullOrEmpty(display)) categories.Add(display);
            }
        }

        List<string> versions = hit["versions"]?.ToObject<List<string>>() ?? [];
        versions = SortVersions(versions.Where(IsReleaseVersion));
        string projectType = hit["project_type"]?.ToString() ?? GetProjectType(type);
        string slug = hit["slug"]?.ToString() ?? string.Empty;
        return new CommunityResourceDto(
            OriginalName: hit["title"]?.ToString() ?? string.Empty,
            Logo: hit["icon_url"]?.ToString() ?? string.Empty,
            Slug: slug,
            DownloadCount: hit["downloads"]?.Value<int>() ?? 0,
            FollowsCount: hit["follows"]?.Value<int>() ?? 0,
            Description: hit["description"]?.ToString() ?? string.Empty,
            Type: type,
            ModrinthProjectId: hit["project_id"]?.ToString() ?? string.Empty,
            Author: hit["author"]?.ToString() ?? string.Empty,
            WebsiteUrl: $"https://modrinth.com/{projectType}/{slug}",
            LastUpdated: FormatDate(hit["date_modified"]?.ToString()),
            Loaders: loaders,
            GameVersions: versions,
            Categories: categories,
            ResourceSource: "Modrinth");
    }

    private static CommunityResourceDto MapProject(JToken project)
    {
        string slug = project["slug"]?.ToString() ?? string.Empty;
        string projectType = project["project_type"]?.ToString() ?? "mod";
        var categories = new List<string>();
        foreach (string value in project["categories"]?.ToObject<List<string>>() ?? [])
        {
            string display = ResourceCategory.ModrinthCategoriesParse(value);
            if (!string.IsNullOrEmpty(display)) categories.Add(display);
        }
        return new CommunityResourceDto(
            OriginalName: project["title"]?.ToString() ?? string.Empty,
            Logo: project["icon_url"]?.ToString() ?? string.Empty,
            Slug: slug,
            DownloadCount: project["downloads"]?.Value<int>() ?? 0,
            Description: (project["description"]?.ToString() ?? string.Empty).Trim().Replace("\n", " "),
            Type: ResourceType.Mod,
            ModrinthProjectId: project["id"]?.ToString() ?? string.Empty,
            WebsiteUrl: $"https://modrinth.com/{projectType}/{slug}",
            LastUpdated: FormatDate(project["updated"]?.ToString()),
            Loaders: project["loaders"]?.ToObject<List<string>>() ?? [],
            GameVersions: project["game_versions"]?.ToObject<List<string>>() ?? [],
            Categories: categories,
            ResourceSource: "Modrinth");
    }

    private static string GetProjectType(ResourceType resourceType) => resourceType switch
    {
        ResourceType.ModPack => "modpack",
        ResourceType.TexturePack => "resourcepack",
        ResourceType.ShaderPack => "shader",
        ResourceType.DataPack => "datapack",
        _ => "mod"
    };

    private static bool IsAll(string? value) => string.IsNullOrEmpty(value) || value == "全部";
    private static bool IsReleaseVersion(string version) => version.Contains('.') && !version.Contains('-') && !version.Contains('b') && !version.Contains('a');

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
        statusCode == HttpStatusCode.TooManyRequests ? "Modrinth rate limit exceeded." : $"Modrinth HTTP {(int)statusCode} ({statusCode}).",
        statusCode == HttpStatusCode.TooManyRequests ? ResourceErrorKind.RateLimited : ResourceErrorKind.Http,
        (int)statusCode);
}
