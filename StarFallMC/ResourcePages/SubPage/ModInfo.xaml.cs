using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.Entity.Resource;
using StarFallMC.Util;
using Button = StarFallMC.Component.Button;
using MessageBox = StarFallMC.Component.MessageBox;

namespace StarFallMC.ResourcePages.SubPage;

public partial class ModInfo : Page {
    public static Action<ModResource> SetResource;

    private ViewModel viewModel = new();
    private CancellationTokenSource cts;
    public ModInfo() {
        InitializeComponent();
        DataContext = viewModel;
        SetResource = setResource;
    }
    
    private void setResource(ModResource resource) {
        Dispatcher.BeginInvoke(() => {
            viewModel.Resource = resource;
            if (!string.IsNullOrEmpty(resource.ModrinthSha1) && resource.CurseForgeSha1 != 0) {
                viewModel.PlatFormVisibility = Visibility.Visible;
            }
            else {
                viewModel.PlatFormVisibility = Visibility.Collapsed;
            }
            if (resource.CurseForgeSha1 != 0 || resource.CurseForgeId != 0) {
                viewModel.IsCurseForgeSource = true;
            }
            else {
                viewModel.IsCurseForgeSource = false;
            }
            GetDownloadFiles();
            Pagination.TotalCount = 1;
            Pagination.CurrentPage = 1;
        });
    }
    
    private async Task GetDownloadFiles(bool init = true) {
        if (viewModel.Resource == null) {
            return;
        }
        cts?.Cancel();
        cts = new CancellationTokenSource();
        NoDownloadSource.Visibility = Visibility.Collapsed;
        Loading.Visibility = Visibility.Visible;
        viewModel.Downloaders = new List<ModDownloader>();
        if (init) {
            if (viewModel.IsCurseForgeSource) {
                var curseForge = await ResourceUtil.GetModDownloaderByCurseForge(viewModel.Resource, cts.Token);
                viewModel.CurseForgeDownloaders = curseForge;
            }
            else {
                var modrinth = await ResourceUtil.GetModDownloaderByModrinth(viewModel.Resource, cts.Token);
                viewModel.ModrinthDownloaders = modrinth;
                viewModel.IsCurseForgeSource = false;
                if (modrinth.Count == 0) {
                    viewModel.IsCurseForgeSource = true;
                    var curseForge = await ResourceUtil.GetModDownloaderByCurseForge(viewModel.Resource, cts.Token);
                    viewModel.CurseForgeDownloaders = curseForge;
                }
            }
            
        }
        else {
            if (viewModel.IsCurseForgeSource) {
                viewModel.CurseForgeDownloaders.Clear();
                var curseForge = await ResourceUtil.GetModDownloaderByCurseForge(viewModel.Resource, cts.Token);
                viewModel.CurseForgeDownloaders = curseForge;
            }
            else {
                viewModel.ModrinthDownloaders.Clear();
                var modrinth = await ResourceUtil.GetModDownloaderByModrinth(viewModel.Resource, cts.Token);
                viewModel.ModrinthDownloaders = modrinth;
            }
        }
        Pagination.CurrentPage = 1;
        SetDownloaderPage();
    }

    public class ViewModel : INotifyPropertyChanged {
        
        private ModResource _resource;
        
        public ModResource Resource {
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

        if (!NetworkUtil.IsValidUrl(item.Tag.ToString())) {
            MessageTips.Show("Mod 资源页面 URL 无效");
            return;
        }
        NetworkUtil.OpenUrl(item.Tag.ToString());
    }

    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        var listView = sender as ListView;
        if (listView == null) {
            return;
        }
        if (listView.SelectedIndex < 0) {
            return;
        }
        
        var modDownloader = listView.SelectedItem as ModDownloader;
        (sender as ListView).SelectedIndex = -1;
        if (modDownloader.File == null) {
            return;
        }
        
        DownloadFile(modDownloader.File);
    }
    
    private async void DownloadFile(DownloadFile download) {
        Console.WriteLine($"下载文件：{download}");
        SaveFileDialog sfd = new SaveFileDialog();
        sfd.Title = $"请选择保存 {download.Name} 的文件夹";
        sfd.DefaultDirectory = DirFileUtil.CurrentDirPosition;
        sfd.FileName = download.Name;
        if (sfd.ShowDialog() == true) {
            download.FilePath = Path.Combine(sfd.FileName);
            MessageTips.Show($"正在下载 {download.Name}");
            var result = await DownloadUtil.SingalDownload(download);
            if (result) {
                MessageBox.Show(
                    $"文件 {download.Name} 下载完成，已保存至({download.FilePath})",
                    "下载成功",btnType:MessageBox.BtnType.ConfirmAndCustom,
                    customBtnText:"前往文件夹",
                    callback: r => {
                        if (r == MessageBox.Result.Custom) {
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

    private void RefreshBtn_OnClick(object sender, RoutedEventArgs e) {
        GetDownloadFiles(false).ConfigureAwait(false);
    }

    private bool isFirstChangePlatform = true;
    private void Platform_OnClick(object sender, RoutedEventArgs e) {
        if (isFirstChangePlatform) {
            isFirstChangePlatform = false;
            GetDownloadFiles(false).ConfigureAwait(false);
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
}