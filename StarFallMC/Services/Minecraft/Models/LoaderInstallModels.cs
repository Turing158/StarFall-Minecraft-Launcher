using StarFallMC.Entity;
using StarFallMC.Entity.Enum;

namespace StarFallMC.Services.Minecraft.Models;

public enum LoaderInstallStepKind
{
    Download,
    WriteVersionJson,
    ResolveFiles,
    RunJava,
    CopyFiles,
    Verify,
    Complete
}

public sealed record LoaderInstallStep(
    int Sequence,
    string Name,
    LoaderInstallStepKind Kind,
    DownloadFile? Download = null,
    string? Executable = null,
    string? Arguments = null,
    string? OutputPath = null);

public sealed record LoaderInstallPlan(
    MinecraftLoader Loader,
    string MinecraftVersion,
    string VersionName,
    string RootDirectory,
    bool Supported,
    IReadOnlyList<LoaderInstallStep> Steps,
    string? UnsupportedReason = null);

public enum InstallRetryDecision
{
    Retry,
    Skip,
    SkipAndExport
}

public sealed record LoaderInstallResult(bool Success, bool Cancelled, string? ProcessKey = null, string? Error = null);
