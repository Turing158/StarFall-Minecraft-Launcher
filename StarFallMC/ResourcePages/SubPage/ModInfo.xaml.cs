using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.Entity.Enum;
using StarFallMC.Entity.Resource;
using StarFallMC.Util;
using StarFallMC.Navigation;
using StarFallMC.Services.Resources;
using StarFallMC.Services.Download;
using Button = StarFallMC.Component.Button;
using MessageBox = StarFallMC.Component.MessageBox;
using MessageBoxResult = StarFallMC.Entity.Enum.MessageBoxResult;

namespace StarFallMC.ResourcePages.SubPage;

public partial class ModInfo : Page, IPageLifecycle {
    private readonly LauncherUiCoordinator uiCoordinator;
    private readonly DownloadCoordinator downloadCoordinator;
    private ViewModel viewModel = new();
    private CancellationTokenSource cts;
    private CancellationTokenSource logoCancellationTokenSource = new();
    private Task activeLoadTask = Task.CompletedTask;
    private readonly bool loadRemoteData;
    public ModInfo(
        MinecraftResource? resource = null,
        bool loadRemoteData = true,
        LauncherUiCoordinator? uiCoordinator = null,
        DownloadCoordinator? downloadCoordinator = null) {
        this.uiCoordinator = uiCoordinator ?? new LauncherUiCoordinator();
        this.downloadCoordinator = downloadCoordinator ?? new DownloadCoordinator(this.uiCoordinator);
        InitializeComponent();
        DataContext = viewModel;
        this.loadRemoteData = loadRemoteData;
        cts = new CancellationTokenSource();
        if (resource != null) {
            setResource(resource);
        }
    }
    
    private void setResource(MinecraftResource resource) {
        viewModel.Resource = resource;
        viewModel.PlatFormVisibility = !string.IsNullOrEmpty(resource.ModrinthSha1) && resource.CurseForgeSha1 != 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        viewModel.IsCurseForgeSource = resource.CurseForgeSha1 != 0 || resource.CurseForgeId != 0;
        Pagination.TotalCount = 1;
        Pagination.CurrentPage = 1;
    }

    private async Task LoadLogoAsync() {
        var resource = viewModel.Resource;
        if (resource == null || string.IsNullOrWhiteSpace(resource.Logo)) {
            return;
        }

        try {
            await resource.EnsureLogoLoadedAsync(logoCancellationTokenSource.Token);
        }
        catch (OperationCanceledException) {
        }
    }
    
    private async Task GetDownloadFiles(bool init = true) {
        var resource = viewModel.Resource;
        if (resource == null) {
            return;
        }
        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();
        NoDownloadSource.Visibility = Visibility.Collapsed;
        Loading.Visibility = Visibility.Visible;
        viewModel.Downloaders = new List<ModDownloader>();
        if (init) {
            if (viewModel.IsCurseForgeSource) {
                viewModel.CurseForgeDownloaders = await GetCurseForgeDownloadersAsync(resource, cts.Token);
            }
            else {
                var modrinth = await GetModrinthDownloadersAsync(resource, cts.Token);
                viewModel.ModrinthDownloaders = modrinth;
                viewModel.IsCurseForgeSource = false;
                if (modrinth.Count == 0) {
                    viewModel.IsCurseForgeSource = true;
                    viewModel.CurseForgeDownloaders = await GetCurseForgeDownloadersAsync(resource, cts.Token);
                }
            }
            
        }
        else {
            if (viewModel.IsCurseForgeSource) {
                viewModel.CurseForgeDownloaders.Clear();
                viewModel.CurseForgeDownloaders = await GetCurseForgeDownloadersAsync(resource, cts.Token);
            }
            else {
                viewModel.ModrinthDownloaders.Clear();
                viewModel.ModrinthDownloaders = await GetModrinthDownloadersAsync(resource, cts.Token);
            }
        }
        Pagination.CurrentPage = 1;
        SetDownloaderPage();
    }

    public class ViewModel : INotifyPropertyChanged {
        
