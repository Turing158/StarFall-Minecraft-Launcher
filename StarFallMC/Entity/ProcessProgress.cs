using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StarFallMC.Entity;

public class ProcessProgress : INotifyPropertyChanged {
    private string _progressKey;
    public string ProgressKey {
        get => _progressKey;
        set => SetField(ref _progressKey, value);
    }

    private string _progressIcon = "\ue7f9";
    public string ProgressIcon {
        get => _progressIcon;
        set => SetField(ref _progressIcon, value);
    }
    
    private string _progressName;
    public string ProgressName {
        get => _progressName;
        set => SetField(ref _progressName, value);
    }
    
    private int _currentStep = -1;
    public int CurrentStep {
        get => _currentStep;
        set => SetField(ref _currentStep, value);
    }
    
    private ObservableCollection<ProcessProgressItem> _progresses = new ();
    public ObservableCollection<ProcessProgressItem> Progesses {
        get => _progresses;
        set => SetField(ref _progresses, value);
    }
    
    private bool _isComplete;
    public bool IsComplete {
        get => _isComplete;
        set => SetField(ref _isComplete, value);
    }
    
    private Action<ProcessProgress> _onComplete = pp => { };
    public Action<ProcessProgress> OnComplete {
        get => _onComplete;
        set => SetField(ref _onComplete, value);
    }
    
    private Action<ProcessProgress>  _onDelete = pp => { };
    public Action<ProcessProgress> OnDelete {
        get => _onDelete;
        set => SetField(ref _onDelete, value);
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