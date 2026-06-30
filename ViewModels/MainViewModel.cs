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
        private readonly UndoRedoService _undoRedoService;
        private readonly ThemeService _themeService;
        private readonly CharacterRegistryService _characterRegistry;
        private AutoSaveService? _autoSaveService;
        private readonly System.Windows.Threading.DispatcherTimer _searchDebounceTimer;

        public CharacterRegistryService CharacterRegistry => _characterRegistry;

        [ObservableProperty]
        private ProjectState _project;

        [ObservableProperty]
        private bool _isDarkMode;

        [ObservableProperty]
        private bool _wordWrap = true;

        [ObservableProperty]
        private double _dataGridFontSize = 13;

        [ObservableProperty]
        private bool _showSpeakerColumn = true;

        [ObservableProperty]
        private bool _showMtlColumn = true;

        [ObservableProperty]
        private int _autoSaveInterval = 30;

        [ObservableProperty]
        private TranslationRow? _selectedRow;

        public event Action<TranslationRow>? ScrollToRowRequested;

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

        [ObservableProperty]
        private string? _currentProjectFilePath;

        [ObservableProperty]
        private string _windowTitle = "Nelir's RPGM Translator v1.0";

        partial void OnCurrentProjectFilePathChanged(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                WindowTitle = "Nelir's RPGM Translator v1.0";
            }
            else
            {
                string fileName = Path.GetFileName(value);
                WindowTitle = $"Nelir's RPGM Translator - [{fileName}]";
            }
        }

        // Binds to the DataGrid
        public ICollectionView RowsView { get; }

        public string VisibleRowCountText
            => $"{RowsView.Cast<TranslationRow>().Count(r => r.RowType != RowType.SectionHeader)} dòng hiển thị";

        public ObservableCollection<string> RecentProjects { get; } = new();

        private void LoadRecentProjectsList()
        {
            RecentProjects.Clear();
            if (_settingsService.CurrentSettings.RecentProjects != null)
            {
                // Clean up non-existent files
                _settingsService.CurrentSettings.RecentProjects.RemoveAll(p => !File.Exists(p));
                _settingsService.SaveSettings();

                foreach (var path in _settingsService.CurrentSettings.RecentProjects)
                {
                    RecentProjects.Add(path);
                }
            }
        }

        [RelayCommand]
        private async Task LoadRecentProject(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                MessageBox.Show("Tệp dự án không tồn tại hoặc đã bị xóa.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                if (_settingsService.CurrentSettings.RecentProjects != null)
                {
                    _settingsService.CurrentSettings.RecentProjects.Remove(path);
                    _settingsService.SaveSettings();
                    LoadRecentProjectsList();
                }
                return;
            }
            await LoadProjectFileAsync(path);
        }

        public MainViewModel()
        {
            _characterRegistry = new CharacterRegistryService();
            _parser = new RpgmParser(_characterRegistry);
            _mtlImporter = new MtlImporter();
            _exportService = new ExportService();
            _settingsService = new AppSettingsService();
            _undoRedoService = new UndoRedoService();
            _themeService = new ThemeService();
            
            _project = new ProjectState();
            _glossary = new GlossaryService();
            _fileTree = new ObservableCollection<FileNode>();

            // Hook up static reference
            TranslationRow.UndoService = _undoRedoService;

            var settings = _settingsService.CurrentSettings;
            _isDarkMode = settings.IsDarkMode;
            _themeService.ApplyTheme(_isDarkMode);

            _wordWrap = settings.WordWrap;
            _dataGridFontSize = settings.DataGridFontSize;
            _showSpeakerColumn = settings.ShowSpeakerColumn;
            _showMtlColumn = settings.ShowMtlColumn;
            _autoSaveInterval = settings.AutoSaveIntervalSeconds;

            // Load recent projects
            LoadRecentProjectsList();

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
            if (!string.IsNullOrEmpty(settings.LastDataFolder) && Directory.Exists(settings.LastDataFolder))
            {
                _ = LoadFolderAsync(settings.LastDataFolder);
            }
        }

        partial void OnSelectedFileChanged(FileNode? value)
        {
            RowsView.Refresh();
            OnPropertyChanged(nameof(VisibleRowCountText));
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
            OnPropertyChanged(nameof(VisibleRowCountText));
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
                    if (IsProjectLoaded)
                    {
                        var relocateResult = MessageBox.Show(
                            "Dự án đã được tải. Bạn có muốn đổi liên kết (relocate) đến thư mục dữ liệu mới để có thể xuất bản dịch không?\n\n- Chọn YES để đổi liên kết thư mục dữ liệu.\n- Chọn NO để tải mới một dự án khác (sẽ làm mất tiến trình chưa lưu của dự án hiện tại).",
                            "Đổi liên kết dữ liệu",
                            MessageBoxButton.YesNoCancel,
                            MessageBoxImage.Question);

                        if (relocateResult == MessageBoxResult.Yes)
                        {
                            Project.DataFolderPath = dialog.FolderName;
                            _settingsService.CurrentSettings.LastDataFolder = dialog.FolderName;
                            _settingsService.SaveSettings();
                            
                            // Re-load glossary from the new path
                            Glossary.Load(dialog.FolderName);
                            _characterRegistry.Load(dialog.FolderName);

                            // Re-init auto save with new path
                            _autoSaveService?.Stop();
                            _autoSaveService = new AutoSaveService(Project, _exportService);
                            _autoSaveService.AutoSaveCompleted += AutoSaveService_AutoSaveCompleted;
                            _autoSaveService.Start();

                            MessageBox.Show("Đã đổi liên kết thư mục dữ liệu thành công.", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                        else if (relocateResult == MessageBoxResult.Cancel)
                        {
                            return;
                        }
                    }

                    // For NO or when project not loaded, load fresh
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
            try
            {
                await LoadFolderInternalAsync(folderPath);

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
                TranslationRow.UndoService = _undoRedoService;
                IsBusy = false;
            }
        }

        private async Task LoadFolderInternalAsync(string folderPath, List<string>? fileLimit = null)
        {
            _characterRegistry.Load(folderPath);
            TranslationRow.UndoService = null;
            IsBusy = true;
            BusyStatus = "Đang đọc các tệp tin game (RAW)...";
            BusyDetail = "Đang chuẩn bị danh sách tệp...";
            BusyProgress = 0;
            BusyPerformanceText = string.Empty;
            ErrorMessage = null;
            IsProjectLoaded = false;

            // Stop existing autosave if active
            _autoSaveService?.Stop();

            // Clear undo history
            _undoRedoService.Clear();

            var files = Directory.GetFiles(folderPath, "*.json")
                .Select(Path.GetFileName)
                .Where(name => name != null && 
                               (Regex.IsMatch(name, @"^Map\d+\.json$", RegexOptions.IgnoreCase) || 
                                name.Equals("CommonEvents.json", StringComparison.OrdinalIgnoreCase)))
                .Select(name => name!)
                .OrderBy(name => name)
                .ToList();

            if (fileLimit != null)
            {
                // Ensure we only load the files that were part of the saved project
                files = files.Where(f => fileLimit.Contains(f, StringComparer.OrdinalIgnoreCase)).ToList();
            }

            if (files.Count == 0)
            {
                throw new Exception("Không tìm thấy tệp Map*.json hoặc CommonEvents.json phù hợp trong thư mục.");
            }

            var allRowsList = new List<TranslationRow>();
            var treeNodes = new List<FileNode>();
            var failedFiles = new List<(string Name, string Error)>();

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

                    var parseResult = _parser.ParseFileResult(fullPath);
                    if (parseResult.IsSuccess)
                    {
                        var parsedRows = parseResult.Rows;
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
                        totalRowsParsed += parsedRows.Count;
                    }
                    else
                    {
                        lock (failedFiles)
                        {
                            failedFiles.Add((file, parseResult.ErrorMessage ?? "Lỗi không xác định"));
                        }
                    }

                    filesProcessed++;
                    double elapsedSec = stopwatch.Elapsed.TotalSeconds;
                    double speed = elapsedSec > 0 ? (totalRowsParsed / elapsedSec) : 0;

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        BusyPerformanceText = $"Thời gian: {elapsedSec:F2}s | Đã đọc: {totalRowsParsed:N0} dòng | Tốc độ: {speed:F0} dòng/giây";
                    });
                }
            });

            // If some files were successfully parsed, we update Project.LoadedFiles to only contain those
            var cleanFilesList = treeNodes.Select(t => t.FileName).ToList();

            // Clear and update project state
            Project.DataFolderPath = folderPath;
            Glossary.Load(folderPath);
            Project.LoadedFiles = cleanFilesList;
            Project.RowIndex.Clear();

            int translatableTotal = 0;
            foreach (var row in allRowsList)
            {
                if (row.RowType != RowType.SectionHeader)
                {
                    Project.RowIndex[row.UniqueKey] = row;
                    translatableTotal++;
                    
                    // Subscribe to change triggers to update stats
                    row.PropertyChanged += Row_PropertyChanged;
                }
            }

            // Batch add all parsed rows to AllRows to prevent massive UI thread binding refresh lag
            Project.AllRows.ClearAndAddRange(allRowsList);

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
            if (AutoSaveInterval > 0)
            {
                _autoSaveService = new AutoSaveService(Project, _exportService, AutoSaveInterval);
                _autoSaveService.AutoSaveCompleted += AutoSaveService_AutoSaveCompleted;
                _autoSaveService.Start();
                AutoSaveStatus = $"Auto-save Active (Every {AutoSaveInterval}s)";
            }
            else
            {
                AutoSaveStatus = "Auto-save Disabled";
            }

            if (failedFiles.Count > 0)
            {
                string details = string.Join("\n", failedFiles.Select(f => $"- {f.Name}: {f.Error}"));
                MessageBox.Show(
                    $"Đã tải xong thư mục game, nhưng phát hiện {failedFiles.Count} tệp tin RAW bị lỗi JSON/bị hỏng:\n\n{details}\n\nCác tệp lỗi này sẽ bị bỏ qua và không được nạp vào công cụ dịch.",
                    "Cảnh báo tệp lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            UpdateStats();
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

            var dialog = new OpenFolderDialog
            {
                Title = "Select MTL Translation Folder"
            };

            if (dialog.ShowDialog(Application.Current.MainWindow) == true)
            {
                await ImportMtlInternalAsync(dialog.FolderName, showSuccessMessage: true);
            }
        }

        private async Task ImportMtlInternalAsync(string mtlFolder, bool showSuccessMessage = true)
        {
            if (!IsProjectLoaded) return;

            TranslationRow.UndoService = null;
            try
            {
                IsBusy = true;
                BusyStatus = "Đang gộp dữ liệu bản dịch máy (MTL)...";
                BusyProgress = 0;
                BusyDetail = "Đang phân tích thư mục...";
                BusyPerformanceText = string.Empty;

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

                Project.MtlFolderPath = mtlFolder;
                _settingsService.CurrentSettings.LastMtlFile = mtlFolder;
                _settingsService.SaveSettings();

                if (showSuccessMessage)
                {
                    MessageBox.Show($"Successfully merged {merged} translation lines from MTL folder.", "Import Finished", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                UpdateStats();
                UpdateAllFileNodesStats();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi import MTL: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Lỗi Hệ Thống", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                TranslationRow.UndoService = _undoRedoService;
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
            if (row != null && !string.IsNullOrEmpty(row.MtlText) && string.IsNullOrEmpty(row.TranslationText))
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

        public void SaveColumnWidths(double[] widths)
        {
            _settingsService.CurrentSettings.ColumnWidths = widths;
            _settingsService.SaveSettings();
        }

        public double[]? GetColumnWidths()
            => _settingsService.CurrentSettings.ColumnWidths;

        [RelayCommand]
        private void JumpToNextUntranslated()
        {
            var allVisibleRows = RowsView.Cast<TranslationRow>().ToList();
            if (!allVisibleRows.Any()) return;

            int currentIndex = SelectedRow != null ? allVisibleRows.IndexOf(SelectedRow) : -1;

            // Tìm từ vị trí tiếp theo
            var next = allVisibleRows
                .Skip(currentIndex + 1)
                .FirstOrDefault(r => r.Status == TranslationStatus.Empty);

            // Wrap nếu không tìm thấy phía dưới
            if (next == null)
                next = allVisibleRows.FirstOrDefault(r => r.Status == TranslationStatus.Empty);

            if (next != null)
            {
                SelectedRow = next;
                ScrollToRowRequested?.Invoke(next);
            }
            else
            {
                AutoSaveStatus = "✓ Đã dịch hết tất cả các dòng!";
                MessageBox.Show("✓ Đã dịch hết tất cả các dòng!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        private void BulkApplyMtl()
        {
            IEnumerable<TranslationRow> targets = SelectedFile != null && !string.IsNullOrEmpty(SelectedFile.FilePath)
                ? Project.AllRows.Where(r => r.SourceFile == SelectedFile.FileName)
                : Project.AllRows;

            var emptyRows = targets
                .Where(r => r.RowType != RowType.SectionHeader
                         && string.IsNullOrEmpty(r.TranslationText)
                         && !string.IsNullOrEmpty(r.MtlText))
                .ToList();

            if (!emptyRows.Any())
            {
                MessageBox.Show("Không có dòng trống nào có bản dịch máy (MTL) để điền.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var row in emptyRows)
                row.TranslationText = row.MtlText;

            UpdateStats();
            UpdateAllFileNodesStats();
            MessageBox.Show($"Đã tự động điền {emptyRows.Count} dòng bằng bản dịch máy (MTL).", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void OpenSettings()
        {
            var win = new SettingsWindow { DataContext = this, Owner = Application.Current.MainWindow };
            win.ShowDialog();
        }

        partial void OnWordWrapChanged(bool value)
        {
            _settingsService.CurrentSettings.WordWrap = value;
            _settingsService.SaveSettings();
        }

        partial void OnDataGridFontSizeChanged(double value)
        {
            _settingsService.CurrentSettings.DataGridFontSize = value;
            _settingsService.SaveSettings();
        }

        partial void OnShowSpeakerColumnChanged(bool value)
        {
            _settingsService.CurrentSettings.ShowSpeakerColumn = value;
            _settingsService.SaveSettings();
        }

        partial void OnShowMtlColumnChanged(bool value)
        {
            _settingsService.CurrentSettings.ShowMtlColumn = value;
            _settingsService.SaveSettings();
        }

        partial void OnAutoSaveIntervalChanged(int value)
        {
            _settingsService.CurrentSettings.AutoSaveIntervalSeconds = value;
            _settingsService.SaveSettings();

            if (_autoSaveService != null)
            {
                _autoSaveService.Stop();
                if (value > 0)
                {
                    _autoSaveService = new AutoSaveService(Project, _exportService, value);
                    _autoSaveService.AutoSaveCompleted += AutoSaveService_AutoSaveCompleted;
                    _autoSaveService.Start();
                    AutoSaveStatus = $"Auto-save Active (Every {value}s)";
                }
                else
                {
                    AutoSaveStatus = "Auto-save Disabled";
                }
            }
        }

        partial void OnIsDarkModeChanged(bool value)
        {
            _themeService.ApplyTheme(value);
            _settingsService.CurrentSettings.IsDarkMode = value;
            _settingsService.SaveSettings();
        }

        [RelayCommand]
        private void ToggleTheme()
        {
            IsDarkMode = !IsDarkMode;
        }

        [RelayCommand]
        private void Undo()
        {
            var entry = _undoRedoService.Undo();
            if (entry == null) return;
            if (Project.RowIndex.TryGetValue(entry.UniqueKey, out var row))
            {
                TranslationRow.UndoService = null;
                row.TranslationText = entry.OldValue;
                TranslationRow.UndoService = _undoRedoService;
            }
        }

        [RelayCommand]
        private void Redo()
        {
            var entry = _undoRedoService.Redo();
            if (entry == null) return;
            if (Project.RowIndex.TryGetValue(entry.UniqueKey, out var row))
            {
                TranslationRow.UndoService = null;
                row.TranslationText = entry.NewValue;
                TranslationRow.UndoService = _undoRedoService;
            }
        }

        [RelayCommand]
        private void SaveProject()
        {
            if (!IsProjectLoaded) return;

            if (string.IsNullOrEmpty(CurrentProjectFilePath))
            {
                SaveProjectAs();
            }
            else
            {
                SaveProjectToPath(CurrentProjectFilePath);
            }
        }

        [RelayCommand]
        private void SaveProjectAs()
        {
            if (!IsProjectLoaded) return;

            var dialog = new SaveFileDialog
            {
                Title = "Lưu dự án dịch (.nel)",
                Filter = "Dự án Nelir (*.nel)|*.nel",
                DefaultExt = ".nel"
            };

            if (dialog.ShowDialog(Application.Current.MainWindow) == true)
            {
                SaveProjectToPath(dialog.FileName);
            }
        }

        private void BackupExistingProjectFile(string projectFilePath)
        {
            if (string.IsNullOrEmpty(projectFilePath) || !File.Exists(projectFilePath)) return;

            string backupPath = projectFilePath + ".bak";
            try
            {
                File.Copy(projectFilePath, backupPath, overwrite: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Không thể tạo backup: {ex.Message}");
            }
        }

        private void SaveProjectToPath(string path)
        {
            BackupExistingProjectFile(path);
            try
            {
                var dict = new Dictionary<string, string>();
                foreach (var r in Project.AllRows)
                {
                    if (r.RowType != RowType.SectionHeader && !string.IsNullOrEmpty(r.TranslationText))
                    {
                        dict[r.UniqueKey] = r.TranslationText;
                    }
                }

                var saveData = new ProjectSaveData
                {
                    DataFolderPath = Project.DataFolderPath,
                    MtlFolderPath = Project.MtlFolderPath,
                    LoadedFiles = Project.LoadedFiles,
                    Translations = dict
                };

                var options = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string jsonContent = System.Text.Json.JsonSerializer.Serialize(saveData, options);
                File.WriteAllText(path, jsonContent, System.Text.Encoding.UTF8);

                CurrentProjectFilePath = path;
                AutoSaveStatus = $"Saved project at {DateTime.Now:HH:mm:ss}";
                _settingsService.AddRecentProject(path);
                LoadRecentProjectsList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu dự án: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task OpenProject()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Mở dự án dịch (.nel)",
                    Filter = "Dự án Nelir (*.nel)|*.nel",
                    DefaultExt = ".nel"
                };

                if (dialog.ShowDialog(Application.Current.MainWindow) == true)
                {
                    await LoadProjectFileAsync(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi mở dự án: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadProjectFileAsync(string projectPath)
        {
            IsBusy = true;
            BusyStatus = "Đang đọc tệp dự án (.nel)...";
            BusyDetail = "Đang chuẩn bị dữ liệu...";
            BusyProgress = 0;
            BusyPerformanceText = string.Empty;
            ErrorMessage = null;

            try
            {
                ProjectSaveData? saveData = null;
                bool loadedFromBackup = false;

                try
                {
                    string json = await Task.Run(() => File.ReadAllText(projectPath));
                    saveData = System.Text.Json.JsonSerializer.Deserialize<ProjectSaveData>(json);
                }
                catch (Exception primaryEx)
                {
                    string backupPath = projectPath + ".bak";
                    if (File.Exists(backupPath))
                    {
                        try
                        {
                            string backupJson = await Task.Run(() => File.ReadAllText(backupPath));
                            saveData = System.Text.Json.JsonSerializer.Deserialize<ProjectSaveData>(backupJson);
                            loadedFromBackup = true;
                        }
                        catch
                        {
                            throw primaryEx; // Both failed, throw original exception
                        }
                    }
                    else
                    {
                        throw;
                    }
                }

                if (saveData == null)
                {
                    throw new Exception("Định dạng tệp .nel không hợp lệ hoặc bị hỏng.");
                }

                string rawFolder = saveData.DataFolderPath;

                // Validate original data folder
                if (string.IsNullOrEmpty(rawFolder) || !Directory.Exists(rawFolder))
                {
                    var relocateDialog = new OpenFolderDialog
                    {
                        Title = $"Không tìm thấy thư mục game gốc tại: {rawFolder}. Vui lòng chọn lại thư mục chứa Map*.json."
                    };

                    if (relocateDialog.ShowDialog(Application.Current.MainWindow) == true)
                    {
                        rawFolder = relocateDialog.FolderName;
                    }
                    else
                    {
                        // Cancel open operation
                        return;
                    }
                }

                // Call the folder loading logic to parse the game files
                await LoadFolderInternalAsync(rawFolder, saveData.LoadedFiles);

                // Auto-import MTL folder if specified
                if (!string.IsNullOrEmpty(saveData.MtlFolderPath))
                {
                    if (Directory.Exists(saveData.MtlFolderPath))
                    {
                        await ImportMtlInternalAsync(saveData.MtlFolderPath, showSuccessMessage: false);
                    }
                    else
                    {
                        Project.MtlFolderPath = saveData.MtlFolderPath; // Preserve path in model
                        MessageBox.Show($"Không tìm thấy thư mục MTL tại: {saveData.MtlFolderPath}. Bạn có thể tự import lại thư mục MTL bằng nút 'Thư mục MTL' sau.", "Không Tìm Thấy MTL", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                // Overlay translations
                TranslationRow.UndoService = null; // Disable undo-redo recording during overlay
                int matchedCount = 0;
                foreach (var kvp in saveData.Translations)
                {
                    if (Project.RowIndex.TryGetValue(kvp.Key, out var row))
                    {
                        row.TranslationText = kvp.Value;
                        matchedCount++;
                    }
                }

                TranslationRow.UndoService = _undoRedoService; // Restore undo-redo
                CurrentProjectFilePath = projectPath;
                UpdateStats();
                UpdateAllFileNodesStats();
                _settingsService.AddRecentProject(projectPath);
                LoadRecentProjectsList();

                if (loadedFromBackup)
                {
                    MessageBox.Show($"Tệp dự án chính bị lỗi, đã tự động khôi phục từ bản backup gần nhất.\nÁp dụng {matchedCount} dòng bản dịch từ backup.", "Đã khôi phục từ backup", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show($"Đã tải dự án thành công. Áp dụng {matchedCount} dòng bản dịch từ dự án.", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải dự án: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                // Clear state if failed
                IsProjectLoaded = false;
                Project.AllRows.Clear();
                Project.RowIndex.Clear();
                FileTree.Clear();
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CheckForUpdates()
        {
            if (!IsProjectLoaded) return;

            var dialog = new OpenFolderDialog
            {
                Title = "Chọn thư mục RAW phiên bản mới để so sánh"
            };

            if (dialog.ShowDialog(Application.Current.MainWindow) != true) return;

            string newFolder = dialog.FolderName;
            if (string.Equals(newFolder, Project.DataFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Thư mục được chọn trùng khớp với thư mục RAW hiện tại của dự án.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsBusy = true;
            BusyStatus = "Đang đọc các tệp tin game mới (RAW)...";
            BusyDetail = "Đang chuẩn bị danh sách tệp...";
            BusyProgress = 0;
            BusyPerformanceText = string.Empty;

            try
            {
                var files = Directory.GetFiles(newFolder, "*.json")
                    .Select(Path.GetFileName)
                    .Where(name => name != null && 
                                   (Regex.IsMatch(name, @"^Map\d+\.json$", RegexOptions.IgnoreCase) || 
                                    name.Equals("CommonEvents.json", StringComparison.OrdinalIgnoreCase)))
                    .Select(name => name!)
                    .OrderBy(name => name)
                    .ToList();

                if (files.Count == 0)
                {
                    MessageBox.Show("Không tìm thấy tệp Map*.json hoặc CommonEvents.json phù hợp trong thư mục mới.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var newRowsList = new List<TranslationRow>();
                var failedFiles = new List<(string Name, string Error)>();
                var cleanFilesList = new List<string>();

                await Task.Run(() =>
                {
                    int totalFiles = files.Count;
                    int filesProcessed = 0;
                    int totalRowsParsed = 0;
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                    foreach (var file in files)
                    {
                        string fullPath = Path.Combine(newFolder, file);
                        long fileSizeKB = new FileInfo(fullPath).Length / 1024;

                        App.Current.Dispatcher.Invoke(() =>
                        {
                            BusyDetail = $"{file} ({fileSizeKB:N0} KB)";
                            BusyProgress = (double)filesProcessed / totalFiles * 100;
                        });

                        var parseResult = _parser.ParseFileResult(fullPath);
                        if (parseResult.IsSuccess)
                        {
                            var parsedRows = parseResult.Rows;
                            if (parsedRows.Count > 0)
                            {
                                newRowsList.AddRange(parsedRows);
                                cleanFilesList.Add(file);
                            }
                            totalRowsParsed += parsedRows.Count;
                        }
                        else
                        {
                            lock (failedFiles)
                            {
                                failedFiles.Add((file, parseResult.ErrorMessage ?? "Lỗi không xác định"));
                            }
                        }

                        filesProcessed++;
                        double elapsedSec = stopwatch.Elapsed.TotalSeconds;
                        double speed = elapsedSec > 0 ? (totalRowsParsed / elapsedSec) : 0;

                        App.Current.Dispatcher.Invoke(() =>
                        {
                            BusyPerformanceText = $"Thời gian: {elapsedSec:F2}s | Đã đọc: {totalRowsParsed:N0} dòng | Tốc độ: {speed:F0} dòng/giây";
                        });
                    }
                });

                if (failedFiles.Count > 0)
                {
                    string details = string.Join("\n", failedFiles.Select(f => $"- {f.Name}: {f.Error}"));
                    MessageBox.Show(
                        $"Phát hiện {failedFiles.Count} tệp tin RAW mới bị lỗi JSON/bị hỏng và bị bỏ qua:\n\n{details}",
                        "Cảnh báo tệp lỗi",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                // Build a temporary ProjectState
                var newRawProject = new ProjectState
                {
                    DataFolderPath = newFolder,
                    LoadedFiles = cleanFilesList
                };
                newRawProject.AllRows.AddRange(newRowsList);

                foreach (var row in newRawProject.AllRows)
                {
                    if (row.RowType != RowType.SectionHeader)
                    {
                        newRawProject.RowIndex[row.UniqueKey] = row;
                    }
                }

                // Perform the compare
                var diffService = new ProjectDiffService();
                var diffResult = diffService.Compare(Project, newRawProject);

                // Show report window
                var reportWindow = new DiffReportWindow
                {
                    DataContext = new DiffReportViewModel(diffResult),
                    Owner = Application.Current.MainWindow
                };

                if (reportWindow.ShowDialog() == true)
                {
                    // Carry out the merge:
                    // 1. Temporarily disable undo/redo recording during bulk update
                    TranslationRow.UndoService = null;

                    // 2. Clear old state and transfer to new RAW structure
                    Project.DataFolderPath = newFolder;
                    Project.LoadedFiles = cleanFilesList;
                    Project.RowIndex.Clear();

                    int translatableTotal = 0;
                    foreach (var row in newRowsList)
                    {
                        if (row.RowType != RowType.SectionHeader)
                        {
                            Project.RowIndex[row.UniqueKey] = row;
                            translatableTotal++;
                            row.PropertyChanged += Row_PropertyChanged;
                        }
                    }

                    Project.AllRows.ClearAndAddRange(newRowsList);

                    // Rebuild File Tree
                    FileTree.Clear();
                    var rootNode = new FileNode
                    {
                        FileName = "Data Folder",
                        FilePath = string.Empty,
                        TotalRows = translatableTotal,
                        TranslatedRows = newRowsList.Count(r => r.RowType != RowType.SectionHeader && !string.IsNullOrEmpty(r.TranslationText))
                    };

                    // Group by SourceFile to recreate treeNodes
                    var fileGroups = newRowsList
                        .Where(r => r.RowType != RowType.SectionHeader)
                        .GroupBy(r => r.SourceFile)
                        .OrderBy(g => g.Key);

                    foreach (var group in fileGroups)
                    {
                        string fileName = group.Key;
                        string fileFullPath = Path.Combine(newFolder, fileName);
                        int totalInFile = group.Count();
                        int transInFile = group.Count(r => !string.IsNullOrEmpty(r.TranslationText));

                        rootNode.Children.Add(new FileNode
                        {
                            FileName = fileName,
                            FilePath = fileFullPath,
                            IsLoaded = true,
                            TotalRows = totalInFile,
                            TranslatedRows = transInFile
                        });
                    }

                    FileTree.Add(rootNode);
                    SelectedFile = rootNode;

                    // Enable undo/redo
                    TranslationRow.UndoService = _undoRedoService;

                    // Update UI stats
                    UpdateStats();

                    // Update Settings
                    _settingsService.CurrentSettings.LastDataFolder = newFolder;
                    _settingsService.SaveSettings();

                    MessageBox.Show("Đã cập nhật dự án sang cấu trúc RAW mới thành công!", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi so sánh phiên bản game mới: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}

