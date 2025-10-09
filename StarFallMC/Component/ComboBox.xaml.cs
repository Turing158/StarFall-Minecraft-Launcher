using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace StarFallMC.Component;

public partial class ComboBox : UserControl {

    public enum SelectedType {
        close,
        SelectNewToClose,
        neverClose
    }
    
    public SelectedType SelectedAction{
        get => (SelectedType)GetValue(SelectedActionProperty);
        set => SetValue(SelectedActionProperty, value);
    }
    public static readonly DependencyProperty SelectedActionProperty = DependencyProperty.Register(
        nameof(SelectedAction), typeof(SelectedType), typeof(ComboBox), new PropertyMetadata(SelectedType.close));

    public string NoSelectText {
        get => (string)GetValue(NoSelectTextProperty);
        set => SetValue(NoSelectTextProperty, value);
    }
    public static readonly DependencyProperty NoSelectTextProperty = DependencyProperty.Register(
        nameof(NoSelectText), typeof(string), typeof(ComboBox), new PropertyMetadata("未选择选项"));
    
    public DataTemplate NoSelectTemplate {
        get => (DataTemplate)GetValue(NoSelectTemplateProperty);
        set => SetValue(NoSelectTemplateProperty, value);
    }
    public static readonly DependencyProperty NoSelectTemplateProperty = DependencyProperty.Register(
        nameof(NoSelectTemplate), typeof(DataTemplate), typeof(ComboBox), new PropertyMetadata(null));
    
    public DataTemplate CurrentChoiceTemplate {
        get => (DataTemplate)GetValue(CurrentChoiceTemplateProperty);
        set => SetValue(CurrentChoiceTemplateProperty, value);
    }
    public static readonly DependencyProperty CurrentChoiceTemplateProperty = DependencyProperty.Register(
        nameof(CurrentChoiceTemplate), typeof(DataTemplate), typeof(ComboBox), new PropertyMetadata(null));
    
    public DataTemplate ChoiceTemplate {
        get => (DataTemplate)GetValue(ChoiceTemplateProperty);
        set => SetValue(ChoiceTemplateProperty, value);
    }
    public static readonly DependencyProperty ChoiceTemplateProperty = DependencyProperty.Register(
        nameof(ChoiceTemplate), typeof(DataTemplate), typeof(ComboBox), new PropertyMetadata(null));

    public Object ItemsSource {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(Object), typeof(ComboBox), new PropertyMetadata(default(Object)));

    public Object CurrentItem {
        get => GetValue(CurrentItemProperty);
        set => SetValue(CurrentItemProperty, value);
    }
    public static readonly DependencyProperty CurrentItemProperty = DependencyProperty.Register(
        nameof(CurrentItem), typeof(Object), typeof(ComboBox), new PropertyMetadata(default(Object)));
    
    public Visibility DeleteButtonVisibility {
        get => (Visibility)GetValue(DeleteButtonVisibilityProperty);
        set => SetValue(DeleteButtonVisibilityProperty, value);
    }
    public static readonly DependencyProperty DeleteButtonVisibilityProperty = DependencyProperty.Register(
        nameof(DeleteButtonVisibility), typeof(Visibility), typeof(ComboBox), new PropertyMetadata(Visibility.Visible));
    
    public Visibility ClearButtonVisibility {
        get => (Visibility)GetValue(ClearButtonVisibilityProperty);
        set => SetValue(ClearButtonVisibilityProperty, value);
    }
    public static readonly DependencyProperty ClearButtonVisibilityProperty = DependencyProperty.Register(
        nameof(ClearButtonVisibility), typeof(Visibility), typeof(ComboBox), new PropertyMetadata(Visibility.Visible));
    
    public int DefaultIndex {
        get => (int)GetValue(DefaultIndexProperty);
        set => SetValue(DefaultIndexProperty, value);
    }
    public static readonly DependencyProperty DefaultIndexProperty = DependencyProperty.Register(
        nameof(DefaultIndex), typeof(int), typeof(ComboBox), new PropertyMetadata(-1));
    
