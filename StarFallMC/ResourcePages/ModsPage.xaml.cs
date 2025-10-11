using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using StarFallMC.Component;
using StarFallMC.Entity.Resource;
using StarFallMC.ResourcePages.SubPage;
using StarFallMC.Util;
using Path = System.IO.Path;

namespace StarFallMC.ResourcePages;

public partial class ModsPage : Page {

    private ViewModel viewModel = new();

    private CancellationTokenSource cancellationTokenSource;
    public ModsPage() {
        InitializeComponent();
        DataContext = viewModel;
        cancellationTokenSource = new CancellationTokenSource();
        viewModel.PercentText = "正在加载Mod列表... 0%";
    }
    
    private Dictionary<string,int> _modIndexCache = new();
    
    private async void InitResource() {
        VirtualizingStackPanel.SetIsVirtualizing(ListView, true);
        VirtualizingStackPanel.SetVirtualizationMode(ListView, VirtualizationMode.Recycling);
        if (ResourceUtil.LocalModResources == null || ResourceUtil.LocalModResources.Count == 0) {
            ResourcePageExtension.ReloadList(MainScrollViewer,LoadingBorder,NotExist);
            var progress = new Progress<int>(percent => {
                viewModel.PercentText = $"加载Mod列表... {percent}%";
                if (percent >= 99) {
                    
                    viewModel.Mods = new ObservableCollection<ModResource>(ResourceUtil.LocalModResources ?? new List<ModResource>());
                    for (int i = 0; i < viewModel.Mods.Count; i++) {
                        _modIndexCache[viewModel.Mods[i].ModrinthSha1] = i;
                    }
                }
                if (percent == 100) {
                    ResourcePageExtension.AlreadyLoaded(this,MainScrollViewer,LoadingBorder,NotExist,ResourceUtil.LocalModResources == null || ResourceUtil.LocalModResources.Count == 0);
                    viewModel.PercentText = "加载完成";
                    MessageTips.Show($"获取到{viewModel.Mods.Count}个Mods资源");
                }
            });
            try {
                await ResourceUtil.GetModResources(cancellationTokenSource.Token,progress).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                MessageTips.Show("加载已取消");
            }
            catch (Exception e){
                Console.WriteLine(e);
            }
        }
        else {
            if (ResourceUtil.LocalModResources.Count > 50) {
                MessageTips.Show("获取的Mod数量比较多，可能会造成一小会的卡顿");
            }
            await Task.Delay(250);
            viewModel.Mods = new ObservableCollection<ModResource>(ResourceUtil.LocalModResources);
            for (int i = 0; i < viewModel.Mods.Count; i++) {
                _modIndexCache[viewModel.Mods[i].ModrinthSha1] = i;
            }
            ResourcePageExtension.AlreadyLoaded(this,MainScrollViewer,LoadingBorder,NotExist,ResourceUtil.LocalModResources == null || ResourceUtil.LocalModResources.Count == 0);
        }
    }

    
    
    public class ViewModel : INotifyPropertyChanged {

        private ObservableCollection<ModResource> _mods;

        public ObservableCollection<ModResource> Mods {
            get => _mods;
            set => SetField(ref _mods, value);
        }

        
        private string _percentText;
        
