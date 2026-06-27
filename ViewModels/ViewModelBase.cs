using CommunityToolkit.Mvvm.ComponentModel;

namespace Nelir.ViewModels
{
    public partial class ViewModelBase : ObservableObject
    {
        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private string _busyStatus = string.Empty;

        [ObservableProperty]
        private string _busyDetail = string.Empty;

        [ObservableProperty]
        private double _busyProgress;

        [ObservableProperty]
        private string _busyPerformanceText = string.Empty;
    }
}

