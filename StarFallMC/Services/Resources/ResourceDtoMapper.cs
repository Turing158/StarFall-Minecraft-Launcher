using StarFallMC.Entity;
using StarFallMC.Entity.Resource;

namespace StarFallMC.Services.Resources;

/// <summary>
/// Converts transport/local DTOs into the existing display entities. The mapper
/// does not access a Page, Dispatcher, or static UI collection.
/// </summary>
public sealed class ResourceDtoMapper
{
    public MinecraftResource Map(LocalModResourceDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MinecraftResource
        {
            FilePath = dto.FilePath,
            ResourceVersion = dto.ResourceVersion,
            OriginalName = dto.OriginalName,
            Author = dto.Author,
            ModrinthSha1 = dto.ModrinthSha1,
            CurseForgeSha1 = dto.CurseForgeSha1,
            Disabled = dto.Disabled
        };
    }

    public SavesResource Map(LocalSaveResourceDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new SavesResource(dto.Nbt ?? new fNbt.NbtCompound(), dto.DirName, dto.Path, dto.RefreshDate)
        {
            IconPath = dto.IconPath
        };
    }

    public TexturePackResource Map(LocalTexturePackResourceDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new TexturePackResource
        {
            Name = dto.Name,
            Description = dto.Description,
            Path = dto.Path,
            IconPath = dto.IconPath,
            IconArchiveEntryName = dto.IconArchiveEntryName
        };
    }

    public MinecraftResource Map(CommunityResourceDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MinecraftResource
        {
            OriginalName = dto.OriginalName,
            Logo = dto.Logo,
            Slug = dto.Slug,
            DownloadCount = dto.DownloadCount,
            FollowsCount = dto.FollowsCount,
            Description = dto.Description,
            Type = dto.Type,
            ModrinthProjectId = dto.ModrinthProjectId,
            ModrinthAuthorId = dto.ModrinthAuthorId,
            CurseForgeId = dto.CurseForgeId,
            Author = dto.Author,
            WebsiteUrl = dto.WebsiteUrl,
            LastUpdated = dto.LastUpdated,
            Loaders = dto.Loaders?.ToList() ?? [],
            GameVersions = dto.GameVersions?.ToList() ?? [],
            Categories = dto.Categories?.ToList() ?? [],
            ResourceSource = dto.ResourceSource,
            ChineseName = dto.ChineseName,
            EnglishName = dto.EnglishName
        };
    }

    public MinecraftResource Map(CommunityResourceDto dto, IEnumerable<McModData> localizations)
    {
        MinecraftResource resource = Map(dto);
        if (dto.Type != Entity.Enum.ResourceType.Mod) return resource;
        McModData? match = localizations.FirstOrDefault(item =>
            string.Equals(item.ModrinthSlug, dto.Slug, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.CurseForgeSlug, dto.Slug, StringComparison.OrdinalIgnoreCase));
        if (match == null) return resource;
        try
        {
            if (match.ChineseName.Length > 0) resource.ChineseName = match.ChineseName[^1];
            if (match.EnglishName.Length > 0) resource.EnglishName = match.EnglishName[^1];
        }
        catch (IndexOutOfRangeException)
        {
            // Malformed optional localization data does not invalidate the API item.
        }
        return resource;
    }

    public ModDownloader Map(ResourceFileDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var file = new DownloadFile
        {
            Name = dto.FileName,
            Sha1 = dto.Sha1,
            UrlPath = dto.Url,
            UrlPaths = string.IsNullOrWhiteSpace(dto.Url) ? [] : [dto.Url],
            Size = dto.Size
        };
        var downloader = new ModDownloader
        {
            Name = dto.Name,
            Version = dto.Version,
            Date = dto.Date,
            File = file
        };
        downloader.McVersion.AddRange(dto.GameVersions);
        downloader.ModLoader.AddRange(dto.Loaders);
        return downloader;
    }

    public IReadOnlyList<MinecraftResource> Map(IEnumerable<CommunityResourceDto> resources) =>
        resources.Select(Map).ToList();
}
