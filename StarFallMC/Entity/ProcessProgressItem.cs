using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using StarFallMC.Entity.Enum;

namespace StarFallMC.Entity;

public class ProcessProgressItem : INotifyPropertyChanged {
    private string _progressName;
    public string ProgressName {
        get => _progressName;
        set => SetField(ref _progressName, value);
    }
    
    private ProcessStatus _status;
    public ProcessStatus Status {
        get => _status;
        set {
            if (SetField(ref _status, value)) {
                OnPropertyChanged(nameof(DoingVisibility));
                OnPropertyChanged(nameof(CompleteVisibility));
                OnPropertyChanged(nameof(ErrorVisibility));
                OnPropertyChanged(nameof(WaitVisibility));
            }
        }
    }

    public Visibility DoingVisibility => Status == ProcessStatus.Doing ? Visibility.Visible : Visibility.Collapsed;
    public Visibility CompleteVisibility => Status == ProcessStatus.Complete ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ErrorVisibility => Status == ProcessStatus.Error ? Visibility.Visible : Visibility.Collapsed;
    public Visibility WaitVisibility => Status == ProcessStatus.Wait ? Visibility.Visible : Visibility.Collapsed;
    
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