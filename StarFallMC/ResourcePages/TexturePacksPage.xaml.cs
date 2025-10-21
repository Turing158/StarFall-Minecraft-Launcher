using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using StarFallMC.Component;
using StarFallMC.Entity.Resource;
using StarFallMC.Util;
using MessageBox = StarFallMC.Component.MessageBox;

namespace StarFallMC.ResourcePages;

public partial class TexturePacksPage : Page {

    private ViewModel viewModel = new();
    private CancellationTokenSource cancellationTokenSource;
    public TexturePacksPage() {
        InitializeComponent();
        DataContext = viewModel;
        cancellationTokenSource = new();
    }
    
    private async void InitResource() {
        VirtualizingStackPanel.SetIsVirtualizing(ListView, true);
        VirtualizingStackPanel.SetVirtualizationMode(ListView, VirtualizationMode.Recycling);
        if (ResourceUtil.LocalTexturePackResources == null || ResourceUtil.LocalTexturePackResources.Count == 0) {
            ResourcePageExtension.ReloadList(MainScrollViewer,LoadingBorder,NotExist);
            var progress = new Progress<int>(percent => {
                viewModel.PercentText = $"加载中... {percent}%";
                if (percent >= 99) {
                    
                    viewModel.TexturePacks = new ObservableCollection<TexturePackResource>(ResourceUtil.LocalTexturePackResources ?? new List<TexturePackResource>());
                    
                }
                if (percent == 100) {
                    ResourcePageExtension.AlreadyLoaded(this,MainScrollViewer,LoadingBorder,NotExist,ResourceUtil.LocalTexturePackResources == null || ResourceUtil.LocalTexturePackResources.Count == 0);
                    viewModel.PercentText = "加载完成";
                    MessageTips.Show($"获取到{viewModel.TexturePacks.Count}个材质包文件");
                }
            });
            try {
                await ResourceUtil.GetTexturePack(cancellationTokenSource.Token,progress).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                Console.WriteLine("取消加载材质包文件");
                return;
            }
            catch (Exception e){
                Console.WriteLine(e);
            }
        }
        else {
            viewModel.TexturePacks = new ObservableCollection<TexturePackResource>(ResourceUtil.LocalTexturePackResources);
            ResourcePageExtension.AlreadyLoaded(this,MainScrollViewer,LoadingBorder,NotExist,ResourceUtil.LocalTexturePackResources == null || ResourceUtil.LocalTexturePackResources.Count == 0);
        }
    }
    
    public class ViewModel : INotifyPropertyChanged {
        
        private string _percentText = "0%";
        
        public string PercentText { 
            get => _percentText;
            set => SetField(ref _percentText, value);
        }
        
        private ObservableCollection<TexturePackResource> _texturePacks = new();

        public ObservableCollection<TexturePackResource> TexturePacks{
            get => _texturePacks;
            set => SetField(ref _texturePacks, value);
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

    private void PackPosition_OnClick(object sender, RoutedEventArgs e) {
        var item = sender as TextButton;
        if (item == null) return;
        var path = item.Tag as string;
        if (string.IsNullOrEmpty(path)) {
            MessageTips.Show("路径无效");
            return;
        }
        DirFileUtil.OpenContainingFolder(path);
    }

    private void PackDelete_OnClick(object sender, RoutedEventArgs e) {
        var item = (sender as TextButton).Tag as TexturePackResource;
        if (item == null) return;
        MessageBox.Show($"确定删除材质包 {item.Name} 吗？", "确认删除", MessageBox.BtnType.ConfirmAndCancel, (result) => {
            if (result == MessageBox.Result.Confirm) {
                ResourceUtil.LocalTexturePackResources.Remove(item);
                viewModel.TexturePacks.Remove(item);
                File.Delete(item.Path);
                MessageTips.Show($"删除材质包 {item.Name} 成功");
            }
        });
    }
    
    private void TexturePacksPage_OnLoaded(object sender, RoutedEventArgs e) {
        InitResource();
    }

    private void TexturePacksPage_OnUnloaded(object sender, RoutedEventArgs e) {
        cancellationTokenSource?.Cancel();
    }

    private void RefreshBtn_OnClick(object sender, RoutedEventArgs e) {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource = new CancellationTokenSource();
        ResourceUtil.LocalTexturePackResources?.Clear();
        InitResource();
    }
}