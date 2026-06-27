using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using NolirRpgmTranslator.Models;
using NolirRpgmTranslator.Services;

namespace NolirRpgmTranslator
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
