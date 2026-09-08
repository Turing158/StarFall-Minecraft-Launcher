using System.IO;
using StarFallMC.Entity;
using StarFallMC.Util;

namespace StarFallMC.Services.Minecraft;

/// <summary>
/// Resolves Minecraft paths from an explicit game root. The service does not
/// read launcher settings, page fields, or process-global directories.
/// </summary>
public sealed class MinecraftPathService
{
    public string NormalizeRoot(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        return Path.GetFullPath(root);
    }

    public string GetVersionsDirectory(string root) =>
        GetInsideRoot(root, Path.Combine("versions"));

    public string GetVersionDirectory(string root, string versionName) =>
        GetInsideRoot(root, "versions", RequireSegment(versionName, nameof(versionName)));

    public string GetVersionJsonPath(string root, string versionName) =>
        Path.Combine(GetVersionDirectory(root, versionName), $"{RequireSegment(versionName, nameof(versionName))}.json");

    public string GetVersionJarPath(string root, string versionName) =>
        Path.Combine(GetVersionDirectory(root, versionName), $"{RequireSegment(versionName, nameof(versionName))}.jar");

    public string GetNativesDirectory(string root, string versionName) =>
        Path.Combine(GetVersionDirectory(root, versionName), $"{RequireSegment(versionName, nameof(versionName))}-natives");

    public string GetLibrariesDirectory(string root) => GetInsideRoot(root, "libraries");

    public string GetLibraryPath(string root, string relativePath) =>
        GetInsideRoot(root, "libraries", NormalizeRelativePath(relativePath));

    public string GetAssetsDirectory(string root) => GetInsideRoot(root, "assets");

    public string GetAssetIndexPath(string root, string assetId) =>
        GetInsideRoot(root, "assets", "indexes", $"{RequireSegment(assetId, nameof(assetId))}.json");

    public string GetAssetObjectPath(string root, string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        if (hash.Length < 2 || hash.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Asset hash must contain at least two non-whitespace characters.", nameof(hash));
        }

        return GetInsideRoot(root, "assets", "objects", hash[..2], hash);
    }

    public string GetGameDirectory(string root, string versionName, bool isolation)
    {
        string normalizedRoot = NormalizeRoot(root);
        return isolation ? GetVersionDirectory(normalizedRoot, versionName) : normalizedRoot;
    }

    public bool IsVersionPresent(MinecraftItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return !string.IsNullOrWhiteSpace(item.Path)
            && Directory.Exists(item.Path)
            && File.Exists(Path.Combine(item.Path, $"{item.Name}.json"));
    }

    public IReadOnlyList<MinecraftItem> ScanGames(string root, MinecraftVersionResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        string versionsDirectory = GetVersionsDirectory(root);
        if (!Directory.Exists(versionsDirectory))
        {
            return Array.Empty<MinecraftItem>();
        }

        var games = new List<MinecraftItem>();
        foreach (string versionDirectory in Directory.GetDirectories(versionsDirectory))
        {
            string versionName = Path.GetFileName(versionDirectory);
            string jsonPath = Path.Combine(versionDirectory, $"{versionName}.json");
            if (File.Exists(jsonPath))
            {
                var item = resolver.ResolveMinecraftItem(versionDirectory, Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(jsonPath)));
                if (File.Exists(Path.Combine(versionDirectory, "ico.png")))
                {
                    item.Icon = Path.GetFullPath(Path.Combine(versionDirectory, "ico.png"));
                }
                games.Add(item);
            }
        }

        return games.OrderBy(item => item.Name, new DirFileUtil.NaturalComparer(true)).ToArray();
    }

    public MinecraftItem? RenameVersion(MinecraftItem item, string newVersionName)
    {
        ArgumentNullException.ThrowIfNull(item);
        string normalizedName = RequireSegment(newVersionName, nameof(newVersionName));
        string oldDirectory = Path.GetFullPath(item.Path);
        string parentDirectory = Directory.GetParent(oldDirectory)?.FullName
            ?? throw new IOException("The version directory has no parent.");
        string newDirectory = GetInsideRoot(parentDirectory, normalizedName);
        if (Directory.Exists(newDirectory))
        {
            return null;
        }

        string oldName = RequireSegment(item.Name, nameof(item.Name));
        string oldJsonPath = Path.Combine(oldDirectory, $"{oldName}.json");
        string newJsonPath = Path.Combine(oldDirectory, $"{normalizedName}.json");
        File.WriteAllText(oldJsonPath, RenameJson(File.ReadAllText(oldJsonPath), normalizedName));
        File.Move(oldJsonPath, newJsonPath, true);
        File.Move(Path.Combine(oldDirectory, $"{oldName}.jar"), Path.Combine(oldDirectory, $"{normalizedName}.jar"), true);

        string oldNatives = Path.Combine(oldDirectory, $"{oldName}-natives");
        if (Directory.Exists(oldNatives))
        {
            Directory.Move(oldNatives, Path.Combine(oldDirectory, $"{normalizedName}-natives"));
        }

        Directory.Move(oldDirectory, newDirectory);
        item.Name = normalizedName;
        item.Path = Path.GetFullPath(newDirectory);
        if (!string.IsNullOrEmpty(item.Icon) && item.Icon.Contains(":", StringComparison.Ordinal))
        {
            item.Icon = Path.Combine(item.Path, "ico.png");
        }

        return item;
    }

    private static string RenameJson(string json, string newName)
    {
        var root = Newtonsoft.Json.Linq.JObject.Parse(json);
        if (root["id"] != null)
        {
            root["id"] = newName;
        }
        if (root["jar"] != null)
        {
            root["jar"] = newName;
        }
        return root.ToString();
    }

    private string GetInsideRoot(string root, params string[] segments)
    {
        string normalizedRoot = NormalizeRoot(root);
        string candidate = segments.Aggregate(normalizedRoot, Path.Combine);
        string fullCandidate = Path.GetFullPath(candidate);
        string rootPrefix = normalizedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!fullCandidate.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            && !fullCandidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The resolved path escapes the Minecraft root.", nameof(segments));
        }

        return fullCandidate;
    }

    private static string NormalizeRelativePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
    }

    private static string RequireSegment(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value is "." or ".." || value.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            throw new ArgumentException("Path segments cannot contain directory separators.", parameterName);
        }

        return value;
    }
}
