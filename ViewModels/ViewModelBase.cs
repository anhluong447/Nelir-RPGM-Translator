using CommunityToolkit.Mvvm.ComponentModel;

namespace Nelir.ViewModels
{
    public partial class ViewModelBase : ObservableObject
    {
        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string? _errorMessage;
    }
}