        private MinecraftResource? _resource;
        
        public MinecraftResource? Resource {
            get => _resource;
            set => SetField(ref _resource, value);
        }
        
        private List<ModDownloader> _downloaders = new();
        public List<ModDownloader> Downloaders {
            get => _downloaders;
            set => SetField(ref _downloaders, value);
        }
        
        private List<ModDownloader> _modrinthDownloaders = new();
        public List<ModDownloader> ModrinthDownloaders {
            get => _modrinthDownloaders;
            set => SetField(ref _modrinthDownloaders, value);
        }
        
        private List<ModDownloader> _curseForgeDownloaders = new();
        public List<ModDownloader> CurseForgeDownloaders {
            get => _curseForgeDownloaders;
            set => SetField(ref _curseForgeDownloaders, value);
        }

        private bool _isCurseForgeSource = false;
        public bool IsCurseForgeSource {
            get => _isCurseForgeSource;
            set => SetField(ref _isCurseForgeSource, value);
        }

        private Visibility _platFormVisibility = Visibility.Visible;
        public Visibility PlatFormVisibility {
            get => _platFormVisibility;
            set => SetField(ref _platFormVisibility, value);
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    private void GoToModWebPage_OnClick(object sender, RoutedEventArgs e) {
        var item = sender as Button;
        if (item == null || item.Tag == null) {
            MessageTips.Show("暂无 Mod 资源页面");
            return;
        }

        var link = item.Tag.ToString();
        if (string.IsNullOrWhiteSpace(link) || !NetworkUtil.IsValidUrl(link)) {
            MessageTips.Show("Mod 资源页面 URL 无效");
            return;
        }
        NetworkUtil.OpenUrl(link);
    }

    private async void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        var listView = sender as ListView;
        if (listView == null) {
            return;
        }
        if (listView.SelectedIndex < 0) {
            return;
        }
        
        if (listView.SelectedItem is not ModDownloader modDownloader || modDownloader.File is null) {
            listView.SelectedIndex = -1;
            return;
        }
        listView.SelectedIndex = -1;
        
        await DownloadFileAsync(modDownloader.File);
    }
    
    private async Task DownloadFileAsync(DownloadFile download) {
        Console.WriteLine($"下载文件：{download}");
        SaveFileDialog sfd = new SaveFileDialog();
        sfd.Title = $"请选择保存 {download.Name} 的文件夹";
        sfd.DefaultDirectory = DirFileUtil.CurrentDirPosition;
        sfd.FileName = download.Name;
        if (sfd.ShowDialog() == true) {
            download.FilePath = Path.Combine(sfd.FileName);
            MessageTips.Show($"正在下载 {download.Name}");
            var result = await downloadCoordinator.DownloadSingle(download);
            if (result) {
                MessageBox.Show(
                    $"文件 {download.Name} 下载完成，已保存至({download.FilePath})",
                    "下载成功",btnType:MessageBoxBtnType.ConfirmAndCustom,
                    customBtnText:"前往文件夹",
                    callback: r => {
                        if (r == MessageBoxResult.Custom) {
                            DirFileUtil.OpenContainingFolder(download.FilePath);
                        }
                    });
            }
            else {
                MessageBox.Show($"文件 {download.FileName} 下载失败：{download.ErrorMessage}","下载失败");
            }
            
        }
    }

