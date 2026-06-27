using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NolirRpgmTranslator.Models;

namespace NolirRpgmTranslator.Views
{
    public partial class FindReplaceDialog : Window
    {
        private readonly DataGrid _dataGrid;

        public FindReplaceDialog(DataGrid dataGrid, bool showReplace = true)
        {
            InitializeComponent();
            _dataGrid = dataGrid;
            Owner = Window.GetWindow(_dataGrid);

            if (!showReplace)
            {
                ReplaceLabel.Visibility = Visibility.Collapsed;
                ReplaceTextBox.Visibility = Visibility.Collapsed;
                ReplaceButton.Visibility = Visibility.Collapsed;
                ReplaceAllButton.Visibility = Visibility.Collapsed;
                Height = 175;
                Title = "Find";
            }
        }

        private void FindNextButton_Click(object sender, RoutedEventArgs e)
        {
            FindNext(true);
        }

        private TranslationRow? FindNext(bool selectAndFocus)
        {
            string searchText = FindTextBox.Text;
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

            StringComparison comp = MatchCaseCheckBox.IsChecked == true 
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

            MessageBox.Show("No matches found in the document.", "Search", MessageBoxButton.OK, MessageBoxImage.Information);
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
                string searchText = FindTextBox.Text;
                string replaceText = ReplaceTextBox.Text;
                if (string.IsNullOrEmpty(searchText)) return;

                StringComparison comp = MatchCaseCheckBox.IsChecked == true 
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
            string searchText = FindTextBox.Text;
            string replaceText = ReplaceTextBox.Text;
            if (string.IsNullOrEmpty(searchText)) return;

            var items = _dataGrid.Items.OfType<TranslationRow>().ToList();
            int count = 0;

            StringComparison comp = MatchCaseCheckBox.IsChecked == true 
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

            MessageBox.Show($"Successfully replaced {count} rows in the view.", "Replace All", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
