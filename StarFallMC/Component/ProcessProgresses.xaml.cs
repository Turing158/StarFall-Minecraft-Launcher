using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StarFallMC.Entity;
using StarFallMC.Entity.Enum;

namespace StarFallMC.Component;

public partial class ProcessProgresses : UserControl {

    public ObservableCollection<ProcessProgress> Progresses {
        get => (ObservableCollection<ProcessProgress>)GetValue(ProgressesProperty);
        set => SetValue(ProgressesProperty, value);
    }
    public static DependencyProperty ProgressesProperty = DependencyProperty.Register(
        nameof(Progresses),
        typeof(ObservableCollection<ProcessProgress>),
        typeof(ProcessProgresses),
        new PropertyMetadata(new ObservableCollection<ProcessProgress>())
    );
    
    public ProcessProgresses() {
        InitializeComponent();
    }
    
    public void ChangeProcessStatus(string key,ProcessStatus status,bool ChangeNextStep) {
        var process = Progresses.FirstOrDefault(p => p.ProgressKey == key);
        int processIndex = Progresses.IndexOf(process);
        if (processIndex != -1 && process.CurrentStep is int currentStep && currentStep < process.Progesses.Count && !process.IsComplete) {
            if (currentStep < 0) {
                ChangeNextStep = true;
            }
            else {
                Progresses[processIndex].Progesses[currentStep].Status = status;
            }

            if (currentStep == process.Progesses.Count - 1) {
                Progresses[processIndex].IsComplete = true;
                process.OnComplete.Invoke(process);
            }
            if (ChangeNextStep && currentStep < process.Progesses.Count - 1) {
                Progresses[processIndex].Progesses[currentStep + 1].Status = ProcessStatus.Doing;
                Progresses[processIndex].CurrentStep++;
            }
        }
    }

    public void ChangeProcessStatusWithIndex(string key,ProcessStatus status,int progressIndex, string progressName = null) {
        var process = Progresses.FirstOrDefault(p => p.ProgressKey == key);
        int processIndex = Progresses.IndexOf(process);
        if (processIndex != -1) {
            Progresses[processIndex].Progesses[progressIndex].Status = status;
            if (!string.IsNullOrEmpty(progressName)) {
                Progresses[processIndex].Progesses[progressIndex].ProgressName = progressName;
            }
        }
    }
    
    public void ResetProcessStatus(string key, bool autoDoingFirst = false) {
        var process = Progresses.FirstOrDefault(p => p.ProgressKey == key);
        int processIndex = Progresses.IndexOf(process);
        if (processIndex != -1) {
            process.IsComplete = false;
            foreach (var progress in process.Progesses) {
                progress.Status = ProcessStatus.Wait;
            }
            if (autoDoingFirst) {
                process.Progesses[0].Status = ProcessStatus.Doing;
            }
            process.CurrentStep = autoDoingFirst ? 0 : -1;
        }
    }
    
    public string AppendProcessProgress(string name, List<string> progressNames,bool autoDoingFirst = false) {
        var process = new ProcessProgress() {
            ProgressKey = "Process_" + HashCode.Combine(name,DateTime.Now.ToString("yyyyMMddHHmmssffff")),
            ProgressName = name,
            Progesses = new ObservableCollection<ProcessProgressItem>()
        };
        foreach (var progressName in progressNames) {
            process.Progesses.Add(new ProcessProgressItem() {
                ProgressName = progressName,
                Status = ProcessStatus.Wait
            });
        }
        process.CurrentStep = autoDoingFirst ? 0 : -1;
        if (autoDoingFirst) {
            process.Progesses[0].Status = ProcessStatus.Doing;
        }
        Progresses.Insert(0,process);
        return process.ProgressKey;
    }
    
    public void ChangeProcessProgressCallback(string key, Action<ProcessProgress> callback, bool isOnDelete = false) {
        var process = Progresses.FirstOrDefault(p => p.ProgressKey == key);
        if (process != null) {
            if (isOnDelete) {
                process.OnDelete = callback;
            }
            else {
                process.OnComplete = callback;
            }
        }
    }
    public void DeleteProcessProgress(string key) {
        var process = Progresses.FirstOrDefault(p => p.ProgressKey == key);
        if (process != null) {
            process.OnDelete.Invoke(process);
            var item = ProcessListView.ItemContainerGenerator.ContainerFromIndex(Progresses.IndexOf(process)) as ListViewItem;
            if (item != null) {
                item.RenderTransform = new TranslateTransform();
                item.RenderTransform.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation() {
                    To = item.ActualWidth,
                    Duration = TimeSpan.FromSeconds(0.5),
                    EasingFunction = new CubicEase(),
                });
                var hideAnim = new DoubleAnimation() {
                    To = 0,
                    Duration = TimeSpan.FromSeconds(0.2),
                    EasingFunction = new CubicEase(),
                };
                hideAnim.Completed += (sender, args) => {
                    Progresses.Remove(process);
                };
                item.BeginAnimation(OpacityProperty,hideAnim);
            }
        }
    }

    private void ProcessDelete_OnClick(object sender, RoutedEventArgs e) {
        var button = sender as TextButton;
        string key = button?.Tag?.ToString();
        if (string.IsNullOrEmpty(key)) {
            return;
        }
        DeleteProcessProgress(key);
    }
}