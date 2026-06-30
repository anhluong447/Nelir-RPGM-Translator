using System;
using System.IO;
using System.Text.Json;

namespace Nelir.Services
{
    public class AppSettings
    {
        public string LastDataFolder { get; set; } = string.Empty;
        public string LastMtlFile { get; set; } = string.Empty;
        public double[] ColumnWidths { get; set; } = [48, 120, 400, 250, 50, 400];
        public int AutoSaveIntervalSeconds { get; set; } = 30;
        public double WindowWidth { get; set; } = 1400;
        public double WindowHeight { get; set; } = 800;
        public bool WindowMaximized { get; set; } = false;
        public bool IsDarkMode { get; set; } = false;
        public bool WordWrap { get; set; } = true;
        public double DataGridFontSize { get; set; } = 13;
        public bool ShowSpeakerColumn { get; set; } = true;
        public bool ShowMtlColumn { get; set; } = true;
        public System.Collections.Generic.List<string> RecentProjects { get; set; } = new();
    }

    public class AppSettingsService
    {
        private readonly string _settingsFilePath;

        public AppSettings CurrentSettings { get; private set; }

        public AppSettingsService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appDir = Path.Combine(appData, "Nelir");
            Directory.CreateDirectory(appDir);
            _settingsFilePath = Path.Combine(appDir, "settings.json");

            CurrentSettings = LoadSettings();
        }

        private AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string content = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(content);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load settings: {ex.Message}");
            }

            return new AppSettings();
        }

        public void SaveSettings()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string content = JsonSerializer.Serialize(CurrentSettings, options);
                File.WriteAllText(_settingsFilePath, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        public void AddRecentProject(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            if (CurrentSettings.RecentProjects == null)
            {
                CurrentSettings.RecentProjects = new System.Collections.Generic.List<string>();
            }

            CurrentSettings.RecentProjects.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
            CurrentSettings.RecentProjects.Insert(0, path);

            // Keep only existing files
            CurrentSettings.RecentProjects.RemoveAll(p => !File.Exists(p));

            if (CurrentSettings.RecentProjects.Count > 8)
            {
                CurrentSettings.RecentProjects = CurrentSettings.RecentProjects.GetRange(0, 8);
            }

            SaveSettings();
        }
    }
}

