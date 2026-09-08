using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using StarFallMC.Entity;
using StarFallMC.Services;

namespace StarFallMC.Services.Minecraft;

public sealed record MinecraftGameArgumentSettings(
    string LauncherName,
    string LauncherVersion,
    string CustomTitle,
    string Width,
    string Height,
    bool Fullscreen);

public sealed class MinecraftArgumentBuilder
{
    private readonly MinecraftVersionResolver _resolver;
    private readonly JavaDiscoveryService _javaDiscovery;
    private readonly SystemMemoryService _memory;

    public MinecraftArgumentBuilder(
        MinecraftVersionResolver? resolver = null,
        JavaDiscoveryService? javaDiscovery = null,
        SystemMemoryService? memory = null)
    {
        _resolver = resolver ?? new MinecraftVersionResolver();
        _javaDiscovery = javaDiscovery ?? new JavaDiscoveryService();
        _memory = memory ?? new SystemMemoryService();
    }

    public string BuildJvmArguments(string json, JvmArg jvmArg, string os = "windows", string arch = "x64", string? javaWrapperPath = null)
    {
        JObject args = JObject.Parse(json);
        var output = new StringBuilder();
        const string defaultArgs = "-Dfile.encoding=GB18030 -Dstdout.encoding=GB18030 -Dsun.stdout.encoding=GB18030 -Dstderr.encoding=GB18030 -Dsun.stderr.encoding=GB18030 -Djava.rmi.server.useCodebaseOnly=true -Dcom.sun.jndi.rmi.object.trustURLCodebase=false -Dcom.sun.jndi.cosnaming.object.trustURLCodebase=false -Dlog4j2.formatMsgNoLookups=true";
        JArray? jvm = args["arguments"]?["jvm"] as JArray;
        if (jvm == null)
        {
            output.Append(defaultArgs);
            output.Append($" -Dlog4j.configurationFile=\"{Path.GetFullPath(Path.Combine(jvmArg.currentDir, "versions", jvmArg.versionName, $"{jvmArg.versionName}.xml"))}\"");
            output.Append($" -Dminecraft.client.jar=\"{Path.GetFullPath(Path.Combine(".minecraft", "versions", jvmArg.versionName, jvmArg.primaryJarName))}\"");
            output.Append(" -XX:+UnlockExperimentalVMOptions -XX:+UseG1GC -XX:G1ReservePercent=20 -XX:MaxGCPauseMillis=50 -XX:-UseAdaptiveSizePolicy -XX:-OmitStackTraceInFastThrow -XX:-DontCompileHugeMethods -Dfml.ignoreInvalidMinecraftCertificates=true -Dfml.ignorePatchDiscrepancies=true -XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump");
            output.Append($" -Djava.library.path=\"{jvmArg.nativesDirectory}\" -Dminecraft.launcher.brand=\"{jvmArg.launcherName}\" -Dminecraft.launcher.version=\"{jvmArg.launcherVersion}\" -cp \"{jvmArg.classpath}\"");
        }
        else
        {
            foreach (JToken token in jvm)
            {
                if (token.Type == JTokenType.String)
                {
                    output.Append(token).Append(' ');
                    continue;
                }

                JObject obj = (JObject)token;
                bool allowed = obj["rules"] is not JArray rules || rules.Any(rule =>
                    (rule["os"]?["name"]?.ToString() is string name && name == os && rule["action"]?.ToString() == "allow")
                    || (rule["os"]?["arch"]?.ToString() is string ruleArch && ruleArch == arch && rule["action"]?.ToString() == "allow"));
                if (!allowed) continue;
                JToken? value = obj["value"];
                IEnumerable<JToken> values = value is JArray array
                    ? array
                    : value == null ? Array.Empty<JToken>() : [value];
                foreach (JToken valueToken in values)
                {
                    string argument = valueToken.ToString();
                    string[] split = argument.Split('=');
                    if (split.Length == 2) argument = $"{split[0]}=\"{split[1]}\"";
                    output.Append(argument).Append(' ');
                }
            }
            if (output.Length > 0) output.Length--;
        }

        Replace(ref output, "natives_directory", Path.GetFullPath(jvmArg.nativesDirectory));
        Replace(ref output, "launcher_name", jvmArg.launcherName);
        Replace(ref output, "launcher_version", jvmArg.launcherVersion);
        Replace(ref output, "classpath", jvmArg.classpath);
        Replace(ref output, "library_directory", Path.GetFullPath(jvmArg.libraryDirectory));
        Replace(ref output, "primary_jar_name", jvmArg.primaryJarName);
        Replace(ref output, "version_name", jvmArg.versionName);
        Replace(ref output, "classpath_separator", ";");
        if (javaWrapperPath != null && (args["assets"]?.ToString() is "1.7.10" or "legacy"))
        {
            output.Append($" -jar \"{javaWrapperPath}\"");
        }
        return output.ToString();
    }

