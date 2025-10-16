using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using StarFallMC.Util.Extension;

namespace StarFallMC.Component;

public partial class Pagination : UserControl {
    
    public double IconSize{
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }
    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(Pagination), new PropertyMetadata(25.0));
    
    public double ItemFontSize{
        get => (double)GetValue(ItemFontSizeProperty);
        set => SetValue(ItemFontSizeProperty, value);
    }
    
    public static readonly DependencyProperty ItemFontSizeProperty =
        DependencyProperty.Register(nameof(ItemFontSize), typeof(double), typeof(Pagination), new PropertyMetadata(14.0));
    
    public int TotalCount
    {
        get => (int)GetValue(TotalCountProperty);
        set => SetValue(TotalCountProperty, value);
    }

    public static readonly DependencyProperty TotalCountProperty =
        DependencyProperty.Register(nameof(TotalCount), typeof(int), typeof(Pagination), 
            new PropertyMetadata(0, OnTotalCountChanged));

    private static void OnTotalCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        var pagination = d as Pagination;
        if (pagination != null) {
            pagination.InitPages();
        }
    }

    public int PageSize {
        get => (int)GetValue(PageSizeProperty);
        set => SetValue(PageSizeProperty, value);
    }
    public static readonly DependencyProperty PageSizeProperty =
        DependencyProperty.Register(nameof(PageSize), typeof(int), typeof(Pagination), new PropertyMetadata(20));
    
    public int CurrentPage {
        get => (int)GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }
    public static readonly DependencyProperty CurrentPageProperty =
        DependencyProperty.Register(nameof(CurrentPage), typeof(int), typeof(Pagination), new PropertyMetadata(1));
    
    public int PageCount {
        get => (int)GetValue(PageCountProperty);
    }
    private static readonly DependencyProperty PageCountProperty =
        DependencyProperty.Register(nameof(PageCount), typeof(int), typeof(Pagination), new PropertyMetadata(0));
    
    public int MaxShowItemNum {
        get => (int)GetValue(MaxShowItemNumProperty);
        set => SetValue(MaxShowItemNumProperty, value);
    }
    public static readonly DependencyProperty MaxShowItemNumProperty =
        DependencyProperty.Register(nameof(MaxShowItemNum), typeof(int), typeof(Pagination), new PropertyMetadata(5));
    
    public Thickness ItemMargin {
        get => (Thickness)GetValue(ItemMarginProperty);
        set => SetValue(ItemMarginProperty, value);
    }
    public static readonly DependencyProperty ItemMarginProperty =
        DependencyProperty.Register(nameof(ItemMargin), typeof(Thickness), typeof(Pagination), new PropertyMetadata(new Thickness(5,0,5,0)));
    
    public Visibility InfoVisibility {
        get => (Visibility)GetValue(InfoVisibilityProperty);
        set => SetValue(InfoVisibilityProperty, value);
    }
    public static readonly DependencyProperty InfoVisibilityProperty =
        DependencyProperty.Register(nameof(InfoVisibility), typeof(Visibility), typeof(Pagination), new PropertyMetadata(Visibility.Visible));
    
    public Visibility LeftAndRightButtonVisibility {
        get => (Visibility)GetValue(LeftAndRightButtonVisibilityProperty);
        set => SetValue(LeftAndRightButtonVisibilityProperty, value);
    }
    public static readonly DependencyProperty LeftAndRightButtonVisibilityProperty =
        DependencyProperty.Register(nameof(LeftAndRightButtonVisibility), typeof(Visibility), typeof(Pagination), new PropertyMetadata(Visibility.Visible));
    
    public Visibility HomeAndEndButtonVisibility {
        get => (Visibility)GetValue(HomeAndEndButtonVisibilityProperty);
        set => SetValue(HomeAndEndButtonVisibilityProperty, value);
    }
    public static readonly DependencyProperty HomeAndEndButtonVisibilityProperty =
        DependencyProperty.Register(nameof(HomeAndEndButtonVisibility), typeof(Visibility), typeof(Pagination), new PropertyMetadata(Visibility.Collapsed));
    
    public Visibility GoToButtonVisibility {
        get => (Visibility)GetValue(GoToButtonVisibilityProperty);
        set => SetValue(GoToButtonVisibilityProperty, value);
    }
    public static readonly DependencyProperty GoToButtonVisibilityProperty =
        DependencyProperty.Register(nameof(GoToButtonVisibility), typeof(Visibility), typeof(Pagination), new PropertyMetadata(Visibility.Collapsed));
    
    public event SelectionChangedEventHandler PageChanged {
        add { AddHandler(PageChangedEvent, value); }
        remove { RemoveHandler(PageChangedEvent, value); }
    }
    public static readonly RoutedEvent PageChangedEvent =
        EventManager.RegisterRoutedEvent(nameof(PageChanged), RoutingStrategy.Bubble, typeof(SelectionChangedEventHandler), typeof(Pagination));
    
    
    private ViewModel viewModel = new();
    
    public Pagination() {
        InitializeComponent();
        DataContext = viewModel;
        InitPages();
    }

    public void InitPages() {
        Panel.Visibility = TotalCount == 0 ? Visibility.Collapsed : Visibility.Visible;
        SetValue(PageCountProperty, (int)Math.Ceiling((double)TotalCount / PageSize));
        List<int> pages = new List<int>();
        for (int i = 0; i < PageCount; i++) {
            pages.Add(i + 1);
        }
        viewModel.Pages = pages;
        viewModel.ListHeight = Height + 2;
        viewModel.ActiveHeight = Height / 8.0;
        Dispatcher.BeginInvoke(() => {
            var itemWidth = Height + ItemMargin.Left + ItemMargin.Right;
            viewModel.MaxWidth = itemWidth * MaxShowItemNum;
        });
    }
    
    public class ViewModel : INotifyPropertyChanged {
        
        private List<int> _pages = new();
        public List<int> Pages {
            get => _pages;
            set => SetField(ref _pages, value);
        }
        
        private double _maxWidth;
        public double MaxWidth {
            get => _maxWidth;
            set => SetField(ref _maxWidth, value);
        }
        
        private double _listHeight;
        public double ListHeight {
            get => _listHeight;
            set => SetField(ref _listHeight, value);
        }

        private double _activeHeight = 5;
        public double ActiveHeight {
            get => _activeHeight;
            set => SetField(ref _activeHeight, value);
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


    private void Pagination_OnLoaded(object sender, RoutedEventArgs e) {
        InitPages();
    }

    private void Nums_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        var item = Nums.ItemContainerGenerator.ContainerFromIndex(Nums.SelectedIndex) as ListViewItem;
        if (item != null) {
            Point itemPoint = item.TranslatePoint(new Point(0, 0), Nums);
            if (itemPoint.X > viewModel.MaxWidth/2) {
                ScrollViewerExtensions.AnimateScroll(ScrollViewer, itemPoint.X + item.ActualWidth/2 - viewModel.MaxWidth/2, true);
            }
            else {
                ScrollViewerExtensions.AnimateScroll(ScrollViewer,1, true);
            }
        }
        SelectionChangedEventArgs args = new SelectionChangedEventArgs(PageChangedEvent, e.RemovedItems, e.AddedItems);
        args.Source = this;
        RaiseEvent(args);
    }
    
    private void Left_OnClick(object sender, RoutedEventArgs e) {
        if (CurrentPage - 1 <= 0) {
            CurrentPage = 1;
            return;
        }

        CurrentPage--;
    }

    private void Right_OnClick(object sender, RoutedEventArgs e) {
        if (CurrentPage + 1 >= PageCount) {
            CurrentPage = PageCount;
            return;
        }
        CurrentPage++;
    }

    private void Home_OnClick(object sender, RoutedEventArgs e) {
        CurrentPage = 1;
    }

    private void End_OnClick(object sender, RoutedEventArgs e) {
        CurrentPage = PageCount;
    }
}