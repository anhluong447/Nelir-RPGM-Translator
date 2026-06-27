using CommunityToolkit.Mvvm.ComponentModel;

namespace Nelir.Models
{
    public enum RowType
    {
        SectionHeader,   // Header line for event demarcation (non-translatable)
        Dialog,          // Show Message (RPGM code 101/401)
        Choice,          // Show Choices (RPGM code 102)
        Comment          // Show Comment (RPGM code 108/408)
    }

    public partial class TranslationRow : ObservableObject
    {
        public int RowIndex { get; set; }
        public string SourceFile { get; set; } = string.Empty;
        public int EventId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public int PageIndex { get; set; }
        public int CommandIndex { get; set; }
        public int SubIndex { get; set; }
        public RowType RowType { get; set; }

        public string RawText { get; set; } = string.Empty;
        public string Speaker { get; set; } = string.Empty;

        [ObservableProperty]
        private string _mtlText = string.Empty;

        [ObservableProperty]
        private string _translationText = string.Empty;

        [ObservableProperty]
        private bool _isDirty;

        // Custom display helper for DataGrid Column binding
        public string DisplayRawText
        {
            get
            {
                if (RowType == RowType.SectionHeader)
                {
                    return RawText;
                }
                return string.IsNullOrEmpty(Speaker) ? RawText : $"[{Speaker}]: {RawText}";
            }
        }

        public string UniqueKey => $"{SourceFile}::{EventId}::{PageIndex}::{CommandIndex}::{SubIndex}";

        partial void OnTranslationTextChanged(string value)
        {
            IsDirty = true;
        }
    }
}

