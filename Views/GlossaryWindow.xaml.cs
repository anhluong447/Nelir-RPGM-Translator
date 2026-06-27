using System.Windows;
using Nelir.Models;
using Nelir.Services;

namespace Nelir.Views
{
    public partial class GlossaryWindow : Window
    {
        private readonly GlossaryService _glossaryService;

        public GlossaryWindow(GlossaryService glossaryService)
        {
            InitializeComponent();
            _glossaryService = glossaryService;
            
            // Bind the DataGrid directly to the glossary Entries collection
            GlossaryGrid.ItemsSource = _glossaryService.Entries;
            
            Owner = Application.Current.MainWindow;
        }

        private void AddTerm_Click(object sender, RoutedEventArgs e)
        {
            var newEntry = new GlossaryEntry();
            _glossaryService.Entries.Add(newEntry);
            
            // Focus on the newly added row
            GlossaryGrid.SelectedItem = newEntry;
            GlossaryGrid.ScrollIntoView(newEntry);
        }

        private void DeleteTerm_Click(object sender, RoutedEventArgs e)
        {
            if (GlossaryGrid.SelectedItem is GlossaryEntry selectedEntry)
            {
                _glossaryService.Entries.Remove(selectedEntry);
            }
            else
            {
                MessageBox.Show("Vui lòng chọn thuật ngữ cần xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Force committing edits in the DataGrid before saving
            GlossaryGrid.CommitEdit();
            GlossaryGrid.CommitEdit();

            _glossaryService.Save();
            MessageBox.Show("Đã lưu bảng thuật ngữ thành công.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
