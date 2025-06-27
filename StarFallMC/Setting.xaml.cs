using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace StarFallMC;



public partial class Setting : Page {

    private ViewModel viewModel = new ViewModel();

    private Storyboard ActiveMove;
    private Storyboard GameListChangeAnim;

    private Timer GameListChangeTimer;
    
    public Setting() {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.ActiveY = 0;
        viewModel.SelectItems = new ObservableCollection<SettingItem>() {
            new SettingItem("游戏设置","/GameSetting.xaml"),
            new SettingItem("关于","/About.xaml"),
        };
        ActiveMove = (Storyboard) FindResource("ActiveMove");
        GameListChangeAnim = (Storyboard) FindResource("GameListChangeAnim");
    }
    
    public class ViewModel : INotifyPropertyChanged {
        private ObservableCollection<SettingItem> _selectItems;
        public ObservableCollection<SettingItem> SelectItems {
            get=> _selectItems;
            set => SetField(ref _selectItems, value);
        }
        
        private int _activeY;

        public int ActiveY {
            get => _activeY;
            set => SetField(ref _activeY, value);
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
    
    public class SettingItem {
        public string Name { get; set; }
        public string Path { get; set; }

        public SettingItem(string name, string path) {
            Name = name;
            Path = path;
        }
    }

    private void SelectChoice_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        ((DoubleAnimation)ActiveMove.Children[0]).To = SelectChoice.SelectedIndex * 50;
        ActiveMove.Begin();
        GameListChangeAnim.Begin();
        var index = (SettingItem)SelectChoice.SelectedItem;
        if (GameListChangeTimer != null) {
            GameListChangeTimer.Dispose();
        }
        GameListChangeTimer = new Timer(new TimerCallback(state => {
            this.Dispatcher.BeginInvoke(() => {
                SettingFrame.Navigate(new Uri("/SettingPages" + index.Path, UriKind.Relative));
                GameListChangeTimer.Dispose();
            });
        }), null, 300, 0);
        
    }
}