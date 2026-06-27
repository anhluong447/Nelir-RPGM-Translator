using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Nelir.Models;

namespace Nelir.Services
{
    public class GlossaryService : ObservableObject
    {
        private string? _currentFolderPath;

        public ObservableCollection<GlossaryEntry> Entries { get; } = [];

        private Dictionary<string, string> _lookup = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Lookup
        {
            get => _lookup;
            private set => SetProperty(ref _lookup, value);
        }

        public void Load(string folderPath)
        {
            _currentFolderPath = folderPath;
            Entries.Clear();

            string filePath = Path.Combine(folderPath, "glossary.json");
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    var list = JsonSerializer.Deserialize<List<GlossaryEntry>>(json);
                    if (list != null)
                    {
                        foreach (var entry in list)
                        {
                            Entries.Add(entry);
                        }
                    }
                }
                catch
                {
                    // Ignore load errors
                }
            }

            UpdateLookup();
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(_currentFolderPath)) return;

            string filePath = Path.Combine(_currentFolderPath, "glossary.json");
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(Entries, options);
                File.WriteAllText(filePath, json);
            }
            catch
            {
                // Ignore save errors
            }

            UpdateLookup();
        }

        public void UpdateLookup()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in Entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.OriginalTerm) && !dict.ContainsKey(entry.OriginalTerm))
                {
                    dict[entry.OriginalTerm] = entry.TranslatedTerm;
                }
            }
            Lookup = dict;
            
            // Raise property change for Lookup to force binding updates
            OnPropertyChanged(nameof(Lookup));
        }
    }
}
