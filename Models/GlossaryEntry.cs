using CommunityToolkit.Mvvm.ComponentModel;

namespace Nelir.Models
{
    public partial class GlossaryEntry : ObservableObject
    {
        [ObservableProperty]
        private string _originalTerm = string.Empty;

        [ObservableProperty]
        private string _translatedTerm = string.Empty;
    }
}
