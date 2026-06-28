using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Nelir.Models;
using Nelir.Services;

namespace Nelir
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            if (e.Args.Length > 0 && e.Args[0] == "--test")
            {
                RunSelfDiagnostics();
                Shutdown();
                return;
            }

            base.OnStartup(e);

            // MainWindow will be instantiated and shown
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        private void RunSelfDiagnostics()
        {
            try
            {
                Console.WriteLine("=== RUNNING SELF DIAGNOSTICS ===");
                
                string testFolder = @"D:\Shits\Prj\Nelir-RPGM-Translator\test_data";
                string mapFile = Path.Combine(testFolder, "Map001.json");
                
                if (!File.Exists(mapFile))
                {
                    throw new FileNotFoundException("Test file Map001.json not found!");
                }
                
                var parser = new RpgmParser();
                var rows = parser.ParseFile(mapFile);
                
                Console.WriteLine($"Parsed {rows.Count} rows.");
                
                // Assert row counts and types
                if (rows.Count != 5)
                {
                    throw new Exception($"Expected 5 rows, got {rows.Count}");
                }
                
                // Row 0: Section header
                if (rows[0].RowType != RowType.SectionHeader || !rows[0].RawText.Contains("Event [1]"))
                {
                    throw new Exception("Row 0 is not the expected SectionHeader");
                }
                
                // Row 1: Dialog
                var dialogRow = rows[1];
                if (dialogRow.RowType != RowType.Dialog || dialogRow.Speaker != "Nolir" || 
                    !dialogRow.RawText.Contains("Xin chào thế giới!"))
                {
                    throw new Exception($"Row 1 is not the expected Dialog: {dialogRow.RawText}");
                }
                
                // Edit dialog text
                dialogRow.TranslationText = "Hello world! I am Nolir.\nThis game will be translated to Vietnamese.";
                
                // Setup project state for export
                var project = new ProjectState
                {
                    DataFolderPath = testFolder,
                    LoadedFiles = new List<string> { "Map001.json" }
                };
                project.AllRows.Add(rows[0]);
                project.AllRows.Add(dialogRow);
                project.AllRows.Add(rows[2]);
                project.AllRows.Add(rows[3]);
                project.AllRows.Add(rows[4]);
                project.RowIndex[dialogRow.UniqueKey] = dialogRow;
                
                // Export to test_output folder
                string outputFolder = @"D:\Shits\Prj\Nelir-RPGM-Translator\test_output";
                if (Directory.Exists(outputFolder))
                {
                    Directory.Delete(outputFolder, true);
                }
                Directory.CreateDirectory(outputFolder);
                
                var exporter = new ExportService();
                
                // 1. Test game replacement files
                exporter.ExportToGameFiles(project, outputFolder);
                
                string exportedMapFile = Path.Combine(outputFolder, "Map001.json");
                if (!File.Exists(exportedMapFile))
                {
                    throw new FileNotFoundException("Exported Map001.json was not created!");
                }
                
                // Re-parse exported file to verify
                var reParsedRows = parser.ParseFile(exportedMapFile);
                Console.WriteLine($"Re-parsed exported file. Found {reParsedRows.Count} rows.");
                
                if (reParsedRows.Count != 5)
                {
                    throw new Exception($"Expected 5 re-parsed rows, got {reParsedRows.Count}");
                }
                
                var reParsedDialog = reParsedRows[1];
                if (reParsedDialog.RawText != "Hello world! I am Nolir.\nThis game will be translated to Vietnamese.")
                {
                    throw new Exception($"Re-parsed dialog text does not match translated text: {reParsedDialog.RawText}");
                }
                
                // 2. Test flat JSON export
                string flatFile = Path.Combine(outputFolder, "Map001_flat.json");
                exporter.ExportFileFlatJson(project, "Map001.json", flatFile);
                if (!File.Exists(flatFile))
                {
                    throw new FileNotFoundException("Flat JSON file was not created!");
                }
                string flatContent = File.ReadAllText(flatFile);
                if (!flatContent.Contains("Hello world! I am Nolir.") || !flatContent.Contains("This game will be translated to Vietnamese."))
                {
                    throw new Exception("Flat JSON does not contain translated dialog lines");
                }
                Console.WriteLine("Flat JSON export verification passed.");

                // 3. Test structured JSON export
                string structuredFile = Path.Combine(outputFolder, "Map001_structured.json");
                exporter.ExportFileStructured(project, "Map001.json", structuredFile);
                if (!File.Exists(structuredFile))
                {
                    throw new FileNotFoundException("Structured JSON file was not created!");
                }
                string structuredContent = File.ReadAllText(structuredFile);
                if (!structuredContent.Contains("Hello world! I am Nolir.") || !structuredContent.Contains("events") || !structuredContent.Contains("Mở đầu"))
                {
                    throw new Exception("Structured JSON does not contain translations or root schema elements");
                }
                Console.WriteLine("Structured JSON export verification passed.");

                // 4. Test project saving/loading (.nel format)
                string nelProjFile = Path.Combine(outputFolder, "test_project.nel");
                var savedDict = new Dictionary<string, string>();
                foreach (var r in project.AllRows)
                {
                    if (r.RowType != RowType.SectionHeader && !string.IsNullOrEmpty(r.TranslationText))
                    {
                        savedDict[r.UniqueKey] = r.TranslationText;
                    }
                }
                var projSave = new ProjectSaveData
                {
                    DataFolderPath = project.DataFolderPath,
                    LoadedFiles = project.LoadedFiles,
                    Translations = savedDict
                };
                string nelJson = System.Text.Json.JsonSerializer.Serialize(projSave, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(nelProjFile, nelJson, System.Text.Encoding.UTF8);

                if (!File.Exists(nelProjFile))
                {
                    throw new FileNotFoundException(".nel project file was not created!");
                }

                // Try to load and deserialize the .nel file
                string loadedNelJson = File.ReadAllText(nelProjFile);
                var loadedProj = System.Text.Json.JsonSerializer.Deserialize<ProjectSaveData>(loadedNelJson);
                if (loadedProj == null)
                {
                    throw new Exception("Failed to deserialize .nel project file");
                }
                if (loadedProj.DataFolderPath != project.DataFolderPath)
                {
                    throw new Exception("Deserialized DataFolderPath does not match");
                }
                if (loadedProj.Translations.Count != 1 || !loadedProj.Translations.ContainsKey(dialogRow.UniqueKey))
                {
                    throw new Exception("Deserialized Translations list is incorrect");
                }
                if (loadedProj.Translations[dialogRow.UniqueKey] != dialogRow.TranslationText)
                {
                    throw new Exception("Deserialized TranslationText does not match");
                }
                Console.WriteLine("Nel project file save/load verification passed.");

                Console.WriteLine("DIAGNOSTICS PASSED SUCCESSFULLY!");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"DIAGNOSTICS FAILED: {ex.Message}");
                Console.ResetColor();
                Environment.Exit(1);
            }
        }
    }
}

