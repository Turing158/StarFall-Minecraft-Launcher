using StarFallMC.Entity;
using StarFallMC.Entity.Resource;
using StarFallMC.ResourcePages.SubPage;

namespace StarFallMC.Navigation;

public sealed class LauncherUiCoordinator
{
    private WeakReference<MainWindow>? _mainWindow;
    private WeakReference<Home>? _home;
    private WeakReference<PlayerManage>? _playerManage;
    private WeakReference<SelectGame>? _selectGame;
    private WeakReference<GameInfo>? _gameInfo;
    private WeakReference<DownloadPage>? _downloadPage;

    public void Register(MainWindow page) => _mainWindow = new WeakReference<MainWindow>(page);
    public void Register(Home page) => _home = new WeakReference<Home>(page);
    public void Register(PlayerManage page) => _playerManage = new WeakReference<PlayerManage>(page);
    public void Register(SelectGame page) => _selectGame = new WeakReference<SelectGame>(page);
    public void Register(GameInfo page) => _gameInfo = new WeakReference<GameInfo>(page);
    public void Register(DownloadPage page) => _downloadPage = new WeakReference<DownloadPage>(page);

    public void Unregister(MainWindow page) => ClearIfMatches(ref _mainWindow, page);
    public void Unregister(Home page) => ClearIfMatches(ref _home, page);
    public void Unregister(PlayerManage page) => ClearIfMatches(ref _playerManage, page);
    public void Unregister(SelectGame page) => ClearIfMatches(ref _selectGame, page);
    public void Unregister(GameInfo page) => ClearIfMatches(ref _gameInfo, page);
    public void Unregister(DownloadPage page) => ClearIfMatches(ref _downloadPage, page);

    public void SetHomePlayer(Player player) => Invoke(_home, page => page.SetPlayerFromService(player));
    public void SetHomeGame(MinecraftItem? game) => Invoke(_home, page => page.SetGameInfoFromService(game));
    public void SetHomeStartingState(string state) => Invoke(_home, page => page.SetStartingStateFromService(state));
    public Task HideHomeLaunchingAsync(bool isStop) => InvokeAsync(_home, page => page.HideLaunchingFromServiceAsync(isStop));
    public void ShowHomeLaunchError(MinecraftItem item) => Invoke(_home, page => page.ErrorLaunchFromService(item));
    public void ShowHomeDownloadButton(bool visible) => Invoke(_home, page => page.SwitchDownloadButtonFromService(visible));
    public void SetHomeDownloadState(bool downloading) => Invoke(_home, page => page.SetDownloadStateFromService(downloading));
    public void SetHomeBackground() => Invoke(_home, page => page.SettingBackgroundFromService());
    public void SetHomeNotice(bool visible) => Invoke(_home, page => page.SwitchHomeNoticeFromService(visible));

    public void SetPlayerLoadingText(string text) => Invoke(_playerManage, page => page.SetLoadingTextFromService(text));
    public void SetPlayerListItem(Player player) => Invoke(_playerManage, page => page.SetPlayerListItemFromService(player));

    public Task NavigateSubPageAsync(string pageName, string title) => InvokeAsync(_mainWindow, page => page.SubFrameNavigateAsync(pageName, title));
    public Task ReloadSubPageAsync(string pageName, Action? action) => InvokeAsync(_mainWindow, page => page.ReloadSubFrameAsync(pageName, action));
    public void ShowDownloadPage() => Invoke(_mainWindow, page => page.ShowDownloadPageFromService());
    public Task GoBackAsync() => InvokeAsync(_mainWindow, page => page.BackHandleFromServiceAsync());
    public Task ChangeResourceVersionAsync() => InvokeAsync(_mainWindow, page => page.ChangeResourceVersionFromServiceAsync());
    public Task ShowGameInfoAsync(MinecraftDownloader downloader) => InvokeAsync(_mainWindow, page => page.ShowGameInfoAsync(downloader));
    public Task ShowModInfoAsync(MinecraftResource resource) => InvokeAsync(_mainWindow, page => page.ShowModInfoAsync(resource));
    public Task ShowSaveInfoAsync(SavesResource resource) => InvokeAsync(_mainWindow, page => page.ShowSaveInfoAsync(resource));
    public void CancelGameInstall() => Invoke(_gameInfo, page => page.CancelInstallFromService());

    public void ChangeProcessStatus(string key, Entity.Enum.ProcessStatus status, bool changeNextStep) =>
        Invoke(_downloadPage, page => page.ChangeProcessStatus(key, status, changeNextStep));

    public void ChangeProcessStatusWithIndex(string key, Entity.Enum.ProcessStatus status, int progressIndex, string? progressName = null) =>
        Invoke(_downloadPage, page => page.ChangeProcessStatusWithIndex(key, status, progressIndex, progressName));

    public void ResetProcessStatus(string key, bool autoDoingFirst = false) =>
        Invoke(_downloadPage, page => page.ResetProcessStatus(key, autoDoingFirst));

    public string AppendProcessProgress(string name, List<string> progressNames, bool autoDoingFirst = false) =>
        Invoke(_downloadPage, page => page.AppendProcessProgress(name, progressNames, autoDoingFirst), string.Empty);

    public void ChangeProcessProgressCallback(string key, Action<ProcessProgress> callback, bool isOnDelete = false) =>
        Invoke(_downloadPage, page => page.ChangeProcessProgressCallback(key, callback, isOnDelete));

    public bool HasProcessDoing() => Invoke(_downloadPage, page => page.HasProcessDoing(), false);

    private static void Invoke<T>(WeakReference<T>? reference, Action<T> action) where T : class
    {
        if (reference != null && reference.TryGetTarget(out var target))
        {
            action(target);
        }
    }

    private static TResult Invoke<T, TResult>(WeakReference<T>? reference, Func<T, TResult> action, TResult fallback) where T : class
    {
        return reference != null && reference.TryGetTarget(out var target) ? action(target) : fallback;
    }

    private static Task InvokeAsync<T>(WeakReference<T>? reference, Func<T, Task> action) where T : class
    {
        return reference != null && reference.TryGetTarget(out var target) ? action(target) : Task.CompletedTask;
    }

    private static void ClearIfMatches<T>(ref WeakReference<T>? reference, T page) where T : class
    {
        if (reference != null && reference.TryGetTarget(out var target) && ReferenceEquals(target, page))
        {
            reference = null;
        }
    }
}
