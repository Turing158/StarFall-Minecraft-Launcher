using System.IO.Compression;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using fNbt;

namespace StarFallMC.Services.Resources;

/// <summary>
/// Scans one explicit game directory. A damaged archive/world is reported as a
/// file-level error so the remaining resources can still be displayed.
/// </summary>
public sealed class LocalResourceScanner
{
    private readonly ResourceFingerprintService _fingerprints;

    public LocalResourceScanner(
        ResourceFingerprintService? fingerprints = null,
        long maximumMetadataBytes = 4 * 1024 * 1024,
        int maximumNbtDepth = 64)
    {
        _fingerprints = fingerprints ?? new ResourceFingerprintService();
        MaximumMetadataBytes = Math.Max(1024, maximumMetadataBytes);
        MaximumNbtDepth = Math.Max(8, maximumNbtDepth);
    }

    public long MaximumMetadataBytes { get; }
    public int MaximumNbtDepth { get; }

    public async Task<ResourceScanResult<LocalModResourceDto>> ScanModsAsync(
        string versionPath,
        bool isIsolation,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string modsPath = ResolveResourcePath(versionPath, isIsolation, "mods");
        if (!Directory.Exists(modsPath))
        {
            progress?.Report(100);
            return new([], []);
        }

        string[] files = Directory.EnumerateFiles(modsPath, "*.jar", SearchOption.AllDirectories)
            .Where(path => string.Equals(Path.GetExtension(path), ".jar", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var resources = new List<LocalModResourceDto>(files.Length);
        var errors = new List<ResourceError>();
        progress?.Report(0);
        for (int index = 0; index < files.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = files[index];
            try
            {
                resources.Add(await ReadModAsync(path, cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                errors.Add(new ResourceError(path, exception.Message, exception));
            }
            progress?.Report(files.Length == 0 ? 100 : (index + 1) * 100 / files.Length);
        }
        progress?.Report(100);
        return new(resources, errors);
    }

    public async Task<ResourceScanResult<LocalSaveResourceDto>> ScanSavesAsync(
        string versionPath,
        bool isIsolation,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string savesPath = ResolveResourcePath(versionPath, isIsolation, "saves");
        if (!Directory.Exists(savesPath))
        {
            progress?.Report(100);
            return new([], []);
        }

        string[] directories = Directory.EnumerateDirectories(savesPath)
            .ToArray();
        var resources = new List<LocalSaveResourceDto>(directories.Length);
        var errors = new List<ResourceError>();
        progress?.Report(0);
        for (int index = 0; index < directories.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directory = directories[index];
            string levelDatPath = Path.Combine(directory, "level.dat");
            try
            {
                if (File.Exists(levelDatPath))
                {
                    if (new FileInfo(levelDatPath).Length > MaximumMetadataBytes)
                    {
                        throw new InvalidDataException("World metadata file is too large.");
                    }
                    NbtFile nbt = await Task.Run(() =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var value = new NbtFile();
                        value.LoadFromFile(
                            levelDatPath,
                            NbtCompression.AutoDetect,
                            tag => ValidateNbtTag(tag, cancellationToken));
                        return value;
                    }, cancellationToken).ConfigureAwait(false);
                    NbtCompound data = nbt.RootTag.Get<NbtCompound>("Data") ?? new NbtCompound();
                    string iconPath = Path.Combine(directory, "icon.png");
                    resources.Add(new LocalSaveResourceDto(
                        Path.GetFileName(directory),
                        directory,
                        File.Exists(iconPath) ? iconPath : string.Empty,
                        Directory.GetLastWriteTime(directory).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                        data));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                errors.Add(new ResourceError(directory, exception.Message, exception));
            }
            progress?.Report(directories.Length == 0 ? 100 : (index + 1) * 100 / directories.Length);
        }
        progress?.Report(100);
        return new(resources, errors);
    }

    public async Task<ResourceScanResult<LocalTexturePackResourceDto>> ScanTexturePacksAsync(
        string versionPath,
        bool isIsolation,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string resourcePath = ResolveResourcePath(versionPath, isIsolation, "resourcepacks");
        if (!Directory.Exists(resourcePath))
        {
            progress?.Report(100);
            return new([], []);
        }

        string[] files = Directory.EnumerateFiles(resourcePath, "*.zip", SearchOption.TopDirectoryOnly)
            .Where(path => string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var resources = new List<LocalTexturePackResourceDto>(files.Length);
        var errors = new List<ResourceError>();
        progress?.Report(0);
        for (int index = 0; index < files.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = files[index];
            try
            {
                resources.Add(await ReadTexturePackAsync(path, cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                errors.Add(new ResourceError(path, exception.Message, exception));
            }
            progress?.Report(files.Length == 0 ? 100 : (index + 1) * 100 / files.Length);
        }
        progress?.Report(100);
        return new(resources, errors);
    }

    public static string ResolveGameRoot(string versionPath, bool isIsolation)
    {
        ArgumentException.ThrowIfNullOrEmpty(versionPath);
        if (isIsolation) return versionPath;
        string? parent = Directory.GetParent(versionPath)?.Parent?.FullName;
        return string.IsNullOrEmpty(parent) ? versionPath : parent;
    }

    public static string ResolveResourcePath(string versionPath, bool isIsolation, string resourceDirectory) =>
        Path.Combine(ResolveGameRoot(versionPath, isIsolation), resourceDirectory);

    private async Task<LocalModResourceDto> ReadModAsync(string path, CancellationToken cancellationToken)
    {
        string sha1 = await _fingerprints.ComputeSha1Async(path, cancellationToken).ConfigureAwait(false);
        uint murmur = await _fingerprints.ComputeMurmurHash2Async(path, cancellationToken).ConfigureAwait(false);
        string version = string.Empty;
        string name = string.Empty;
        string author = string.Empty;
        await Task.Run(() =>
        {
            using ZipArchive archive = ZipFile.OpenRead(path);
            ZipArchiveEntry? modsToml = archive.GetEntry("META-INF/mods.toml");
            if (modsToml != null)
            {
                string content = ReadEntryText(modsToml);
                Dictionary<string, string> values = ParseModsToml(content);
                if (values.TryGetValue("version", out string? parsedVersion)) version = parsedVersion;
                if (values.TryGetValue("displayName", out string? parsedName)) name = parsedName;
                if (values.TryGetValue("authors", out string? parsedAuthor)) author = parsedAuthor;
            }
            if (string.IsNullOrEmpty(version) || version == "${file.jarVersion}")
            {
                ZipArchiveEntry? manifestEntry = archive.GetEntry("META-INF/MANIFEST.MF");
                if (manifestEntry != null)
                {
                    string? line = ReadEntryText(manifestEntry).Split('\n')
                        .FirstOrDefault(item => item.StartsWith("Implementation-Version:", StringComparison.Ordinal));
                    if (!string.IsNullOrEmpty(line)) version = line.Split(':', 2).ElementAtOrDefault(1)?.Trim() ?? string.Empty;
                }
            }
        }, cancellationToken).ConfigureAwait(false);

        bool disabled = string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), ".disabled", StringComparison.Ordinal);
        return new LocalModResourceDto(path, version ?? string.Empty, name ?? string.Empty, author ?? string.Empty, sha1, murmur, disabled);
    }

    private async Task<LocalTexturePackResourceDto> ReadTexturePackAsync(string path, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using ZipArchive archive = ZipFile.OpenRead(path);
            string iconEntryName = archive.GetEntry("pack.png")?.FullName ?? string.Empty;
            string description = string.Empty;
            ZipArchiveEntry? meta = archive.GetEntry("pack.mcmeta");
            if (meta != null)
            {
                JObject root = JObject.Parse(ReadEntryText(meta));
                description = root["pack"]?["description"]?.ToString().Replace("\n", " ") ?? string.Empty;
            }
            return new LocalTexturePackResourceDto(Path.GetFileNameWithoutExtension(path), description, path, string.Empty, iconEntryName);
        }, cancellationToken).ConfigureAwait(false);
    }

    private string ReadEntryText(ZipArchiveEntry entry)
    {
        if (entry.Length > MaximumMetadataBytes) throw new InvalidDataException("Resource metadata entry is too large.");
        using Stream stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        return reader.ReadToEnd();
    }

    private static Dictionary<string, string> ParseModsToml(string content)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        bool inModsSection = false;
        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.Split('#', 2)[0].Trim();
            if (line == "[[mods]]")
            {
                inModsSection = true;
                continue;
            }
            if (inModsSection && line.StartsWith("[[", StringComparison.Ordinal)) break;
            if (!inModsSection) continue;
            foreach (string key in new[] { "version", "displayName", "authors" })
            {
                if (!line.StartsWith(key + "=", StringComparison.Ordinal)) continue;
                int start = line.IndexOf('"') + 1;
                int end = line.LastIndexOf('"');
                if (start > 0 && end > start) values[key] = line[start..end];
            }
        }
        return values;
    }

    private bool ValidateNbtTag(NbtTag tag, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int depth = 1;
        for (NbtTag? parent = tag.Parent; parent != null; parent = parent.Parent)
        {
            depth++;
            if (depth > MaximumNbtDepth)
            {
                throw new InvalidDataException("World metadata nesting is too deep.");
            }
        }
        return true;
    }
}
