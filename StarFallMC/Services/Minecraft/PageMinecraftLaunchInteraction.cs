using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.Entity.Enum;
using StarFallMC.Navigation;
using StarFallMC.Util;

namespace StarFallMC.Services.Minecraft;

public sealed class PageMinecraftLaunchInteraction : IMinecraftLaunchInteraction
{
    private readonly LauncherUiCoordinator _uiCoordinator;

    internal PageMinecraftLaunchInteraction(LauncherUiCoordinator uiCoordinator)
    {
        _uiCoordinator = uiCoordinator ?? throw new ArgumentNullException(nameof(uiCoordinator));
    }

    public void SetStatus(string status) => _uiCoordinator.SetHomeStartingState(status);

    public async Task<bool> RetryDownloadsAsync(IReadOnlyList<DownloadFile> failedFiles, CancellationToken cancellationToken)
    {
        bool retry = false;
        await MessageBox.ShowAsync(
            $"下载文件出现问题，共 {failedFiles.Count} 个文件出现错误。\n可能是网络波动问题，可选择重新下载 或 尝试重新启动 以及 前往“版本属性”处重新补全下载。",
            "下载失败",
            MessageBoxBtnType.ConfirmAndCancel,
            result => retry = result == MessageBoxResult.Confirm,
            confirmBtnText: "重新下载",
            cancelBtnText: "跳过").WaitAsync(cancellationToken).ConfigureAwait(true);
        return retry;
    }

    public async Task<bool> ConfirmUnsafeJavaAsync(string versionName, string? suitableJava, CancellationToken cancellationToken)
    {
        bool launch = false;
        string extraInfo = string.IsNullOrWhiteSpace(suitableJava) ? string.Empty : $"当前版本 {versionName} 最适合的Java版本为 Java{suitableJava}，";
        await MessageBox.ShowAsync(
            $"未获取到合适的Java版本，是否还要坚持启动！\n继续启动可能会出现不可描述的错误！\n{extraInfo}建议前往下载！",
            "未获取到合适的Java版本",
            MessageBoxBtnType.ConfirmAndCancelAndCustom,
            result =>
            {
                launch = result == MessageBoxResult.Confirm;
                if (result == MessageBoxResult.Custom) NetworkUtil.OpenUrl("https://www.oracle.com/java/technologies/downloads/");
            },
            customBtnText: "前往下载").WaitAsync(cancellationToken).ConfigureAwait(true);
        return launch;
    }

    public Task HideLaunchingAsync(bool isStop) => _uiCoordinator.HideHomeLaunchingAsync(isStop);

    public void ShowLaunchError(MinecraftItem minecraft, int exitCode)
    {
        Console.WriteLine($"Minecraft出现失败，错误代码：{exitCode}");
        _uiCoordinator.ShowHomeLaunchError(minecraft);
    }
}
