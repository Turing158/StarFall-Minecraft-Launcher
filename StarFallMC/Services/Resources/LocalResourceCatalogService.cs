using StarFallMC.Entity.Resource;

namespace StarFallMC.Services.Resources;

public sealed class LocalResourceCatalogService
{
    private readonly ResourceServiceContainer _services;

    public LocalResourceCatalogService(ResourceServiceContainer services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public async Task<ResourceScanResult<MinecraftResource>> ScanModsAsync(
        string versionPath,
        bool isIsolation,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var rawProgress = new Progress<int>(value => progress?.Report(5 + value * 29 / 100));
        ResourceScanResult<LocalModResourceDto> scanned = await _services.Scanner.ScanModsAsync(
            versionPath, isIsolation, rawProgress, cancellationToken).ConfigureAwait(false);
        var resources = scanned.Items.Select(_services.Mapper.Map).ToList();
        var errors = scanned.Errors.ToList();
        progress?.Report(35);
        if (resources.Count == 0)
        {
            progress?.Report(100);
            return new(resources, errors);
        }

        ApiResult<IReadOnlyDictionary<string, LocalModEnrichmentDto>> modrinth =
            await _services.Modrinth.MatchFilesAsync(resources.Select(item => item.ModrinthSha1), cancellationToken).ConfigureAwait(false);
        if (modrinth.ErrorKind == ResourceErrorKind.Cancelled) throw new OperationCanceledException(cancellationToken);
        var unresolved = new List<MinecraftResource>();
        foreach (MinecraftResource resource in resources)
        {
            if (modrinth.Success && modrinth.Value != null && modrinth.Value.TryGetValue(resource.ModrinthSha1, out LocalModEnrichmentDto? match))
            {
                ApplyEnrichment(resource, match);
            }
            if (IsIncomplete(resource)) unresolved.Add(resource);
        }
        var complete = resources.Except(unresolved).ToList();
        progress?.Report(70);

        if (unresolved.Count != 0)
        {
            ApiResult<IReadOnlyDictionary<uint, LocalModEnrichmentDto>> curseForge =
                await _services.CurseForge.MatchFingerprintsAsync(unresolved.Select(item => item.CurseForgeSha1), cancellationToken).ConfigureAwait(false);
            if (curseForge.ErrorKind == ResourceErrorKind.Cancelled) throw new OperationCanceledException(cancellationToken);
            foreach (MinecraftResource resource in unresolved)
            {
                if (curseForge.Success && curseForge.Value != null && curseForge.Value.TryGetValue(resource.CurseForgeSha1, out LocalModEnrichmentDto? match))
                {
                    ApplyEnrichment(resource, match);
                }
            }
        }
        progress?.Report(95);
        var orderedResources = complete.Concat(unresolved).ToList();
        foreach (MinecraftResource resource in orderedResources)
        {
            if (string.IsNullOrEmpty(resource.OriginalName)) resource.OriginalName = resource.FileNameWithExtension;
            ApplyLocalization(resource);
        }
        progress?.Report(100);
        return new(orderedResources, errors);
    }

    public async Task<ResourceScanResult<SavesResource>> ScanSavesAsync(
        string versionPath,
        bool isIsolation,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ResourceScanResult<LocalSaveResourceDto> scanned = await _services.Scanner.ScanSavesAsync(
            versionPath, isIsolation, progress, cancellationToken).ConfigureAwait(false);
        return new(scanned.Items.Select(_services.Mapper.Map).ToList(), scanned.Errors);
    }

    public async Task<ResourceScanResult<TexturePackResource>> ScanTexturePacksAsync(
        string versionPath,
        bool isIsolation,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ResourceScanResult<LocalTexturePackResourceDto> scanned = await _services.Scanner.ScanTexturePacksAsync(
            versionPath, isIsolation, progress, cancellationToken).ConfigureAwait(false);
        return new(scanned.Items.Select(_services.Mapper.Map).ToList(), scanned.Errors);
    }

    private void ApplyEnrichment(MinecraftResource target, LocalModEnrichmentDto enrichment)
    {
        if (!string.IsNullOrEmpty(enrichment.ProjectId)) target.ModrinthProjectId = enrichment.ProjectId;
        if (!string.IsNullOrEmpty(enrichment.AuthorId)) target.ModrinthAuthorId = enrichment.AuthorId;
        if (!string.IsNullOrEmpty(enrichment.Version)) target.ResourceVersion = enrichment.Version;
        if (enrichment.CurseForgeId != 0) target.CurseForgeId = enrichment.CurseForgeId;
        if (enrichment.Resource == null) return;

        MinecraftResource mapped = _services.Mapper.Map(enrichment.Resource, _services.Localizations);
        target.OriginalName = mapped.OriginalName;
        target.Logo = mapped.Logo;
        target.Slug = mapped.Slug;
        target.DownloadCount = mapped.DownloadCount;
        target.FollowsCount = mapped.FollowsCount;
        target.Description = mapped.Description;
        target.Author = mapped.Author;
        target.WebsiteUrl = mapped.WebsiteUrl;
        target.LastUpdated = mapped.LastUpdated;
        target.Loaders = mapped.Loaders;
        target.GameVersions = mapped.GameVersions;
        target.Categories = mapped.Categories;
        target.ResourceSource = mapped.ResourceSource;
        target.ChineseName = mapped.ChineseName;
        target.EnglishName = mapped.EnglishName;
    }

    private void ApplyLocalization(MinecraftResource resource)
    {
        if (string.IsNullOrEmpty(resource.Slug)) return;
        CommunityResourceDto dto = new(
            resource.OriginalName,
            resource.Logo,
            resource.Slug,
            resource.DownloadCount,
            resource.FollowsCount,
            resource.Description,
            resource.Type,
            resource.ModrinthProjectId,
            resource.ModrinthAuthorId,
            resource.CurseForgeId,
            resource.Author,
            resource.WebsiteUrl,
            resource.LastUpdated,
            resource.Loaders,
            resource.GameVersions,
            resource.Categories,
            resource.ResourceSource,
            resource.ChineseName,
            resource.EnglishName);
        MinecraftResource localized = _services.Mapper.Map(dto, _services.Localizations);
        resource.ChineseName = localized.ChineseName;
        resource.EnglishName = localized.EnglishName;
    }

    private static bool IsIncomplete(MinecraftResource resource) =>
        string.IsNullOrEmpty(resource.ResourceSource) ||
        string.IsNullOrEmpty(resource.WebsiteUrl) ||
        string.IsNullOrEmpty(resource.Description);
}
