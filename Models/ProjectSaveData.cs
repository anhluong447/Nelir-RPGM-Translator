using System.Collections.Generic;

namespace Nelir.Models
{
    public class ProjectSaveData
    {
        public string DataFolderPath { get; set; } = string.Empty;
        public List<string> LoadedFiles { get; set; } = [];
        public Dictionary<string, string> Translations { get; set; } = [];
    }
}
