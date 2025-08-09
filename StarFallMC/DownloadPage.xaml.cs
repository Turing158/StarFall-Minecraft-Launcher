using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using StarFallMC.Entity;
using StarFallMC.Util;

namespace StarFallMC;

public partial class DownloadPage : Page {

    private ViewModel viewModel = new ViewModel();

    public static Action<List<DownloadFile>> ProgressInit;
    public static Action<DownloadFile,int,int> ProgressUpdate;
    public DownloadPage() {
        InitializeComponent();
        DataContext = viewModel;
        
        ProgressInit = progressInit;
        ProgressUpdate = progressUpdate;
        
    }
    
    public class ViewModel : INotifyPropertyChanged {
        public event PropertyChangedEventHandler? PropertyChanged;
        
        private ObservableCollection<DownloadFile> _downloads = new ();
        public ObservableCollection<DownloadFile> Downloads {
            get => _downloads;
            set => SetField(ref _downloads, value);
        }

        private int _speed;
        public int Speed {
            get => _speed;
            set => SetField(ref _speed, value);
        }

        private int _total;
        public int Total {
            get => _total;
            set => SetField(ref _total, value);
        }
        
        private int _finished;
        public int Finished {
            get => _finished;
            set => SetField(ref _finished, value);
        }
        
        private int _errorCount;
        public int ErrorCount {
            get => _errorCount;
            set => SetField(ref _errorCount, value);
        }

        private int _remaining;
        public int Remaining {
            get => _remaining;
            set => SetField(ref _remaining, value);
        }
        
        private string _progressText = "0%";
        public string ProgressText {
            get => _progressText;
            set => SetField(ref _progressText, value);
        }

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

    private void ProgressGrid_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        if (ProgressGrid.Opacity == 0) {
            ProgressGrid.BeginAnimation(OpacityProperty,new DoubleAnimation(1,new Duration(TimeSpan.FromSeconds(0.2))));
        }
        else {
            ProgressGrid.BeginAnimation(OpacityProperty, new DoubleAnimation(0, new Duration(TimeSpan.FromSeconds(0.2))));
        }
    }

    private void progressInit(List<DownloadFile> downloadFiles) {
        viewModel.ProgressText = "0%";
        viewModel.Total = downloadFiles.Count;
        viewModel.Remaining = downloadFiles.Count;
        viewModel.Finished = 0;
        viewModel.ErrorCount = 0;

        viewModel.Downloads = new ObservableCollection<DownloadFile>(downloadFiles);
    }
    
    private void progressUpdate(DownloadFile downloadFile,int FinishCount,int ErrorCount) {
        viewModel.Finished = FinishCount;
        viewModel.ErrorCount = ErrorCount;
        viewModel.Remaining = viewModel.Total - FinishCount;
        viewModel.ProgressText = $"{formatDouble((FinishCount == 0 ? 0 : ((double)FinishCount / viewModel.Total)) * 100,2)}%";
        int index = viewModel.Downloads.IndexOf(viewModel.Downloads.First(i => i.FilePath == downloadFile.FilePath));
        viewModel.Downloads[index] = downloadFile;
    }

    private string formatDouble(double input,int round) {
        if (round == -1) {
            return input.ToString();
        }
        if (round == 0) {
            return Math.Floor(input).ToString();
        }

        if (round > 16) {
            round = 16;
        }
        string format = "0.";
        for (int i = 0; i < round; i++) {
            format += "#";
        }
        return Math.Round(input,round).ToString(format);
    }

    private void CancelDownload_OnClick(object sender, RoutedEventArgs e) {
        Console.WriteLine("取消下载");
        try {
            DownloadUtil.CancelDownload();
        }
        catch (Exception exception){
            Console.WriteLine(exception);
            throw;
        }
    }
}