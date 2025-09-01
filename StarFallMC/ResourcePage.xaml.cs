using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using StarFallMC.Entity;


namespace StarFallMC;



public partial class ResourcePage : Page {

    private ViewModel viewModel = new ViewModel();
    
    private Storyboard NaviBarChangeAnim;

    private Timer NaviBarChangeTimer;
    
    public ResourcePage() {
        InitializeComponent();
        DataContext = viewModel;
        NaviBarChangeAnim = (Storyboard)FindResource("NaviBarChangeAnim");
    }
    
    public class ViewModel : INotifyPropertyChanged {
        
        private ObservableCollection<NavigationItem> _navi = new () {
            new ("\ue7f9",20,
                0,new ObservableCollection<NavigationItem> {
                    new ("材质包","TexturePacksPage"),
                    new ("地图","SavesPage"),
                    new ("模组","ModsPage"),
                }),
            new ("Minecraft","DownloadGame"),
            new ("社区资源","CommunityResource"),
        };
        public ObservableCollection<NavigationItem> Navi {
            get=> _navi;
            set => SetField(ref _navi, value);
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
    private int test;
    private void NaviBar_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        NaviBarChangeAnim.Begin();
        NaviBarChangeTimer?.Dispose();
        NaviBarChangeTimer = new Timer(o => {
            this.Dispatcher.BeginInvoke(() => {
                var item = ResourceBar.CurrentItem;
                string path = item.Path;
                if (item.Children != null) {
                    path = (item.Children[item.ChildrenIndex] as NavigationItem).Path;
                }
                if (!string.IsNullOrEmpty(path)) {
                    PageFrame.Navigate(new Uri($"/ResourcePages/{path}.xaml", UriKind.Relative));
                }
                NaviBarChangeTimer.Dispose();
            });
        }, null, 300, 0);
    }
}