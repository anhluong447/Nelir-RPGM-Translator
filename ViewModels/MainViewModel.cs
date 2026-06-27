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
using Nelir.Models;
using Nelir.Services;
using Nelir.Views;
using System.Text.RegularExpressions;

namespace Nelir.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly RpgmParser _parser;
        private readonly MtlImporter _mtlImporter;
        private readonly ExportService _exportService;
        private readonly AppSettingsService _settingsService;
        private AutoSaveService? _autoSaveService;
        private readonly System.Windows.Threading.DispatcherTimer _searchDebounceTimer;

        [ObservableProperty]
        private ProjectState _project;

        [ObservableProperty]
        private GlossaryService _glossary;

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
            _glossary = new GlossaryService();
            _fileTree = new ObservableCollection<FileNode>();

            // Setup collection view for filtering and sorting
            RowsView = CollectionViewSource.GetDefaultView(_project.AllRows);
            RowsView.Filter = FilterRows;
            RowsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TranslationRow.SourceFile)));
            RowsView.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(TranslationRow.SourceFile), System.ComponentModel.ListSortDirection.Ascending));
            RowsView.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(TranslationRow.RowIndex), System.ComponentModel.ListSortDirection.Ascending));

            _searchDebounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;

            // Load last settings if available
            var settings = _settingsService.CurrentSettings;
            if (!string.IsNullOrEmpty(settings.LastDataFolder) && Directory.Exists(settings.LastDataFolder))
            {
                _ = LoadFolderAsync(settings.LastDataFolder);
            }
        }

        partial void OnSelectedFileChanged(FileNode? value)
        {
            RowsView.Refresh();
        }

        partial void OnSearchQueryChanged(string value)
        {
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private void SearchDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _searchDebounceTimer.Stop();
            RowsView.Refresh();
        }

        private bool FilterRows(object item)
        {
            if (item is TranslationRow row)
            {
                // 1. Never show Section Headers (Page demarcations and Event markers) in the grid
                if (row.RowType == RowType.SectionHeader)
                {
                    return false;
                }

                // 2. Filter by Selected TreeView Item
                if (SelectedFile != null && !string.IsNullOrEmpty(SelectedFile.FilePath))
                {
                    // If selected node has a filepath, it represents a single file
                    if (row.SourceFile != SelectedFile.FileName)
                    {
                        return false;
                    }
                }

                // 3. Filter by search query
                if (!string.IsNullOrEmpty(SearchQuery))
                {
                    bool isMatch = row.RawText.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                  row.Speaker.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                  row.MtlText.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                  row.TranslationText.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);

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
        private async Task SelectFolder()
        {
            try
            {
                var dialog = new OpenFolderDialog
                {
                    Title = "Select RPGMaker Game 'data' Folder"
                };

                if (!string.IsNullOrEmpty(_settingsService.CurrentSettings.LastDataFolder) && 
                    Directory.Exists(_settingsService.CurrentSettings.LastDataFolder))
                {
                    try
                    {
                        dialog.InitialDirectory = Path.GetFullPath(_settingsService.CurrentSettings.LastDataFolder);
                    }
                    catch
                    {
                        // Ignore normalization errors
                    }
                }

                bool? result = null;
                try
                {
                    result = dialog.ShowDialog(Application.Current.MainWindow);
                }
                catch (Exception)
                {
                    // Fallback: Clear InitialDirectory and retry
                    dialog.InitialDirectory = null;
                    result = dialog.ShowDialog(Application.Current.MainWindow);
                }

                if (result == true)
                {
                    await LoadFolderAsync(dialog.FolderName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi chọn thư mục RAW: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Lỗi Hệ Thống", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadFolderAsync(string folderPath)
        {
            IsBusy = true;
            BusyStatus = "Đang đọc các tệp tin game (RAW)...";
            BusyDetail = "Đang chuẩn bị danh sách tệp...";
            BusyProgress = 0;
            BusyPerformanceText = string.Empty;
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
                    int totalFiles = files.Count;
                    int filesProcessed = 0;
                    int totalRowsParsed = 0;
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                    foreach (var file in files)
                    {
                        string fullPath = Path.Combine(folderPath, file);
                        long fileSizeKB = new FileInfo(fullPath).Length / 1024;

                        App.Current.Dispatcher.Invoke(() =>
                        {
                            BusyDetail = $"{file} ({fileSizeKB:N0} KB)";
                            BusyProgress = (double)filesProcessed / totalFiles * 100;
                        });

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
                                TotalRows = translatableCount,
                                TranslatedRows = parsedRows.Count(r => r.RowType != RowType.SectionHeader && !string.IsNullOrEmpty(r.TranslationText))
                            });
                        }

                        filesProcessed++;
                        totalRowsParsed += parsedRows.Count;
                        double elapsedSec = stopwatch.Elapsed.TotalSeconds;
                        double speed = elapsedSec > 0 ? (totalRowsParsed / elapsedSec) : 0;

                        App.Current.Dispatcher.Invoke(() =>
                        {
                            BusyPerformanceText = $"Thời gian: {elapsedSec:F2}s | Đã đọc: {totalRowsParsed:N0} dòng | Tốc độ: {speed:F0} dòng/giây";
                        });
                    }
                });

                // Clear and update project state
                Project.DataFolderPath = folderPath;
                Glossary.Load(folderPath);
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
                    TotalRows = translatableTotal,
                    TranslatedRows = allRowsList.Count(r => r.RowType != RowType.SectionHeader && !string.IsNullOrEmpty(r.TranslationText))
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
                string autosaveFile = Path.Combine(folderPath, ".nelir_autosave.json");
                if (File.Exists(autosaveFile))
                {
                    var result = MessageBox.Show(
                        "An existing auto-save backup file was found. Do you want to restore your previous translation session?",
                        "Restore Backup",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        int restoredCount = RestoreBackup(autosaveFile);
                        MessageBox.Show($"Restored {restoredCount} translations from auto-save backup.", "Restore Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                        UpdateStats();
                        UpdateAllFileNodesStats();
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

        private int RestoreBackup(string backupFilePath)
        {
            int restoredCount = 0;
            try
            {
                if (!File.Exists(backupFilePath)) return 0;
                string content = File.ReadAllText(backupFilePath);
                var backupData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(content);
                if (backupData == null) return 0;

                foreach (var kvp in backupData)
                {
                    if (Project.RowIndex.TryGetValue(kvp.Key, out var row))
                    {
                        row.TranslationText = kvp.Value;
                        restoredCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi khôi phục bản sao lưu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return restoredCount;
        }

        private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TranslationRow.TranslationText))
            {
                UpdateStats();
                AutoSaveStatus = "Unsaved Changes";
                if (sender is TranslationRow row)
                {
                    UpdateFileNodeStats(row.SourceFile);
                }
            }
        }

        private void UpdateFileNodeStats(string sourceFile)
        {
            var rootNode = FileTree.FirstOrDefault();
            if (rootNode == null) return;

            var fileNode = rootNode.Children.FirstOrDefault(c => c.FileName == sourceFile);
            if (fileNode != null)
            {
                fileNode.TranslatedRows = Project.AllRows.Count(r => r.SourceFile == sourceFile && r.RowType != RowType.SectionHeader && !string.IsNullOrEmpty(r.TranslationText));
            }

            rootNode.TranslatedRows = Project.AllRows.Count(r => r.RowType != RowType.SectionHeader && !string.IsNullOrEmpty(r.TranslationText));
        }

        private void UpdateAllFileNodesStats()
        {
            var rootNode = FileTree.FirstOrDefault();
            if (rootNode == null) return;

            foreach (var child in rootNode.Children)
            {
                child.TranslatedRows = Project.AllRows.Count(r => r.SourceFile == child.FileName && r.RowType != RowType.SectionHeader && !string.IsNullOrEmpty(r.TranslationText));
            }

            rootNode.TranslatedRows = Project.AllRows.Count(r => r.RowType != RowType.SectionHeader && !string.IsNullOrEmpty(r.TranslationText));
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
        private async Task ImportMtl()
        {
            if (!IsProjectLoaded) return;

            try
            {
                var dialog = new OpenFolderDialog
                {
                    Title = "Select MTL Translation Folder"
                };

                if (dialog.ShowDialog(Application.Current.MainWindow) == true)
                {
                    IsBusy = true;
                    BusyStatus = "Đang gộp dữ liệu bản dịch máy (MTL)...";
                    BusyProgress = 0;
                    BusyDetail = "Đang phân tích thư mục...";
                    BusyPerformanceText = string.Empty;

                    string mtlFolder = dialog.FolderName;
                    int merged = 0;

                    await Task.Run(() =>
                    {
                        var files = Directory.GetFiles(mtlFolder, "*.json");
                        int totalFiles = files.Length;
                        int filesProcessed = 0;
                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                        foreach (var file in files)
                        {
                            string fileName = Path.GetFileName(file);
                            long fileSizeKB = new FileInfo(file).Length / 1024;

                            App.Current.Dispatcher.Invoke(() =>
                            {
                                BusyDetail = $"{fileName} ({fileSizeKB:N0} KB)";
                                BusyProgress = (double)filesProcessed / totalFiles * 100;
                            });

                            int fileMerged = _mtlImporter.ImportSingleMtlFile(file, Project);
                            merged += fileMerged;
                            filesProcessed++;

                            double elapsedSec = stopwatch.Elapsed.TotalSeconds;
                            double speed = elapsedSec > 0 ? (filesProcessed / elapsedSec) : 0;

                            App.Current.Dispatcher.Invoke(() =>
                            {
                                BusyPerformanceText = $"Thời gian: {elapsedSec:F2}s | Tệp đã xử lý: {filesProcessed}/{totalFiles} | Tốc độ: {speed:F1} tệp/giây";
                            });
                        }
                    });

                    MessageBox.Show($"Successfully merged {merged} translation lines from MTL folder.", "Import Finished", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateStats();
                    UpdateAllFileNodesStats();
                    
                    _settingsService.CurrentSettings.LastMtlFile = dialog.FolderName;
                    _settingsService.SaveSettings();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi import MTL: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Lỗi Hệ Thống", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void ExportFlat()
        {
            if (!IsProjectLoaded) return;

            try
            {
                var exportWindow = new ExportSelectionWindow(this, _exportService);
                exportWindow.ShowDialog();

                // Clear dirty flags after export
                foreach (var row in Project.AllRows)
                {
                    row.IsDirty = false;
                }
                AutoSaveStatus = "Saved changes successfully";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi mở hộp thoại xuất JSON: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Lỗi Hệ Thống", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void OpenGlossary()
        {
            if (!IsProjectLoaded) return;
            var window = new GlossaryWindow(Glossary);
            window.ShowDialog();
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

