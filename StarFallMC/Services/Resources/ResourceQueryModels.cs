using StarFallMC.Entity.Enum;
using fNbt;

namespace StarFallMC.Services.Resources;

public sealed record ResourceQuery(
    ResourceType ResourceType,
    bool UseCurseForge,
    int Page = 1,
    string SearchText = "",
    string SelectedLoader = "全部",
    string SelectedVersion = "全部",
    string SelectedCategory = "全部",
    string GameVersion = "",
    string DirectoryVersion = "",
    string ProtocolVersion = "resource-v1")
{
    public int Offset(int pageSize) => Math.Max(0, Page - 1) * Math.Max(1, pageSize);
}

public sealed record ResourceCacheKey(
    string Source,
    ResourceType ResourceType,
    string SearchText,
    string Loader,
    string Version,
    string Category,
    int Page,
    string GameVersion,
    string DirectoryVersion,
    string ProtocolVersion = "resource-v1")
{
    public static ResourceCacheKey FromQuery(ResourceQuery query) => new(
        query.UseCurseForge ? "curseforge" : "modrinth",
        query.ResourceType,
        query.SearchText ?? string.Empty,
        query.SelectedLoader ?? "全部",
        query.SelectedVersion ?? "全部",
        query.SelectedCategory ?? "全部",
        Math.Max(1, query.Page),
        query.GameVersion ?? string.Empty,
        query.DirectoryVersion ?? string.Empty,
        query.ProtocolVersion);
}

public sealed record ResourcePageDto<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    bool FromCache = false);

public sealed record ResourceError(
    string Path,
    string Message,
    Exception? Exception = null);

public sealed record ResourceScanResult<T>(
    IReadOnlyList<T> Items,
    IReadOnlyList<ResourceError> Errors)
{
    public bool HasErrors => Errors.Count != 0;
}

public sealed record LocalModResourceDto(
    string FilePath,
    string ResourceVersion,
    string OriginalName,
    string Author,
    string ModrinthSha1,
    uint CurseForgeSha1,
    bool Disabled);

public sealed record LocalSaveResourceDto(
    string DirName,
    string Path,
    string IconPath,
    string RefreshDate,
    NbtCompound? Nbt);

public sealed record LocalTexturePackResourceDto(
    string Name,
    string Description,
    string Path,
    string IconPath,
    string IconArchiveEntryName);

public sealed record CommunityResourceDto(
    string OriginalName = "",
    string Logo = "",
    string Slug = "",
    int DownloadCount = 0,
    int FollowsCount = 0,
    string Description = "",
    ResourceType Type = ResourceType.Mod,
    string ModrinthProjectId = "",
    string ModrinthAuthorId = "",
    int CurseForgeId = 0,
    string Author = "",
    string WebsiteUrl = "",
    string LastUpdated = "",
    IReadOnlyList<string>? Loaders = null,
    IReadOnlyList<string>? GameVersions = null,
    IReadOnlyList<string>? Categories = null,
    string ResourceSource = "",
    string ChineseName = "",
    string EnglishName = "");

public sealed record ResourceFileDto(
    string Name,
    string Version,
    IReadOnlyList<string> GameVersions,
    IReadOnlyList<string> Loaders,
    string Date,
    string FileName,
    string Sha1,
    string Url,
    long Size);

public sealed record LocalModEnrichmentDto(
    string MatchKey,
    string ProjectId,
    int CurseForgeId,
    string AuthorId,
    string Version,
    CommunityResourceDto? Resource);

public sealed record ApiResult<T>(
    bool Success,
    T? Value,
    int? StatusCode = null,
    string? Error = null,
    ResourceErrorKind ErrorKind = ResourceErrorKind.None)
{
    public static ApiResult<T> Ok(T value) => new(true, value);
    public static ApiResult<T> Fail(string error, ResourceErrorKind kind = ResourceErrorKind.Unknown, int? statusCode = null) =>
        new(false, default, statusCode, error, kind);
}

public enum ResourceErrorKind
{
    None,
    Cancelled,
    Timeout,
    Http,
    RateLimited,
    InvalidResponse,
    Unknown
}

public sealed class ResourceApiException : Exception
{
    public ResourceApiException(string message, ResourceErrorKind errorKind, int? statusCode = null)
        : base(message)
    {
        ErrorKind = errorKind;
        StatusCode = statusCode;
    }

    public ResourceErrorKind ErrorKind { get; }
    public int? StatusCode { get; }
}
