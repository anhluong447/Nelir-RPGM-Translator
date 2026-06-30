using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nelir.Models
{
    public class ExportFileItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string FileName { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public int TranslatedRows { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DisplayText => $"{FileName} ({TranslatedRows} / {TotalRows} dòng)";

        public int UntranslatedCount => TotalRows - TranslatedRows;
        public string DisplayLabel => UntranslatedCount > 0
            ? $"{FileName} ({UntranslatedCount} dòng chưa dịch)"
            : FileName;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