        public string PercentText {
            get => _percentText;
            set => SetField(ref _percentText, value);
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
    
    private void ModsPage_OnUnloaded(object sender, RoutedEventArgs e) {
        cancellationTokenSource.Cancel();
    }

    private void ModsPage_OnLoaded(object sender, RoutedEventArgs e) {
        InitResource();
    }

    private void ModInfo_OnClick(object sender, RoutedEventArgs e) {
        var item = (sender as TextButton)?.Tag as ModResource;
        MainWindow.SubFrameNavigate.Invoke("/ResourcePages/SubPage/ModInfo",item.DisplayName);
        Dispatcher.BeginInvoke(() => {
            ModInfo.SetResource?.Invoke(item);
        });
    }

    private void ModPosition_OnClick(object sender, RoutedEventArgs e) {
        var item = (sender as TextButton)?.Tag as ModResource;
        if (item == null) {
            return;
        }
        DirFileUtil.OpenContainingFolder(item.FilePath);
        
    }

    private void ModDisable_OnClick(object sender, RoutedEventArgs e) {
        var textButton = sender as TextButton;
        var item = textButton?.Tag as ModResource;
        if (item == null) {
            return;
        }
        
        string dirPath = Path.GetDirectoryName(item.FilePath);
        if (Path.GetFileName(dirPath) == ".disabled") {
            MessageTips.Show($"模组已禁用:{item.DisplayName}");
            return;
        }
        string disabledDirPath = Path.Combine(dirPath, ".disabled");
        if (!Directory.Exists(disabledDirPath)) {
            Directory.CreateDirectory(disabledDirPath);
        }

        string disabledFilePath = Path.Combine(disabledDirPath, item.FileName);
        try {
            File.Move(item.FilePath, disabledFilePath, true);
            item.FilePath = disabledFilePath;
            item.Disabled = true;

            MessageTips.Show($"模组已禁用:{item.DisplayName}");
            int index = -1;
            if (_modIndexCache.TryGetValue(item.ModrinthSha1, out index)) {
                if (index >= 0) {
                    ResourceUtil.LocalModResources[index] = item;
                    viewModel.Mods[index] = item;
                    Dispatcher.BeginInvoke(() => {
                        var parent = textButton.TemplatedParent as ListViewItem;
                        (parent.Template.FindName("Disabled", parent) as Border)?.RenderTransform.BeginAnimation(
                            ScaleTransform.ScaleXProperty, ResourcePageExtension.ValueTo1);
                        (parent.Template.FindName("DisabledBg", parent) as Border)?.RenderTransform.BeginAnimation(
                            ScaleTransform.ScaleXProperty, ResourcePageExtension.ValueTo1);
                    }, DispatcherPriority.Render);
                }
            }
        }
        catch (Exception exception){
            MessageTips.Show($"模组禁用失败:{item.DisplayName}");
            Console.WriteLine(exception);
        }
        
    }

    private void ModRollBack_OnClick(object sender, RoutedEventArgs e) {
        var textButton = sender as TextButton;
        var item = textButton?.Tag as ModResource;
        if (item == null) {
            return;
        }
        string dirPath = Path.GetDirectoryName(item.FilePath);
        if (Path.GetFileName(dirPath) != ".disabled") {
            MessageTips.Show($"模组已启用:{item.DisplayName}");
            return;
        }

        string enabledFilePath = Path.Combine(Path.GetDirectoryName(dirPath), item.FileName);

        if (!File.Exists(item.FilePath)) {
            MessageTips.Show($"模组已启用:{item.DisplayName}");
            return;
        }
        
        try {
            File.Move(item.FilePath, enabledFilePath, true);
            item.FilePath = enabledFilePath;
            item.Disabled = false;
            MessageTips.Show($"模组已启用:{item.DisplayName}");
            int index = ResourceUtil.LocalModResources.FindIndex(i => item.DisplayName == i.DisplayName);
            if (index >= 0) {
                ResourceUtil.LocalModResources[index] = item;
                viewModel.Mods[index] = item;
                var parent = textButton.TemplatedParent as ListViewItem;
                var border = parent.Template.FindName("Disabled", parent) as Border;
                var disabledBg = parent.Template.FindName("DisabledBg", parent) as Border;
                border.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, ResourcePageExtension.ValueTo0);
                disabledBg.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty,
                    ResourcePageExtension.ValueTo0);
            }
        }
        catch (Exception exception){
            MessageTips.Show($"模组启用失败:{item.DisplayName}");
            Console.WriteLine(exception);
        }
    }

    private void Disabled_OnLoaded(object sender, RoutedEventArgs e) {
        var item = sender as Border;
        bool disabled = false;
        if (item.Tag is bool _disabled) {
            disabled = _disabled;
        }
        ScaleTransform st = new ScaleTransform();
        if (disabled) {
            st.ScaleX = 1;
        }
        else {
            st.ScaleX = 0;
        }
        (sender as Border).RenderTransform = st;
    }
}