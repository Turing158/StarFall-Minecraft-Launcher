using System.IO;
using Microsoft.Win32;
using StarFallMC.Entity;
using StarFallMC.Util;

namespace StarFallMC.Services.Minecraft;

public interface IJavaProcessRunner
{
    Task<ProcessResult> RunAsync(string executable, string arguments, CancellationToken cancellationToken);
}

public sealed class ProcessRunnerJavaProcessRunner : IJavaProcessRunner
{
    private readonly ProcessRunner _runner;

    public ProcessRunnerJavaProcessRunner(ProcessRunner? runner = null)
    {
        _runner = runner ?? new ProcessRunner(outputCapacity: 64, maxLineLength: 4096);
    }

    public async Task<ProcessResult> RunAsync(string executable, string arguments, CancellationToken cancellationToken)
    {
        await using ProcessRun run = _runner.Start(executable, arguments, cancellationToken);
        return await run.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class JavaDiscoveryService
{
    private static readonly string[] RegistryPaths = {
        @"SOFTWARE\JavaSoft\Java Runtime Environment",
        @"SOFTWARE\JavaSoft\Java Development Kit",
        @"SOFTWARE\JavaSoft\JDK"
    };

    private readonly IJavaProcessRunner _processRunner;

    public JavaDiscoveryService(IJavaProcessRunner? processRunner = null)
    {
        _processRunner = processRunner ?? new ProcessRunnerJavaProcessRunner();
    }

    public IReadOnlyList<JavaItem> DiscoverInstalled()
    {
        var result = new List<JavaItem>();
        foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            foreach (string registryPath in RegistryPaths)
            {
                using RegistryKey? key = baseKey.OpenSubKey(registryPath);
                string prefix = registryPath.Contains("Java Runtime Environment", StringComparison.Ordinal) ? "JRE-" : "JDK-";
                if (key == null) continue;
                foreach (string version in key.GetSubKeyNames())
                {
                    using RegistryKey? subKey = key.OpenSubKey(version);
                    string? javaHome = subKey?.GetValue("JavaHome") as string;
                    if (subKey == null || string.IsNullOrWhiteSpace(javaHome) || result.Any(item => string.Equals(item.Path, javaHome, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    result.Add(new JavaItem($"{prefix}{version}", javaHome, version));
                }
            }
        }

        return result;
    }

    public static string ParseVersionOutput(IReadOnlyList<string> output)
    {
        string? versionLine = output.FirstOrDefault(line => line.Contains('"'));
        if (string.IsNullOrEmpty(versionLine)) return string.Empty;
        string[] values = versionLine.Split('"');
        return values.Length > 1 ? values[1] : string.Empty;
    }

    public async Task<string?> ProbeVersionAsync(string javaHome, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(javaHome);
        string executable = Path.Combine(javaHome, "bin", "java.exe");
        if (!File.Exists(executable)) return null;
        ProcessResult result = await _processRunner.RunAsync(executable.Trim('"'), "-version", cancellationToken).ConfigureAwait(false);
        string version = ParseVersionOutput(result.Output);
        return string.IsNullOrEmpty(version) ? null : version;
    }

    public JavaItem? SelectCompatible(IEnumerable<JavaItem> javaItems, int majorVersion)
    {
        string required = majorVersion < 10 ? $"1.{majorVersion}" : majorVersion.ToString();
        return javaItems.FirstOrDefault(item => item.Version?.Contains(required, StringComparison.OrdinalIgnoreCase) == true);
    }

    public string GetExecutable(JavaItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        string path = Path.GetFullPath(Path.Combine(item.Path, "bin", "java.exe"));
        return path.Contains(' ') ? $"\"{path}\"" : path;
    }

    public async Task<string> ResolveLegacyJavaAsync(
        string json,
        string javaArgument,
        Func<int, (string Java, string Memory)> fallbackSelector,
        CancellationToken cancellationToken = default)
    {
        var root = Newtonsoft.Json.Linq.JObject.Parse(json);
        if (!string.Equals(root["assets"]?.ToString(), "legacy", StringComparison.Ordinal)) return javaArgument;

        string? version = await ProbeExecutableVersionAsync(javaArgument, cancellationToken).ConfigureAwait(false);
        if (version?.Contains("1.7", StringComparison.Ordinal) == true) return javaArgument;
        string fallbackJava = fallbackSelector(7).Java;
        version = await ProbeExecutableVersionAsync(fallbackJava, cancellationToken).ConfigureAwait(false);
        return version?.Contains("1.7", StringComparison.Ordinal) == true ? fallbackJava : string.Empty;
    }

    private async Task<string?> ProbeExecutableVersionAsync(string executable, CancellationToken cancellationToken)
    {
        ProcessResult result = await _processRunner.RunAsync(executable, "-version", cancellationToken).ConfigureAwait(false);
        string version = ParseVersionOutput(result.Output);
        return string.IsNullOrEmpty(version) ? null : version;
    }
}