    public string BuildMinecraftArguments(string json, MinecraftArg arg, MinecraftGameArgumentSettings settings)
    {
        JObject root = JObject.Parse(json);
        var output = new StringBuilder(root["mainClass"]?.ToString() ?? string.Empty).Append(' ');
        if (root["minecraftArguments"] != null)
        {
            output.Append(root["minecraftArguments"]);
        }
        else if (root["arguments"]?["game"] is JArray game)
        {
            foreach (JToken token in game.Where(token => token.Type == JTokenType.String)) output.Append(token).Append(' ');
        }

        Replace(ref output, "auth_player_name", arg.username);
        Replace(ref output, "version_name", arg.version);
        Replace(ref output, "game_directory", Path.GetFullPath(arg.gameDir));
        Replace(ref output, "assets_root", Path.GetFullPath(arg.assetsDir));
        Replace(ref output, "assets_index_name", root["assets"]?.ToString() ?? string.Empty);
        Replace(ref output, "auth_uuid", arg.uuid);
        Replace(ref output, "auth_access_token", string.IsNullOrEmpty(arg.accessToken) ? Guid.NewGuid().ToString("N") : arg.accessToken);
        Replace(ref output, "user_type", "msa");
        Replace(ref output, "version_type", string.IsNullOrEmpty(settings.CustomTitle) ? $"{settings.LauncherName} {settings.LauncherVersion}" : settings.CustomTitle);
        Replace(ref output, "user_properties", "{}");
        Replace(ref output, "clientid", "{}");
        Replace(ref output, "auth_xuid", "{}");
        output.Append($" --width {settings.Width} --height {settings.Height} {(settings.Fullscreen ? "--fullscreen" : string.Empty)}");
        return output.ToString();
    }

    public (string Java, string Memory) SelectJavaAndMemory(
        string json,
        IGameSettingsState settings,
        IReadOnlyList<JavaItem> configuredJava,
        int suitableJavaVersion = -1)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var javaItems = configuredJava.ToList();
        string java = "java";
        if (settings.CurrentJavaVersionIndex > 0 && settings.CurrentJavaVersionIndex <= javaItems.Count)
        {
            java = _javaDiscovery.GetExecutable(javaItems[settings.CurrentJavaVersionIndex]);
        }
        else
        {
            int required = suitableJavaVersion;
            if (required < 0) required = JObject.Parse(json)["javaVersion"]?["majorVersion"]?.ToObject<int>() ?? 7;
            if (javaItems.Count <= 1) javaItems = _javaDiscovery.DiscoverInstalled().ToList();
            JavaItem? selected = _javaDiscovery.SelectCompatible(javaItems, required);
            if (selected != null) java = _javaDiscovery.GetExecutable(selected);
        }

        int memory = settings.AutoMemoryDisable
            ? settings.MemoryValue
            : Math.Max(656, (int)(_memory.GetAllInfo()[MemoryName.FreeMemory] * 2 / 3));
        return (java, $"-Xms{memory}m");
    }

    public static void Replace(ref StringBuilder builder, string key, string value)
    {
        string replacement = value.Contains(' ') ? $"\"{value}\"" : value;
        builder.Replace($"${{{key}}}", replacement);
    }
}
