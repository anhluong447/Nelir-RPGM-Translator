using System;
using System.IO;
using System.Text.Json;

namespace Nelir.Services
{
    public class AppSettings
    {
        public string LastDataFolder { get; set; } = string.Empty;
        public string LastMtlFile { get; set; } = string.Empty;
        public double[] ColumnWidths { get; set; } = [400, 300, 400];
        public int AutoSaveIntervalSeconds { get; set; } = 30;
        public double WindowWidth { get; set; } = 1400;
        public double WindowHeight { get; set; } = 800;
        public bool WindowMaximized { get; set; } = false;
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
    }
}

