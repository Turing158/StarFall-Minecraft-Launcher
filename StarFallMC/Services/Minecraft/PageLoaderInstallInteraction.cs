using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.Entity.Enum;
using StarFallMC.Navigation;
using StarFallMC.Services.Minecraft.Models;

namespace StarFallMC.Services.Minecraft;

/// <summary>
/// WPF adapter for loader progress. The installation service only sees the
/// interaction contract, while this adapter keeps the existing download page
/// wording and cancellation behavior.
/// </summary>
public sealed class PageLoaderInstallInteraction : ILoaderInstallInteraction
{
    private readonly LauncherUiCoordinator _uiCoordinator;

    internal PageLoaderInstallInteraction(LauncherUiCoordinator uiCoordinator)
    {
        _uiCoordinator = uiCoordinator ?? throw new ArgumentNullException(nameof(uiCoordinator));
    }

    public string Begin(string versionName, IReadOnlyList<string> stepNames)
    {
        string key = _uiCoordinator.AppendProcessProgress($"安装 {versionName}", stepNames.ToList(), true);
        _uiCoordinator.ChangeProcessProgressCallback(key, _ => _uiCoordinator.CancelGameInstall(), true);
        return key;
    }

    public void StepCompleted(string processKey, bool advance = true) =>
        _uiCoordinator.ChangeProcessStatus(processKey, ProcessStatus.Complete, advance);

    public void StepFailed(string processKey, bool advance = false) =>
        _uiCoordinator.ChangeProcessStatus(processKey, ProcessStatus.Error, advance);

    public void Notify(string message)
    {
        if (!string.IsNullOrWhiteSpace(message)) MessageTips.Show(message, MessageTips.MessageType.Error);
    }

    public async Task<InstallRetryDecision> ChooseRetryAsync(
        IReadOnlyList<DownloadFile> failedFiles,
        CancellationToken cancellationToken)
    {
        InstallRetryDecision decision = InstallRetryDecision.Skip;
        await MessageBox.ShowAsync(
            $"下载文件出现问题，共 {failedFiles.Count} 个文件出现错误。\n可能是网络波动问题，可选择重新下载或跳过。",
            "下载失败",
            MessageBoxBtnType.ConfirmAndCancel,
            result => decision = result == MessageBoxResult.Confirm ? InstallRetryDecision.Retry : InstallRetryDecision.Skip,
            confirmBtnText: "重新下载",
            cancelBtnText: "跳过").WaitAsync(cancellationToken).ConfigureAwait(true);
        return decision;
    }
}