    private bool isFirstLoad = true;
    private void SetDownloaderPage() {
        viewModel.Downloaders = new List<ModDownloader>();
        if (isFirstLoad) {
            Loading.Visibility = Visibility.Visible;
            isFirstLoad = false;
        }
        else {
            Loading.Visibility = Visibility.Collapsed;
            if (viewModel.IsCurseForgeSource) {
                viewModel.Downloaders = NetworkUtil.GetPageList(viewModel.CurseForgeDownloaders,Pagination.CurrentPage,20);
                Pagination.TotalCount = viewModel.CurseForgeDownloaders.Count;
            }
            else {
                viewModel.Downloaders = NetworkUtil.GetPageList(viewModel.ModrinthDownloaders,Pagination.CurrentPage,20);
                Pagination.TotalCount = viewModel.ModrinthDownloaders.Count;
            }
            if (viewModel.Downloaders == null || viewModel.Downloaders.Count == 0) {
                NoDownloadSource.Visibility = Visibility.Visible;
            }
            else {
                NoDownloadSource.Visibility = Visibility.Collapsed;
            }

            if (Pagination.TotalCount > 10) {
                Pagination.PageNumVisibility = Visibility.Collapsed;
                Pagination.GoToButtonVisibility = Visibility.Visible;
            }
            else {
                Pagination.PageNumVisibility = Visibility.Visible;
                Pagination.GoToButtonVisibility = Visibility.Collapsed;
            }
        }
    }

    private async void RefreshBtn_OnClick(object sender, RoutedEventArgs e) {
        await GetDownloadFiles(false);
    }

    private bool isFirstChangePlatform = true;
    private async void Platform_OnClick(object sender, RoutedEventArgs e) {
        if (isFirstChangePlatform) {
            isFirstChangePlatform = false;
            await GetDownloadFiles(false);
        }
        else {
            Pagination.CurrentPage = 1;
            SetDownloaderPage();
        }
    }

    private void Pagination_OnPageChanged(object sender, SelectionChangedEventArgs e) {
        if (Pagination.CurrentPage < 0) {
            return;
        }
        
        SetDownloaderPage();
    }

    private static async Task<List<ModDownloader>> GetModrinthDownloadersAsync(
        MinecraftResource resource,
        CancellationToken cancellationToken)
    {
        ApiResult<IReadOnlyList<ResourceFileDto>> result = await ResourceServices.Current.Modrinth.GetFilesAsync(
            resource.ModrinthProjectId,
            cancellationToken);
        if (!result.Success || result.Value == null) {
            if (!string.IsNullOrWhiteSpace(result.Error)) Console.WriteLine(result.Error);
            return [];
        }
        return result.Value.Select(ResourceServices.Current.Mapper.Map).ToList();
    }

    private static async Task<List<ModDownloader>> GetCurseForgeDownloadersAsync(
        MinecraftResource resource,
        CancellationToken cancellationToken)
    {
        if (resource.CurseForgeId == 0 && resource.CurseForgeSha1 != 0) {
            ApiResult<int> match = await ResourceServices.Current.CurseForge.ResolveProjectIdByFingerprintAsync(
                resource.CurseForgeSha1,
                cancellationToken);
            if (match.Success && match.Value > 0) resource.CurseForgeId = match.Value;
        }
        ApiResult<IReadOnlyList<ResourceFileDto>> result = await ResourceServices.Current.CurseForge.GetFilesAsync(
            resource.CurseForgeId,
            cancellationToken);
        if (!result.Success || result.Value == null) {
            if (!string.IsNullOrWhiteSpace(result.Error)) Console.WriteLine(result.Error);
            return [];
        }
        return result.Value.Select(ResourceServices.Current.Mapper.Map).ToList();
    }

    public Task ActivateAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (logoCancellationTokenSource.IsCancellationRequested) {
            logoCancellationTokenSource.Dispose();
            logoCancellationTokenSource = new CancellationTokenSource();
        }
        var logoTask = LoadLogoAsync();
        if (!loadRemoteData) {
            activeLoadTask = logoTask;
            return activeLoadTask;
        }
        if (cts.IsCancellationRequested) {
            cts.Dispose();
            cts = new CancellationTokenSource();
        }
        activeLoadTask = Task.WhenAll(GetDownloadFiles(), logoTask);
        return activeLoadTask;
    }

    public async Task DeactivateAsync() {
        cts.Cancel();
        logoCancellationTokenSource.Cancel();
        try {
            await activeLoadTask;
        }
        catch (OperationCanceledException) {
        }
        cts.Dispose();
        cts = new CancellationTokenSource();
        logoCancellationTokenSource.Dispose();
        logoCancellationTokenSource = new CancellationTokenSource();
    }
}
