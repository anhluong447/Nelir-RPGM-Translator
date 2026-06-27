using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NolirRpgmTranslator.Models
{
    public class ProjectState
    {
        public string DataFolderPath { get; set; } = string.Empty;
        public List<string> LoadedFiles { get; set; } = [];
        public ObservableCollection<TranslationRow> AllRows { get; set; } = [];
        
        // Fast index mapping: UniqueKey -> TranslationRow for O(1) MTL merges and saves
        public Dictionary<string, TranslationRow> RowIndex { get; set; } = [];
    }
}
