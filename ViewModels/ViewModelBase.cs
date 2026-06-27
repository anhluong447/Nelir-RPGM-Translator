using CommunityToolkit.Mvvm.ComponentModel;

namespace NolirRpgmTranslator.ViewModels
{
    public partial class ViewModelBase : ObservableObject
    {
        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string? _errorMessage;
    }
}
