using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.ResourcePages.SubPage;
using StarFallMC.Util;
using MessageBox = StarFallMC.Component.MessageBox;

namespace StarFallMC.ResourcePages;



public partial class SavesPage : Page {
    private ViewModel viewModel = new();
    
    private CancellationTokenSource cancellationTokenSource;
    public SavesPage() {
        InitializeComponent();
        DataContext = viewModel;
        cancellationTokenSource = new CancellationTokenSource();
        viewModel.PercentText = "0%";
    }
    
    private async void InitResource() {
        VirtualizingStackPanel.SetIsVirtualizing(ListView, true);
        VirtualizingStackPanel.SetVirtualizationMode(ListView, VirtualizationMode.Recycling);
        if (ResourceUtil.LocalSavesResources == null || ResourceUtil.LocalSavesResources.Count == 0) {
            ResourcePageExtension.ReloadModList(MainScrollViewer,LoadingBorder,NotExist);
            var progress = new Progress<int>(percent => {
                viewModel.PercentText = $"加载中... {percent}%";
                if (percent >= 99) {
                    viewModel.Saves = new ObservableCollection<SavesResource>(ResourceUtil.LocalSavesResources ?? new List<SavesResource>());
                }
                if (percent == 100) {
                    ResourcePageExtension.AlreadyModLoaded(this,MainScrollViewer,LoadingBorder,NotExist,ResourceUtil.LocalSavesResources == null || ResourceUtil.LocalSavesResources.Count == 0);
                    viewModel.PercentText = "加载完成";
                    MessageTips.Show($"获取到{viewModel.Saves.Count}个地图文件");
                }
            });
            try {
                await ResourceUtil.GetSavesResource(cancellationTokenSource.Token,progress).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                MessageTips.Show("加载已取消");
            }
            catch (Exception e){
                Console.WriteLine(e);
            }
        }
        else {
            viewModel.Saves = new ObservableCollection<SavesResource>(ResourceUtil.LocalSavesResources);
            ResourcePageExtension.AlreadyModLoaded(this,MainScrollViewer,LoadingBorder,NotExist,ResourceUtil.LocalSavesResources == null || ResourceUtil.LocalSavesResources.Count == 0);
        }
    }
    
    public class ViewModel : INotifyPropertyChanged {
        
        private string _percentText = "0%";
        
        public string PercentText { 
            get => _percentText;
            set => SetField(ref _percentText, value);
        }
        
        private ObservableCollection<SavesResource> _saves = new();

        public ObservableCollection<SavesResource> Saves {
            get => _saves;
            set => SetField(ref _saves, value);
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
    

    private void SaveInfo_OnClick(object sender, RoutedEventArgs e) {
        var item = (sender as TextButton)?.Tag as SavesResource;
        MainWindow.SubFrameNavigate.Invoke("/ResourcePages/SubPage/SaveInfo",item.WorldName);
        Dispatcher.BeginInvoke(() => {
            SaveInfo.SetResource?.Invoke(item);
        });
    }

    private void SavePosition_OnClick(object sender, RoutedEventArgs e) {
        var item = sender as TextButton;
        if (item?.Tag == null) {
            return;
        }

        var path = item.Tag as string;
        if (path != null) {
            DirFileUtil.OpenContainingFolder(path);
        }
    }

    private void SavesDelete_OnClick(object sender, RoutedEventArgs e) {
        var item = sender as TextButton;
        if (item?.Tag == null) {
            return;
        }
        var resource = item.Tag as SavesResource;
        if (resource == null) {
           return;
        }
        MessageBox.Show($"确定删除下面的地图文件吗？它会永久消失不见喔\n\n {resource.WorldName} ({resource.DirName}) ","删除提示",MessageBox.BtnType.ConfirmAndCancel,
            r => {
                if (r == MessageBox.Result.Confirm) {
                    viewModel.Saves.Remove(resource);
                    ResourceUtil.LocalSavesResources?.Remove(resource);
                    Directory.Delete(resource.Path,true);
                    MessageTips.Show($"已删除地图文件 {resource.WorldName} ({resource.DirName})");
                }
            });
    }

    private void SavesPage_OnLoaded(object sender, RoutedEventArgs e) {
        InitResource();
    }

    private void SavesPage_OnUnloaded(object sender, RoutedEventArgs e) {
        cancellationTokenSource?.Cancel();
    }

    private void SaveCopy_OnClick(object sender, RoutedEventArgs e) {
        var item = sender as TextButton;
        if (item?.Tag == null) {
            return;
        }
        var resource = item.Tag as SavesResource;
        if (resource == null) {
            return;
        }
        
        MessageTips.Show($"正在备份地图文件 {resource.WorldName} ({resource.DirName})");
        var copyResource = new SavesResource(resource.nbt,resource.DirName,resource.Path,resource.RefreshDate);
        var copyDirPath = Path.GetDirectoryName(resource.Path);
        var copyPath = Path.Combine(copyDirPath,$"{resource.DirName}_备份");
        if (Directory.Exists(copyPath)) {
            MessageTips.Show($"备份失败，目标目录已存在 {copyPath}");
            return;
        }
        copyResource.Path = copyPath;
        copyResource.DirName = $"{resource.DirName}_备份";
        DirFileUtil.CopyDirAndFiles(resource.Path,copyPath);
        int index = ResourceUtil.LocalSavesResources.FindIndex(r => r.Path == resource.Path);
        if (index >= 0) {
            ResourceUtil.LocalSavesResources.Add(copyResource);
            viewModel.Saves.Add(copyResource);
        }
    }
}