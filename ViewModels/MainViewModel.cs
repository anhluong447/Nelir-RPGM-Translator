using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NolirRpgmTranslator.Models;
using NolirRpgmTranslator.Services;
using System.Text.RegularExpressions;

namespace NolirRpgmTranslator.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly RpgmParser _parser;
        private readonly MtlImporter _mtlImporter;
        private readonly ExportService _exportService;
        private readonly AppSettingsService _settingsService;
        private AutoSaveService? _autoSaveService;

        [ObservableProperty]
        private ProjectState _project;

        [ObservableProperty]
        private ObservableCollection<FileNode> _fileTree;

        [ObservableProperty]
        private FileNode? _selectedFile;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private int _totalRowsCount;

        [ObservableProperty]
        private int _translatedRowsCount;

        [ObservableProperty]
        private double _completionPercentage;

        [ObservableProperty]
        private string _completionText = "0 / 0 lines translated (0.00%)";

        [ObservableProperty]
        private string _autoSaveStatus = "Not Active";

        [ObservableProperty]
        private bool _isProjectLoaded;

        // Binds to the DataGrid
        public ICollectionView RowsView { get; }

        public MainViewModel()
        {
            _parser = new RpgmParser();
            _mtlImporter = new MtlImporter();
            _exportService = new ExportService();
            _settingsService = new AppSettingsService();
            
            _project = new ProjectState();
            _fileTree = new ObservableCollection<FileNode>();

            // Setup collection view for filtering and sorting
            RowsView = CollectionViewSource.GetDefaultView(_project.AllRows);
            RowsView.Filter = FilterRows;

            // Load last settings if available
            var settings = _settingsService.CurrentSettings;
            if (!string.IsNullOrEmpty(settings.LastDataFolder) && Directory.Exists(settings.LastDataFolder))
            {
                // Defer folder loading until View is ready, or run it
                _ = LoadFolderAsync(settings.LastDataFolder);
            }
        }

        partial void OnSelectedFileChanged(FileNode? value)
        {
            RowsView.Refresh();
        }

        partial void OnSearchQueryChanged(string value)
        {
            RowsView.Refresh();
        }

        private bool FilterRows(object item)
        {
            if (item is TranslationRow row)
            {
                // 1. Filter by Selected TreeView Item
                if (SelectedFile != null && !string.IsNullOrEmpty(SelectedFile.FilePath))
                {
                    // If selected node has a filepath, it represents a single file
                    if (row.SourceFile != SelectedFile.FileName)
                    {
                        return false;
                    }
                }

                // 2. Filter by search query
                if (!string.IsNullOrEmpty(SearchQuery))
                {
                    bool isMatch = row.RawText.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                  row.Speaker.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                  row.MtlText.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                  row.TranslationText.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);

                    // Don't show SectionHeaders when searching unless they are the only things (better to skip)
                    if (row.RowType == RowType.SectionHeader)
                    {
                        return false;
                    }

                    if (!isMatch)
                    {
                        return false;
                    }
                }

                return true;
            }
            return false;
        }

        [RelayCommand]
        private async Task SelectFolderCommand()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select RPGMaker Game 'data' Folder",
                InitialDirectory = Directory.Exists(_settingsService.CurrentSettings.LastDataFolder) 
                    ? _settingsService.CurrentSettings.LastDataFolder 
                    : string.Empty
            };

            if (dialog.ShowDialog() == true)
            {
                await LoadFolderAsync(dialog.FolderName);
            }
        }

        private async Task LoadFolderAsync(string folderPath)
        {
            IsBusy = true;
            ErrorMessage = null;
            IsProjectLoaded = false;

            // Stop existing autosave if active
            _autoSaveService?.Stop();

            try
            {
                var files = Directory.GetFiles(folderPath, "*.json")
                    .Select(Path.GetFileName)
                    .Where(name => name != null && 
                                   (Regex.IsMatch(name, @"^Map\d+\.json$", RegexOptions.IgnoreCase) || 
                                    name.Equals("CommonEvents.json", StringComparison.OrdinalIgnoreCase)))
                    .Select(name => name!)
                    .OrderBy(name => name)
                    .ToList();

                if (files.Count == 0)
                {
                    ErrorMessage = "No RPGMaker Map*.json or CommonEvents.json files found in selected folder.";
                    IsBusy = false;
                    return;
                }

                var allRowsList = new List<TranslationRow>();
                var treeNodes = new List<FileNode>();

                // Parse on background thread to keep UI interactive
                await Task.Run(() =>
                {
                    foreach (var file in files)
                    {
                        string fullPath = Path.Combine(folderPath, file);
                        var parsedRows = _parser.ParseFile(fullPath);
                        if (parsedRows.Count > 0)
                        {
                            allRowsList.AddRange(parsedRows);

                            // Calculate count of editable segments for stats in treeview
                            int translatableCount = parsedRows.Count(r => r.RowType != RowType.SectionHeader);
                            treeNodes.Add(new FileNode
                            {
                                FileName = file,
                                FilePath = fullPath,
                                IsLoaded = true,
                                TotalRows = translatableCount
                            });
                        }
                    }
                });

                // Clear and update project state
                Project.DataFolderPath = folderPath;
                Project.LoadedFiles = files;
                Project.AllRows.Clear();
                Project.RowIndex.Clear();

                int translatableTotal = 0;
                foreach (var row in allRowsList)
                {
                    Project.AllRows.Add(row);
                    if (row.RowType != RowType.SectionHeader)
                    {
                        Project.RowIndex[row.UniqueKey] = row;
                        translatableTotal++;
                        
                        // Subscribe to change triggers to update stats
                        row.PropertyChanged += Row_PropertyChanged;
                    }
                }

                // Populate File Tree
                FileTree.Clear();
                var rootNode = new FileNode
                {
                    FileName = "Data Folder",
                    FilePath = string.Empty, // Empty represents All Files root
                    TotalRows = translatableTotal
                };
                foreach (var node in treeNodes)
                {
                    rootNode.Children.Add(node);
                }
                FileTree.Add(rootNode);

                SelectedFile = rootNode;

                // Update settings
                _settingsService.CurrentSettings.LastDataFolder = folderPath;
                _settingsService.SaveSettings();

                IsProjectLoaded = true;

                // Initialize AutoSave
                _autoSaveService = new AutoSaveService(Project, _exportService);
                _autoSaveService.AutoSaveCompleted += AutoSaveService_AutoSaveCompleted;
                _autoSaveService.Start();
                AutoSaveStatus = "Auto-save Active (Every 30s)";

                UpdateStats();

                // Check for existing autosave file to restore
                string autosaveFile = Path.Combine(folderPath, ".nolir_autosave.json");
                if (File.Exists(autosaveFile))
                {
                    var result = MessageBox.Show(
                        "An existing auto-save backup file was found. Do you want to restore your previous translation session?",
                        "Restore Backup",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        int restoredCount = _mtlImporter.ImportMtl(autosaveFile, Project);
                        MessageBox.Show($"Restored {restoredCount} translations from auto-save backup.", "Restore Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                        UpdateStats();
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load folder: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TranslationRow.TranslationText))
            {
                UpdateStats();
                AutoSaveStatus = "Unsaved Changes";
            }
        }

        private void AutoSaveService_AutoSaveCompleted(object? sender, DateTime time)
        {
            AutoSaveStatus = $"Auto-saved at {time:HH:mm:ss}";
        }

        private void UpdateStats()
        {
            TotalRowsCount = Project.AllRows.Count(r => r.RowType != RowType.SectionHeader);
            TranslatedRowsCount = Project.AllRows.Count(r => r.RowType != RowType.SectionHeader && !string.IsNullOrEmpty(r.TranslationText));
            
            if (TotalRowsCount > 0)
            {
                CompletionPercentage = (double)TranslatedRowsCount / TotalRowsCount * 100.0;
            }
            else
            {
                CompletionPercentage = 0.0;
            }

            CompletionText = $"{TranslatedRowsCount} / {TotalRowsCount} lines translated ({CompletionPercentage:F2}%)";
        }

        [RelayCommand]
        private void ImportMtlCommand()
        {
            if (!IsProjectLoaded) return;

            var dialog = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Import MTL Translation Flat JSON"
            };

            if (dialog.ShowDialog() == true)
            {
                int merged = _mtlImporter.ImportMtl(dialog.FileName, Project);
                MessageBox.Show($"Successfully merged {merged} translation lines from MTL.", "Import Finished", MessageBoxButton.OK, MessageBoxImage.Information);
                UpdateStats();
                
                _settingsService.CurrentSettings.LastMtlFile = dialog.FileName;
                _settingsService.SaveSettings();
            }
        }

        [RelayCommand]
        private void ExportFlatCommand()
        {
            if (!IsProjectLoaded) return;

            var dialog = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                Title = "Export Translation Flat JSON",
                FileName = "translations.json"
            };

            if (dialog.ShowDialog() == true)
            {
                _exportService.ExportFlatJson(Project, dialog.FileName);
                
                // Save manually also clears dirty changes flags since they are safe
                foreach (var row in Project.AllRows)
                {
                    row.IsDirty = false;
                }
                AutoSaveStatus = "Saved changes successfully";

                MessageBox.Show("Flat translation JSON exported successfully.", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        private void ExportGameCommand()
        {
            if (!IsProjectLoaded) return;

            var dialog = new OpenFolderDialog
            {
                Title = "Select Output Folder for Translated Game Files"
            };

            if (dialog.ShowDialog() == true)
            {
                string outputDir = dialog.FolderName;

                if (outputDir.Equals(Project.DataFolderPath, StringComparison.OrdinalIgnoreCase))
                {
                    var warningResult = MessageBox.Show(
                        "Selecting the original 'data' folder may overwrite your raw game files. It is highly recommended to output to a separate directory. Proceed anyway?",
                        "Warning: Overwrite Game Files",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (warningResult == MessageBoxResult.No)
                    {
                        return;
                    }
                }

                IsBusy = true;
                try
                {
                    int filesWritten = _exportService.ExportToGameFiles(Project, outputDir);
                    MessageBox.Show($"Successfully wrote translation back into {filesWritten} game files.", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        [RelayCommand]
        public void CopyRaw(TranslationRow? row)
        {
            if (row != null && !string.IsNullOrEmpty(row.RawText))
            {
                Clipboard.SetText(row.RawText);
            }
        }

        [RelayCommand]
        public void CopyMtl(TranslationRow? row)
        {
            if (row != null && !string.IsNullOrEmpty(row.MtlText))
            {
                Clipboard.SetText(row.MtlText);
            }
        }

        [RelayCommand]
        public void ApplyMtlAsTranslation(TranslationRow? row)
        {
            if (row != null && !string.IsNullOrEmpty(row.MtlText))
            {
                row.TranslationText = row.MtlText;
            }
        }

        [RelayCommand]
        public void ClearTranslation(TranslationRow? row)
        {
            if (row != null)
            {
                row.TranslationText = string.Empty;
            }
        }

        // Cleanup resources on Window close
        public void SaveSettingsAndCleanup()
        {
            _autoSaveService?.TriggerAutoSave();
            _autoSaveService?.Stop();
            _settingsService.SaveSettings();
        }
    }
}
