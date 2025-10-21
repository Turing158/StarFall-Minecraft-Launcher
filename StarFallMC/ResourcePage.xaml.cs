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

    public static Action ChangeVersionAction;
    public static Action LoadTempPage;
    
    private string tempPagePath = "ResourcePages/TexturePacksPage.xaml";
    
    public ResourcePage() {
        InitializeComponent();
        DataContext = viewModel;
        NaviBarChangeAnim = (Storyboard)FindResource("NaviBarChangeAnim");
        ChangeVersionAction = changeVersionAction;
        LoadTempPage = loadTempPage;
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
            new ("社区资源","CommunityResource",
                0 ,new ObservableCollection<NavigationItem>() {
                    new ("Mod","ModResources"),
                }),
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
                NavigetePage($"/ResourcePages/{path}.xaml");
                NaviBarChangeTimer.Dispose();
            });
        }, null, 300, 0);
    }

    private bool isFirst = true;
    private void NavigetePage(string path) {
        if (isFirst) {
            isFirst = false;
            return;
        }
        if (!string.IsNullOrEmpty(path)) {
            PageFrame.Navigate(new Uri(path, UriKind.Relative));
        }
    }
    private string[] needReloadPage = {"ModsPage","SavesPage","TexturePacksPage"};
    private void changeVersionAction() {
        if (!PageFrame.Source.ToString().Contains("Blank.xaml")) {
            if (needReloadPage.Contains(PageFrame.Source.ToString().Replace("ResourcePages/", "").Replace(".xaml", ""))) {
                Console.WriteLine("切换版本，清空页面，需要重新加载");
                tempPagePath = PageFrame.Source.ToString();
                NavigetePage("Blank.xaml");
            }
        }
    }
    
    private void loadTempPage() {
        if (!string.IsNullOrEmpty(tempPagePath)) {
            Console.WriteLine("存在切换版本，加载临时页面");
            NavigetePage(tempPagePath);
            tempPagePath = "";
        }
    }
}