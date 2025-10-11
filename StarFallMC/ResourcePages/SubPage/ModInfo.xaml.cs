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
    
    public ModInfo() {
        InitializeComponent();
        DataContext = viewModel;
        SetResource = setResource;
        
    }
    
    private void setResource(ModResource resource) {
        Dispatcher.BeginInvoke(() => {
            viewModel.Resource = resource;
            if (resource.DownloadFiles == null || resource.DownloadFiles.Count == 0) {
                NoDownloadSource.Visibility = Visibility.Visible;
            }
            else {
                NoDownloadSource.Visibility = Visibility.Collapsed;
            }
        });
    }

    public class ViewModel : INotifyPropertyChanged {
        
        private ModResource _resource;
        
        public ModResource Resource {
            get => _resource;
            set => SetField(ref _resource, value);
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
        
        var downloadFile = listView.SelectedItem as DownloadFile;
        (sender as ListView).SelectedIndex = -1;
        if (downloadFile == null) {
            return;
        }
        
        DownloadFile(downloadFile);
    }
    
    private async void DownloadFile(DownloadFile download) {
        Console.WriteLine($"下载文件：{download}");
        OpenFolderDialog ofd = new OpenFolderDialog();
        ofd.Title = $"请选择保存 {download.Name} 的文件夹";
        ofd.DefaultDirectory = DirFileUtil.CurrentDirPosition;
        if (ofd.ShowDialog() == true) {
            download.FilePath = Path.Combine(ofd.FolderName, download.Name);
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
}