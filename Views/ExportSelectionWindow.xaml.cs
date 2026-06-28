using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using Nelir.Models;
using Nelir.Services;
using Nelir.ViewModels;

namespace Nelir.Views
{
    public partial class ExportSelectionWindow : Window
    {
        private readonly MainViewModel _vm;
        private readonly ExportService _exportService;
        private readonly List<ExportFileItem> _fileItems;
        private bool _isUpdatingAll = false;

        public ExportSelectionWindow(MainViewModel vm, ExportService exportService)
        {
            InitializeComponent();
            _vm = vm;
            _exportService = exportService;
            Owner = Application.Current.MainWindow;

            // Generate file list with stats
            _fileItems = _vm.Project.LoadedFiles.Select(file =>
            {
                var fileRows = _vm.Project.AllRows.Where(r => r.SourceFile == file && r.RowType != RowType.SectionHeader).ToList();
                int total = fileRows.Count;
                int translated = fileRows.Count(r => !string.IsNullOrEmpty(r.TranslationText));
                return new ExportFileItem
                {
                    FileName = file,
                    TotalRows = total,
                    TranslatedRows = translated,
                    IsSelected = translated > 0 // Default to checked if translated
                };
            }).ToList();

            FilesListBox.ItemsSource = _fileItems;

            // Default Export folder: same level as data folder
            string defaultFolder = string.Empty;
            if (!string.IsNullOrEmpty(_vm.Project.DataFolderPath))
            {
                string? parentDir = Path.GetDirectoryName(_vm.Project.DataFolderPath);
                defaultFolder = Path.Combine(parentDir ?? _vm.Project.DataFolderPath, "translations_export");
            }
            ExportPathTextBox.Text = defaultFolder;

            // Initialize SelectAll Checkbox state
            UpdateSelectAllState();
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Chọn thư mục xuất dữ liệu JSON"
            };

            if (!string.IsNullOrEmpty(ExportPathTextBox.Text) && Directory.Exists(ExportPathTextBox.Text))
            {
                dialog.InitialDirectory = Path.GetFullPath(ExportPathTextBox.Text);
            }

            if (dialog.ShowDialog(this) == true)
            {
                ExportPathTextBox.Text = dialog.FolderName;
            }
        }

        private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingAll) return;
            _isUpdatingAll = true;

            foreach (var item in _fileItems)
            {
                item.IsSelected = true;
            }

            _isUpdatingAll = false;
        }

        private void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingAll) return;
            _isUpdatingAll = true;

            foreach (var item in _fileItems)
            {
                item.IsSelected = false;
            }

            _isUpdatingAll = false;
        }

        private void CheckBox_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingAll) return;
            UpdateSelectAllState();
        }

        private void UpdateSelectAllState()
        {
            _isUpdatingAll = true;

            bool allChecked = _fileItems.All(i => i.IsSelected);
            bool noneChecked = _fileItems.All(i => !i.IsSelected);

            if (allChecked)
            {
                SelectAllCheckBox.IsChecked = true;
            }
            else if (noneChecked)
            {
                SelectAllCheckBox.IsChecked = false;
            }
            else
            {
                SelectAllCheckBox.IsChecked = null; // Indeterminate
            }

            _isUpdatingAll = false;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            string exportPath = ExportPathTextBox.Text.Trim();
            if (string.IsNullOrEmpty(exportPath))
            {
                MessageBox.Show("Vui lòng chọn thư mục xuất dữ liệu.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedItems = _fileItems.Where(i => i.IsSelected).ToList();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một tệp để xuất.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Directory.CreateDirectory(exportPath);
                int count = 0;
                bool isStructured = StructuredExportRadio.IsChecked == true;

                foreach (var item in selectedItems)
                {
                    string fileOutPath = Path.Combine(exportPath, item.FileName);
                    if (isStructured)
                    {
                        _exportService.ExportFileStructured(_vm.Project, item.FileName, fileOutPath);
                    }
                    else
                    {
                        _exportService.ExportFileFlatJson(_vm.Project, item.FileName, fileOutPath);
                    }
                    count++;
                }

                MessageBox.Show($"Đã xuất thành công {count} tệp JSON bản dịch vào thư mục:\n{exportPath}", "Xuất dữ liệu thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xuất dữ liệu: {ex.Message}", "Lỗi Hệ Thống", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
