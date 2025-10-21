using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.ResourcePages.SubPage;
using StarFallMC.Util;

namespace StarFallMC.ResourcePages;

public partial class DownloadGame : Page {

    private ViewModel viewModel = new();
    private CancellationTokenSource cts;
    public DownloadGame() {
        InitializeComponent();
        DataContext = viewModel;
        cts = new();
    }
    
    public class ViewModel : INotifyPropertyChanged {

        private List<MinecraftDownloader> latestType;
        public List<MinecraftDownloader> LatestType {
            get => latestType;
            set => SetField(ref latestType, value);
        }
        
        private List<MinecraftDownloader> _releaseType;
        public List<MinecraftDownloader> ReleaseType {
            get => _releaseType;
            set => SetField(ref _releaseType, value);
        }
        
        private List<MinecraftDownloader> _snapshotType;
        public List<MinecraftDownloader> SnapshotType {
            get => _snapshotType;
            set => SetField(ref _snapshotType, value);
        }
        
        private List<MinecraftDownloader> _aprilFoolsType;
        public List<MinecraftDownloader> AprilFoolsType {
            get => _aprilFoolsType;
            set => SetField(ref _aprilFoolsType, value);
        }
        
        private List<MinecraftDownloader> _oldType;
        
        private string _percentText;
        public string PercentText {
            get => _percentText;
            set => SetField(ref _percentText, value);
        }

        public List<MinecraftDownloader> OldType {
            get => _oldType;
            set => SetField(ref _oldType, value);
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
    
    private async Task InitMinecraftDownloader() {
        ResourcePageExtension.ReloadList(MainScrollViewer,LoadingBorder);
        if (ResourceUtil.IsNeedInitDownloader()) {
            var progress = new Progress<int>(percent => {
                viewModel.PercentText = $"加载中... {percent}%";
                if (percent == 100) {
                    viewModel.LatestType = ResourceUtil.LatestType ?? new List<MinecraftDownloader>();
                    viewModel.ReleaseType = ResourceUtil.ReleaseType ?? new List<MinecraftDownloader>();
                    viewModel.SnapshotType = ResourceUtil.SnapshotType ?? new List<MinecraftDownloader>();
                    viewModel.AprilFoolsType = ResourceUtil.AprilFoolsType ?? new List<MinecraftDownloader>();
                    viewModel.OldType = ResourceUtil.OldType ?? new List<MinecraftDownloader>();
                    ResourcePageExtension.AlreadyLoaded(this,MainScrollViewer,LoadingBorder,null,ResourceUtil.IsNeedInitDownloader());
                    viewModel.PercentText = "加载完成";
                    MessageTips.Show($"获取Minecraft列表完成");
                }
            });
            try {
                await ResourceUtil.GetMinecraftDownloader(cts.Token,progress).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                Console.WriteLine("取消加载Minecraft列表");
                return;
            }
            catch (Exception e){
                Console.WriteLine(e);
            }
        }
        else {
            MessageTips.Show("卡顿一下~");
            await Task.Delay(250).ConfigureAwait(false);
            viewModel.LatestType = ResourceUtil.LatestType ?? new List<MinecraftDownloader>();
            viewModel.ReleaseType = ResourceUtil.ReleaseType ?? new List<MinecraftDownloader>();
            viewModel.SnapshotType = ResourceUtil.SnapshotType ?? new List<MinecraftDownloader>();
            viewModel.AprilFoolsType = ResourceUtil.AprilFoolsType ?? new List<MinecraftDownloader>();
            viewModel.OldType = ResourceUtil.OldType ?? new List<MinecraftDownloader>();
            ResourcePageExtension.AlreadyLoaded(this,MainScrollViewer,LoadingBorder,null,ResourceUtil.IsNeedInitDownloader());
        }
    }

    private void DownloadGame_OnLoaded(object sender, RoutedEventArgs e) {
        InitMinecraftDownloader();
    }
    
    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        var listView = sender as ListView;
        if (listView == null) {
            return;
        }
        if (listView.SelectedIndex < 0) {
            return;
        }
        
        var downloader = listView.SelectedItem as MinecraftDownloader;
        
        if (downloader == null) {
            return;
        }
        MainWindow.SubFrameNavigate.Invoke("/ResourcePages/SubPage/GameInfo",downloader.Name);
        Dispatcher.BeginInvoke(() => {
            GameInfo.SetMinecraftDownloader?.Invoke(downloader);
        });
        Console.WriteLine($"选择了{downloader}");
        (sender as ListView).SelectedIndex = -1;
    }

    private void DownloadGame_OnUnloaded(object sender, RoutedEventArgs e) {
        cts.Cancel();
    }

    private void RefreshBtn_OnClick(object sender, RoutedEventArgs e) {
        cts?.Cancel();
        cts = new CancellationTokenSource();
        ResourceUtil.ClearDownloader();
        InitMinecraftDownloader();
    }
}