    public int SelectedIndex {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }
    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex), typeof(int), typeof(ComboBox), new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double MaxDropDownHeight {
        get => (double)GetValue(MaxDropDownHeightProperty);
        set => SetValue(MaxDropDownHeightProperty, value);
    }
    public static readonly DependencyProperty MaxDropDownHeightProperty = DependencyProperty.Register(
        nameof(MaxDropDownHeight), typeof(double), typeof(ComboBox), new PropertyMetadata(double.PositiveInfinity));
    
    public bool NeedConfirmMessageBox {
        get => (bool)GetValue(NeedConfirmMessageBoxProperty);
        set => SetValue(NeedConfirmMessageBoxProperty, value);
    }
    public static readonly DependencyProperty NeedConfirmMessageBoxProperty = DependencyProperty.Register(
        nameof(NeedConfirmMessageBox), typeof(bool), typeof(ComboBox), new PropertyMetadata(false));
    
    public string MessageBoxTitle {
        get => (string)GetValue(MessageBoxTitleProperty);
        set => SetValue(MessageBoxTitleProperty, value);
    }
    public static readonly DependencyProperty MessageBoxTitleProperty = DependencyProperty.Register(
        nameof(MessageBoxTitle), typeof(string), typeof(ComboBox), new PropertyMetadata("确认删除"));
    
    public string MessageBoxContent {
        get => (string)GetValue(MessageBoxContentProperty);
        set => SetValue(MessageBoxContentProperty, value);
    }
    public static readonly DependencyProperty MessageBoxContentProperty = DependencyProperty.Register(
        nameof(MessageBoxContent), typeof(string), typeof(ComboBox), new PropertyMetadata("确认删除该选项？"));
    
    public int ForbiddenDeleteFirstLengthChoice {
        get => (int)GetValue(ForbiddenDeleteFirstLengthChoiceProperty);
        set => SetValue(ForbiddenDeleteFirstLengthChoiceProperty, value);
    }
    public static readonly DependencyProperty ForbiddenDeleteFirstLengthChoiceProperty = DependencyProperty.Register(
        nameof(ForbiddenDeleteFirstLengthChoice), typeof(int), typeof(ComboBox), new PropertyMetadata(0));

    public bool IsAnimatedOnSelection {
        get => (bool)GetValue(IsAnimatedOnSelectionProperty);
        set => SetValue(IsAnimatedOnSelectionProperty, value);
    }
    public static readonly DependencyProperty IsAnimatedOnSelectionProperty = DependencyProperty.Register(
        nameof(IsAnimatedOnSelection), typeof(bool), typeof(ComboBox), new PropertyMetadata(true));
    
    public event SelectionChangedEventHandler SelectionChanged {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }
    public static readonly RoutedEvent SelectionChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(SelectionChanged), RoutingStrategy.Bubble, typeof(SelectionChangedEventHandler), typeof(ComboBox));
    
    private DoubleAnimation ShowAnim;
    private DoubleAnimation HideAnim;
    
    public ComboBox() {
        InitializeComponent();
        if (ChoiceTemplate == null) {
            ChoiceTemplate = (DataTemplate)FindResource("DefaultChoice");
        }
        if (CurrentChoiceTemplate == null) {
            CurrentChoiceTemplate = (DataTemplate)FindResource("DefaultCurrentChoice");
        }
        if (NoSelectTemplate == null) {
            NoSelectTemplate = (DataTemplate)FindResource("DefaultNoSelect");
        }
    }

    public override void OnApplyTemplate() {
        base.OnApplyTemplate();
        ShowAnim = new DoubleAnimation() {
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new CubicEase()
        };
        HideAnim = new DoubleAnimation() {
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new CubicEase()
        };
    }
    
    private void ListView_OnLoaded(object sender, RoutedEventArgs e) {
        AdjustListView();
    }
    
    public void AdjustListView() {
        if (ItemsSource == null || (ItemsSource as IList).Count == 0) {
            NoChoice.Visibility = Visibility.Visible;
        }
        else {
            NoChoice.Visibility = Visibility.Collapsed;
        }
        SelectedIndexChange();
    }
    
    private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        SelectedIndexChange();
        RaiseEvent(new SelectionChangedEventArgs(SelectionChangedEvent, e.RemovedItems, e.AddedItems));
    }

    private void SelectedIndexChange() {
        if (SelectedIndex == -1) {
            NoSelect.BeginAnimation(OpacityProperty,ShowAnim);
            ClearButton.Visibility = Visibility.Collapsed;
            if (DefaultIndex != -1) {
                SelectedIndex = DefaultIndex;
            }
        } else {
            NoSelect.BeginAnimation(OpacityProperty,HideAnim);
            ClearButton.Visibility = Visibility.Visible;
        }
        if (SelectedAction == SelectedType.SelectNewToClose) {
            ComboBoxPanel.Hide();
        }
    }

    private void ItemMouseLeftButtonUp_OnHandler(object sender, MouseButtonEventArgs e) {
        if (SelectedAction == SelectedType.close) {
            ComboBoxPanel.Hide();
        }
    }

    private void DeleteItem_OnClick(object sender, RoutedEventArgs e) {
        if (NeedConfirmMessageBox) {
            MessageBox.Show(MessageBoxContent, 
                MessageBoxTitle,
                MessageBox.BtnType.ConfirmAndCancel,
                callback: r => {
                if (r == MessageBox.Result.Confirm) {
                    DeleteItem(sender);
                } });
        }
        else {
            DeleteItem(sender);
        }
    }

    private void DeleteItem(object sender) {
        var item = ((sender as TextButton).TemplatedParent as ListViewItem).Content;
        (ItemsSource as IList).Remove(item);
        AdjustSelectIndex();
        AdjustListView();
    }

    private void ClearCurrentChoice_OnClick(object sender, RoutedEventArgs e) {
        AdjustSelectIndex();
    }

    private void AdjustSelectIndex() {
        if (DefaultIndex != -1 && (ItemsSource as IList).Count > DefaultIndex) {
            SelectedIndex = DefaultIndex;
        }
        else {
            SelectedIndex = -1;
        }
    }

    public void Show() {
        ComboBoxPanel.Show();
    }
    
    public void Hide() {
        ComboBoxPanel.Hide();
    }
}