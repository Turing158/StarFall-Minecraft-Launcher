using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using StarFallMC.Entity;
using StarFallMC.Entity.Enum;
using StarFallMC.Util;
using DownloadDto = StarFallMC.Entity.Download;

namespace StarFallMC.Services.Minecraft;

public sealed record ResolvedMinecraftVersion(
    string VersionName,
    string Json,
    IReadOnlyList<Lib> Libraries,
    IReadOnlyList<string> JvmArguments,
    IReadOnlyList<string> GameArguments);

public sealed record VersionResolutionResult(bool Success, ResolvedMinecraftVersion? Version, string? Error)
{
    public static VersionResolutionResult Failure(string error) => new(false, null, error);
    public static VersionResolutionResult Ok(ResolvedMinecraftVersion version) => new(true, version, null);
}

/// <summary>
/// Pure Minecraft version and library mapping. No file, network, UI, or
/// application-state access is performed by this type.
/// </summary>
public sealed class MinecraftVersionResolver
{
    public VersionResolutionResult ResolveInheritance(string versionName, Func<string, string?> jsonProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionName);
        ArgumentNullException.ThrowIfNull(jsonProvider);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            JObject resolved = ResolveInheritanceCore(versionName, jsonProvider, visiting);
            return VersionResolutionResult.Ok(new ResolvedMinecraftVersion(
                versionName,
                resolved.ToString(),
                GetLibs(resolved.ToString()),
                ReadStringArguments(resolved["arguments"]?["jvm"]),
                ReadStringArguments(resolved["arguments"]?["game"])));
        }
        catch (Exception exception) when (exception is InvalidDataException or Newtonsoft.Json.JsonException)
        {
            return VersionResolutionResult.Failure(exception.Message);
        }
    }

    public string GetMainClass(string json)
    {
        JObject root = JObject.Parse(json);
        return root["mainClass"]?.ToString() ?? "net.minecraft.client.main.Main";
    }

    public MinecraftItem ResolveMinecraftItem(string versionPath, JObject root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var item = new MinecraftItem {
            Name = root["id"]?.ToString() ?? string.Empty,
            Path = Path.GetFullPath(versionPath)
        };

        JToken patches = root["patches"] ?? new JArray();
        if (patches.Count() != 0)
        {
            string loaderName = patches[patches.Count() - 1]?["id"]?.ToString()?.ToLowerInvariant() ?? string.Empty;
            (item.Loader, item.Icon) = loaderName switch {
                _ when loaderName.Contains("game") => (MinecraftLoader.Minecraft, root["type"]?.ToString() == "release" ? "/assets/DefaultGameIcon/Minecraft.png" : "/assets/DefaultGameIcon/snapshot.png"),
                _ when loaderName.Contains("optifine") => (MinecraftLoader.Optifine, "/assets/DefaultGameIcon/Optifine.png"),
                _ when loaderName.Contains("liteloader") => (MinecraftLoader.LiteLoader, "/assets/DefaultGameIcon/Liteloader.png"),
                _ when loaderName.Contains("neoforge") || root.ToString().Contains("neoforged", StringComparison.OrdinalIgnoreCase) => (MinecraftLoader.NeoForge, "/assets/DefaultGameIcon/NeoForge.png"),
                _ when loaderName.Contains("forge") => (MinecraftLoader.Forge, "/assets/DefaultGameIcon/Forge.png"),
                _ when loaderName.Contains("fabric") => (MinecraftLoader.Fabric, "/assets/DefaultGameIcon/Fabric.png"),
                _ when loaderName.Contains("quilt") => (MinecraftLoader.Quilt, "/assets/DefaultGameIcon/quiltmc.png"),
                _ => (MinecraftLoader.Unknown, "/assets/DefaultGameIcon/unknowGame.png")
            };
        }
        else
        {
            string libraries = root["libraries"]?.ToString() ?? string.Empty;
            if (libraries.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
            {
                item.Loader = MinecraftLoader.NeoForge;
                item.Icon = "/assets/DefaultGameIcon/NeoForge.png";
            }
            else if (libraries.Contains("minecraftforge", StringComparison.OrdinalIgnoreCase))
            {
                item.Loader = MinecraftLoader.Forge;
                item.Icon = "/assets/DefaultGameIcon/Forge.png";
            }
            else if (libraries.Contains("fabricmc", StringComparison.OrdinalIgnoreCase))
            {
                item.Loader = MinecraftLoader.Fabric;
                item.Icon = "/assets/DefaultGameIcon/Fabric.png";
            }
            else if (libraries.Contains("quiltmc", StringComparison.OrdinalIgnoreCase))
            {
                item.Loader = MinecraftLoader.Quilt;
                item.Icon = "/assets/DefaultGameIcon/quiltmc.png";
            }
            else if (root["type"] != null)
            {
                item.Loader = MinecraftLoader.Minecraft;
                item.Icon = root["type"]?.ToString() == "release" ? "/assets/DefaultGameIcon/Minecraft.png" : "/assets/DefaultGameIcon/snapshot.png";
            }
            else
            {
                item.Loader = MinecraftLoader.Unknown;
                item.Icon = "/assets/DefaultGameIcon/unknowGame.png";
            }
        }

        return item;
    }

    public string GetLoaderVersion(string json, MinecraftLoader loaderType)
    {
        if (loaderType == MinecraftLoader.Unknown)
        {
            return string.Empty;
        }

        string loaderName = loaderType switch {
            MinecraftLoader.Minecraft => "game",
            MinecraftLoader.Optifine => "optifine",
            MinecraftLoader.LiteLoader => "liteloader",
            MinecraftLoader.Forge => "forge",
            MinecraftLoader.Fabric => "fabric",
            MinecraftLoader.Quilt => "quiltmc",
            MinecraftLoader.NeoForge => "neoforged",
            _ => string.Empty
        };

        if (JObject.Parse(json)["patches"] is JArray patches)
        {
            foreach (JToken patch in patches)
            {
                if (patch["id"]?.ToString() == loaderName)
                {
                    return patch["version"]?.ToString() ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }

    public string GetSuitableJava(string json)
    {
        int version = JObject.Parse(json)["javaVersion"]?["majorVersion"]?.Value<int>() ?? 0;
        return version is > 0 and < 10 ? $"1.{version}" : version.ToString();
    }

    public List<Lib> JsonToLib(JArray? libs)
    {
        var result = new List<Lib>();
        if (libs == null)
        {
            return result;
        }

        foreach (JToken lib in libs)
        {
            string? name = lib["name"]?.ToString();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            string[] nameParts = name.Split(':');
            string path = string.Join('/', nameParts[0].Split('.')) + "/";
            string fileName = string.Empty;
            for (int i = 1; i < nameParts.Length; i++)
            {
                fileName += nameParts[i] + "-";
                if (i == nameParts.Length - 1)
                {
                    if (!string.IsNullOrEmpty(nameParts[i]) && IsNumber(nameParts[i][..1]))
                    {
                        path += nameParts[i] + "/";
                    }
                    fileName = fileName[..^1] + ".jar";
                }
                else
                {
                    path += nameParts[i] + "/";
                }
            }
            path += fileName;

            DownloadDto artifact = ParseDownload(lib["downloads"]?["artifact"], 0);
            var classifiers = new Dictionary<string, DownloadDto>();
            List<LibRule> rules = CreateNameRules(name);
            JToken? classifierJson = lib["downloads"]?["classifiers"];
            if (classifierJson != null)
            {
                rules.Clear();
                foreach (string classifierName in new[] { "natives-linux", "natives-windows", "natives-macos", "natives-windows-32", "natives-windows-64" })
                {
                    JToken? classifier = classifierJson[classifierName];
                    if (classifier == null)
                    {
                        continue;
                    }
                    AddRuleForName(rules, classifierName, true);
                    classifiers[classifierName] = ParseDownload(classifier, -1);
                }
            }

            if (lib["rules"] is JArray jsonRules)
            {
                rules.Clear();
                foreach (JToken rule in jsonRules)
                {
                    bool allow = rule["action"]?.ToString() == "allow";
                    JToken? os = rule["os"];
                    if (os == null)
                    {
                        RuleToLib(ref rules, DeviceOs.Linux, allow);
                        RuleToLib(ref rules, DeviceOs.Windows, allow);
                        RuleToLib(ref rules, DeviceOs.MacOs, allow);
                    }
                    else
                    {
                        switch (os["name"]?.ToString())
                        {
                            case "linux": RuleToLib(ref rules, DeviceOs.Linux, allow); break;
                            case "windows": RuleToLib(ref rules, DeviceOs.Windows, allow); break;
                            case "osx": RuleToLib(ref rules, DeviceOs.MacOs, allow); break;
                        }
                    }
                }
            }

            if (classifierJson != null && lib["downloads"]?["artifact"] == null)
            {
                path = string.Empty;
            }

            result.Add(new Lib { name = name, path = path, rules = rules, artifact = artifact, classifiers = classifiers });
        }

        return result;
    }

    public void RuleToLib(ref List<LibRule> rules, DeviceOs os, bool isAllow)
    {
        int index = rules.FindIndex(rule => rule.Os == os);
        if (index >= 0)
        {
            rules[index].IsAllow = isAllow;
            return;
        }

        rules.Add(new LibRule { IsAllow = isAllow, Os = os });
    }

    public List<Lib> GetLibs(string json)
    {
        JObject root = JObject.Parse(json);
        var all = new List<Lib>();
        all.AddRange(JsonToLib(root["libraries"] as JArray));
        if (root["patches"] is JArray patches)
        {
            foreach (JToken patch in patches)
            {
                all.AddRange(JsonToLib(patch["libraries"] as JArray));
            }
        }

        var unique = new HashSet<Lib>();
        var result = new List<Lib>();
        foreach (Lib lib in all)
        {
            if (unique.Add(lib))
            {
                result.Add(lib);
            }
        }

        return result;
    }

    public bool IsNumber(string value) => !string.IsNullOrWhiteSpace(value)
        && Regex.IsMatch(value, @"^[-+]?(?:\d+\.\d*|\.\d+|\d+)$");

    public string GetClassPaths(IReadOnlyList<Lib> libs, string currentDir, string versionName)
    {
        var classPaths = new HashSet<string>();
        var alreadyAdded = new List<Lib>();
        foreach (Lib lib in libs)
        {
            if (lib.rules.Count > 0 && !lib.rules.Any(rule => rule.Os == DeviceOs.Windows && rule.IsAllow))
            {
                continue;
            }
            if (classPaths.Contains(lib.path) || string.IsNullOrEmpty(lib.path))
            {
                continue;
            }
            int index = !lib.name.Contains("natives", StringComparison.Ordinal)
                ? alreadyAdded.FindIndex(item => item.nameOutVersion == lib.nameOutVersion)
                : -1;
            if (index >= 0)
            {
                Lib old = alreadyAdded[index];
                if (!old.nameLast.Equals(lib.nameLast)
                    && NetworkUtil.IsValidVersion(old.nameLast)
                    && NetworkUtil.IsValidVersion(lib.nameLast)
                    && NetworkUtil.GetNewerVersion(old.nameLast, lib.nameLast) == lib.nameLast)
                {
                    alreadyAdded[index] = lib;
                    classPaths.Remove(old.path);
                    classPaths.Add(lib.path);
                }
                continue;
            }

            alreadyAdded.Add(lib);
            classPaths.Add(lib.path);
        }

        var result = new StringBuilder();
        foreach (string path in classPaths)
        {
            result.Append(Path.GetFullPath(Path.Combine(currentDir, "libraries", path))).Append(';');
        }
        result.Append(Path.GetFullPath(Path.Combine(currentDir, "versions", versionName, $"{versionName}.jar")));
        return result.ToString();
    }

    public JObject ParseVersionJson(string json, string versionName, bool isPatch = false)
    {
        JObject root = JObject.Parse(json);
        var parsed = new JObject {
            ["id"] = isPatch ? "game" : versionName,
            ["mainClass"] = root["mainClass"],
            ["assetIndex"] = root["assetIndex"],
            ["assets"] = root["assets"],
            ["javaVersion"] = root["javaVersion"],
            ["libraries"] = root["libraries"]
        };
        if (root["minecraftArguments"] != null) parsed["minecraftArguments"] = root["minecraftArguments"];
        if (root["arguments"] != null) parsed["arguments"] = root["arguments"];
        if (!isPatch) parsed["jar"] = versionName;
        if (root["complianceLevel"] != null) parsed["complianceLevel"] = root["complianceLevel"];
        foreach (var property in root.Properties())
        {
            parsed[property.Name] ??= property.Value;
        }
        return parsed;
    }

    public void VersionArgumentParse(JObject versionJson, JToken installJson)
    {
        ArgumentNullException.ThrowIfNull(versionJson);
        ArgumentNullException.ThrowIfNull(installJson);
        if (versionJson["arguments"] is JObject arguments)
        {
            JArray game = arguments["game"] as JArray ?? new JArray();
            JArray jvm = arguments["jvm"] as JArray ?? new JArray();
            AppendArguments(game, installJson["arguments"]?["game"] as JArray);
            AppendArguments(jvm, installJson["arguments"]?["jvm"] as JArray);
            arguments["game"] = game;
            arguments["jvm"] = jvm;
            return;
        }

        versionJson["minecraftArguments"] = installJson["minecraftArguments"];
    }

    private static void AppendArguments(JArray target, JArray? source)
    {
        if (source == null) return;
        foreach (JToken value in source)
        {
            target.Add(value.Type == JTokenType.String ? value.ToString().Replace(" ", string.Empty) : value);
        }
    }

    private static DownloadDto ParseDownload(JToken? token, int defaultSize)
    {
        return new DownloadDto(
            token?["path"]?.ToString() ?? string.Empty,
            token?["url"]?.ToString() ?? string.Empty,
            token?["sha1"]?.ToString() ?? string.Empty,
            token?["size"]?.Value<int>() ?? defaultSize);
    }

    private static List<LibRule> CreateNameRules(string name)
    {
        var rules = new List<LibRule>();
        if (name.Contains("natives-linux", StringComparison.Ordinal)) rules.Add(new LibRule { IsAllow = true, Os = DeviceOs.Linux });
        if (name.Contains("natives-windows", StringComparison.Ordinal)) rules.Add(new LibRule { IsAllow = true, Os = DeviceOs.Windows });
        if (name.Contains("natives-macos", StringComparison.Ordinal)) rules.Add(new LibRule { IsAllow = true, Os = DeviceOs.MacOs });
        return rules;
    }

    private static void AddRuleForName(List<LibRule> rules, string name, bool allow)
    {
        DeviceOs os = name.Contains("linux", StringComparison.Ordinal) ? DeviceOs.Linux
            : name.Contains("macos", StringComparison.Ordinal) ? DeviceOs.MacOs : DeviceOs.Windows;
        int index = rules.FindIndex(rule => rule.Os == os);
        if (index >= 0) rules[index].IsAllow = allow;
        else rules.Add(new LibRule { IsAllow = allow, Os = os });
    }

    private JObject ResolveInheritanceCore(string versionName, Func<string, string?> jsonProvider, HashSet<string> visiting)
    {
        if (!visiting.Add(versionName))
        {
            throw new InvalidDataException($"Version inheritance cycle detected at '{versionName}'.");
        }

        try
        {
            string json = jsonProvider(versionName) ?? throw new InvalidDataException($"Version JSON '{versionName}' was not found.");
            JObject child = JObject.Parse(json);
            string? parentName = child["inheritsFrom"]?.ToString();
            if (string.IsNullOrWhiteSpace(parentName))
            {
                return (JObject)child.DeepClone();
            }

            JObject parent = ResolveInheritanceCore(parentName, jsonProvider, visiting);
            return MergeVersionJson(parent, child);
        }
        finally
        {
            visiting.Remove(versionName);
        }
    }

    private static JObject MergeVersionJson(JObject parent, JObject child)
    {
        var merged = (JObject)parent.DeepClone();
        foreach (JProperty property in child.Properties())
        {
            if (property.Name == "libraries" && property.Value is JArray childLibraries)
            {
                var libraries = merged["libraries"] as JArray ?? new JArray();
                foreach (JToken library in childLibraries) libraries.Add(library.DeepClone());
                merged["libraries"] = libraries;
            }
            else if (property.Name == "arguments" && property.Value is JObject childArguments)
            {
                var arguments = merged["arguments"] as JObject ?? new JObject();
                foreach (string key in new[] { "jvm", "game" })
                {
                    var values = arguments[key] as JArray ?? new JArray();
                    if (childArguments[key] is JArray childValues)
                    {
                        foreach (JToken value in childValues) values.Add(value.DeepClone());
                    }
                    arguments[key] = values;
                }
                merged["arguments"] = arguments;
            }
            else
            {
                merged[property.Name] = property.Value.DeepClone();
            }
        }

        return merged;
    }

    private static IReadOnlyList<string> ReadStringArguments(JToken? token) => token is JArray array
        ? array.Where(value => value.Type == JTokenType.String).Select(value => value.ToString()).ToArray()
        : Array.Empty<string>();
}
