using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Nelir.Models;

namespace Nelir.Views
{
    public partial class FindReplaceDialog : Window
    {
        private readonly DataGrid _dataGrid;
        private bool _isInitializing = true;

        public FindReplaceDialog(DataGrid dataGrid, bool showReplace = true)
        {
            InitializeComponent();
            _dataGrid = dataGrid;
            Owner = Window.GetWindow(_dataGrid);

            _isInitializing = false;

            if (!showReplace)
            {
                MainTabControl.SelectedIndex = 0;
                Height = 260;
                Title = "Tìm kiếm (Find)";
            }
            else
            {
                MainTabControl.SelectedIndex = 1;
                Height = 325;
                Title = "Thay thế (Replace)";
            }
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            if (e.Source == MainTabControl)
            {
                if (MainTabControl.SelectedIndex == 0) // Find
                {
                    Height = 260;
                    Title = "Tìm kiếm (Find)";
                    // Sync text
                    if (FindTextBox_Find != null && FindTextBox_Replace != null)
                    {
                        FindTextBox_Find.Text = FindTextBox_Replace.Text;
                        MatchCaseCheckBox_Find.IsChecked = MatchCaseCheckBox_Replace.IsChecked;
                    }
                }
                else if (MainTabControl.SelectedIndex == 1) // Replace
                {
                    Height = 325;
                    Title = "Thay thế (Replace)";
                    // Sync text
                    if (FindTextBox_Find != null && FindTextBox_Replace != null)
                    {
                        FindTextBox_Replace.Text = FindTextBox_Find.Text;
                        MatchCaseCheckBox_Replace.IsChecked = MatchCaseCheckBox_Find.IsChecked;
                    }
                }
            }
        }

        private void FindNextButton_Click(object sender, RoutedEventArgs e)
        {
            FindNext(true);
        }

        private TranslationRow? FindNext(bool selectAndFocus)
        {
            string searchText = MainTabControl.SelectedIndex == 0 ? FindTextBox_Find.Text : FindTextBox_Replace.Text;
            if (string.IsNullOrEmpty(searchText))
            {
                return null;
            }

            var items = _dataGrid.Items.OfType<TranslationRow>().ToList();
            if (items.Count == 0)
            {
                return null;
            }

            int selectedIdx = _dataGrid.SelectedIndex;
            int start = (selectedIdx >= 0) ? selectedIdx + 1 : 0;

            bool isMatchCase = MainTabControl.SelectedIndex == 0 
                ? MatchCaseCheckBox_Find.IsChecked == true 
                : MatchCaseCheckBox_Replace.IsChecked == true;

            StringComparison comp = isMatchCase 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;

            // Search from next item to the end
            for (int i = start; i < items.Count; i++)
            {
                var row = items[i];
                if (row.RowType == RowType.SectionHeader) continue;

                if (Matches(row, searchText, comp))
                {
                    if (selectAndFocus) SelectRow(row);
                    return row;
                }
            }

            // Wrap around: search from start of list to current index
            int end = Math.Min(start, items.Count);
            for (int i = 0; i < end; i++)
            {
                var row = items[i];
                if (row.RowType == RowType.SectionHeader) continue;

                if (Matches(row, searchText, comp))
                {
                    if (selectAndFocus) SelectRow(row);
                    return row;
                }
            }

            MessageBox.Show("Không tìm thấy kết quả phù hợp.", "Tìm kiếm", MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        private bool Matches(TranslationRow row, string query, StringComparison comp)
        {
            return (row.RawText != null && row.RawText.Contains(query, comp)) ||
                   (row.MtlText != null && row.MtlText.Contains(query, comp)) ||
                   (row.TranslationText != null && row.TranslationText.Contains(query, comp));
        }

        private void SelectRow(TranslationRow row)
        {
            _dataGrid.SelectedItem = row;
            _dataGrid.ScrollIntoView(row);

            // Focus the third column (TranslationText column is index 2)
            _dataGrid.UpdateLayout();
            if (_dataGrid.Columns.Count > 2)
            {
                var cellInfo = new DataGridCellInfo(row, _dataGrid.Columns[2]);
                _dataGrid.CurrentCell = cellInfo;
            }
        }

        private void ReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            if (_dataGrid.SelectedItem is TranslationRow row && row.RowType != RowType.SectionHeader)
            {
                string searchText = FindTextBox_Replace.Text;
                string replaceText = ReplaceTextBox_Replace.Text;
                if (string.IsNullOrEmpty(searchText)) return;

                StringComparison comp = MatchCaseCheckBox_Replace.IsChecked == true 
                    ? StringComparison.Ordinal 
                    : StringComparison.OrdinalIgnoreCase;

                if (row.TranslationText != null && row.TranslationText.Contains(searchText, comp))
                {
                    row.TranslationText = ReplaceFirstOccurrence(row.TranslationText, searchText, replaceText, comp);
                }
            }
            FindNext(true);
        }

        private string ReplaceFirstOccurrence(string source, string find, string replace, StringComparison comp)
        {
            int index = source.IndexOf(find, comp);
            if (index < 0) return source;
            return source.Substring(0, index) + replace + source.Substring(index + find.Length);
        }

        private void ReplaceAllButton_Click(object sender, RoutedEventArgs e)
        {
            string searchText = FindTextBox_Replace.Text;
            string replaceText = ReplaceTextBox_Replace.Text;
            if (string.IsNullOrEmpty(searchText)) return;

            var items = _dataGrid.Items.OfType<TranslationRow>().ToList();
            int count = 0;

            StringComparison comp = MatchCaseCheckBox_Replace.IsChecked == true 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;

            foreach (var row in items)
            {
                if (row.RowType == RowType.SectionHeader) continue;

                if (row.TranslationText != null && row.TranslationText.Contains(searchText, comp))
                {
                    string text = row.TranslationText;
                    string oldText;
                    do
                    {
                        oldText = text;
                        text = ReplaceFirstOccurrence(text, searchText, replaceText, comp);
                    } while (text != oldText);

                    row.TranslationText = text;
                    count++;
                }
            }

            MessageBox.Show($"Đã thay thế thành công {count} dòng trong chế độ xem.", "Thay thế tất cả", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
