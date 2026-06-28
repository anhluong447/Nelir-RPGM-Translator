using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Nelir.Services
{
    /// <summary>
    /// Map bust sprite ID -> display name của nhân vật.
    /// Load/save từ character_registry.json trong RAW folder.
    /// </summary>
    public class CharacterRegistryService : ObservableObject
    {
        private string? _folderPath;

        // bust_id (e.g. "machoV") -> display name (e.g. "Black Muscle")
        public Dictionary<string, string> Registry { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Default registry nhúng sẵn - user có thể override bằng character_registry.json
        /// </summary>
        private static readonly Dictionary<string, string> DefaultRegistry = new(StringComparer.OrdinalIgnoreCase)
        {
            // Thêm entries mặc định ở đây nếu biết trước
            // { "machoV",   "Black Muscle" },
            // { "hasumiV",  "Revantia" },
        };

        public void Load(string folderPath)
        {
            _folderPath = folderPath;
            Registry = new Dictionary<string, string>(DefaultRegistry, StringComparer.OrdinalIgnoreCase);

            var filePath = Path.Combine(folderPath, "character_registry.json");
            if (!File.Exists(filePath)) return;

            try
            {
                var json = File.ReadAllText(filePath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (loaded == null) return;
                foreach (var kv in loaded)
                    Registry[kv.Key] = kv.Value;
            }
            catch { /* Ignore parse errors */ }

            OnPropertyChanged(nameof(Registry));
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(_folderPath)) return;
            var filePath = Path.Combine(_folderPath, "character_registry.json");
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(filePath, JsonSerializer.Serialize(Registry, options));
            }
            catch { }
        }

        public string Resolve(string bustId)
            => Registry.TryGetValue(bustId, out var name) ? name : bustId;

        public void Register(string bustId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(bustId)) return;
            Registry[bustId] = displayName;
            OnPropertyChanged(nameof(Registry));
        }
    }
}
