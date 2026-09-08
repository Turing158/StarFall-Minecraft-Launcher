using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using fNbt;
using StarFallMC.Component;
using StarFallMC.Entity.Resource;
using StarFallMC.Util;
using StarFallMC.Navigation;
using Button = StarFallMC.Component.Button;

namespace StarFallMC.ResourcePages.SubPage;

public partial class SaveInfo : Page, IPageLifecycle {
    private ViewModel viewModel = new();
    
    public SaveInfo(SavesResource? resource = null) {
        InitializeComponent();
        DataContext = viewModel;
        if (resource != null) {
            setResource(resource);
        }
    }
    
    public class ViewModel : INotifyPropertyChanged {
        
        private SaveResourceInfo? _resource;
        
        public SaveResourceInfo? Resource {
            get => _resource;
            set => SetField(ref _resource, value);
        }
        
        private List<GameRule> _gameRules = new();
        public List<GameRule> GameRules {
            get => _gameRules;
            set => SetField(ref _gameRules, value);
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
    
    public class GameRule {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
    
    private void setResource(SavesResource resource) {
        Dispatcher.BeginInvoke(() => {
            var saveResource = SaveResourceInfo.FromSavesResource(resource);
            viewModel.Resource = saveResource;
            
            NbtCompound? nbt = saveResource.GameRuleTag;
            if (nbt != null) {
                List<GameRule> gameRules = new();
                foreach (var i in nbt) {
                    gameRules.Add(new GameRule {
                        Name = i.Name ?? string.Empty,
                        Value = i.StringValue ?? string.Empty
                    });
                }
                gameRules.Sort((a,b) => a.Name.CompareTo(b.Name));
                viewModel.GameRules = gameRules;
            }
        });
    }

    private void GoToExplorePage_OnClick(object sender, RoutedEventArgs e) {
        var item = sender as Button;
        if (item?.Tag == null) {
            return;
        }

        var path = item.Tag as string;
        if (path != null) {
            DirFileUtil.OpenContainingFolder(path);
        }
    }

    private void SeedCopy_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        if (viewModel.Resource is { } resource) {
            Clipboard.SetText(resource.Seed.ToString());
            MessageTips.Show("种子已复制至剪切板");
        }
    }

    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        var listView = sender as ListView;
        if (listView == null) {
            return;
        }
        if (listView.SelectedIndex < 0) {
            return;
        }
        
        if (listView.SelectedItem is not GameRule gameRule) {
            listView.SelectedIndex = -1;
            return;
        }
        listView.SelectedIndex = -1;
        
        Clipboard.SetText($"/gamerule {gameRule.Name} {gameRule.Value}");
        MessageTips.Show("游戏规则已复制至剪切板");
    }

    public Task ActivateAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task DeactivateAsync() => Task.CompletedTask;
}
