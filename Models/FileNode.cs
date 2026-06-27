using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NolirRpgmTranslator.Models
{
    public partial class FileNode : ObservableObject
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public bool IsLoaded { get; set; }
        
        [ObservableProperty]
        private int _totalRows;

        [ObservableProperty]
        private ObservableCollection<FileNode> _children = [];

        // Display string showing row count stats next to file name
        public string DisplayName => TotalRows > 0 ? $"{FileName} ({TotalRows})" : FileName;
    }
}
