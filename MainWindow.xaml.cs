using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;
using System.Windows.Controls.Primitives;
using System.Collections.Specialized;
using Nelir.Models;
using Nelir.ViewModels;
using Nelir.Views;
using Nelir.Helpers;

namespace Nelir
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var vm = new MainViewModel();
            DataContext = vm;
            vm.ScrollToRowRequested += Vm_ScrollToRowRequested;
            MainGrid.PreviewKeyDown += MainGrid_PreviewKeyDown;

            MainGrid.Loaded += (s, e) =>
            {
                if (DataContext is MainViewModel vmModel)
                {
                    var widths = vmModel.GetColumnWidths();
                    if (widths != null && widths.Length == MainGrid.Columns.Count)
                    {
                        for (int i = 0; i < MainGrid.Columns.Count; i++)
                        {
                            if (widths[i] > 0)
                                MainGrid.Columns[i].Width = new DataGridLength(widths[i]);
                        }
                    }
                }
                AttachColumnHeaderDoubleClick();
            };

            MainGrid.Columns.CollectionChanged += (s, e) => AttachColumnHeaderDoubleClick();
        }

        private void Vm_ScrollToRowRequested(TranslationRow row)
        {
            MainGrid.SelectedItem = row;
            MainGrid.ScrollIntoView(row);
            
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                MainGrid.Focus();
                
                DataGridColumn? translatedColumn = null;
                foreach (var col in MainGrid.Columns)
                {
                    if (col.Header?.ToString()?.Contains("TRANSLATED") == true)
                    {
                        translatedColumn = col;
                        break;
                    }
                }
                
                if (translatedColumn != null)
                {
                    MainGrid.CurrentCell = new DataGridCellInfo(row, translatedColumn);
                }
            }));
        }

        private bool IsCurrentCellInEditMode()
        {
            if (Keyboard.FocusedElement is TextBox textBox)
            {
                DependencyObject current = textBox;
                while (current != null)
                {
                    if (current == MainGrid)
                        return true;
                    current = VisualTreeHelper.GetParent(current);
                }
            }
            return false;
        }

        private void MainGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                bool isEditing = IsCurrentCellInEditMode();
                
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (isEditing)
                    {
                        e.Handled = true;
                        
                        MainGrid.CommitEdit(DataGridEditingUnit.Row, true);
                        
                        int nextIndex = MainGrid.SelectedIndex + 1;
                        if (nextIndex < MainGrid.Items.Count)
                        {
                            MainGrid.SelectedIndex = nextIndex;
                            var nextItem = MainGrid.SelectedItem;
                            if (nextItem != null)
                            {
                                MainGrid.ScrollIntoView(nextItem);
                                
                                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                                {
                                    MainGrid.Focus();
                                    
                                    DataGridColumn? translatedColumn = null;
                                    foreach (var col in MainGrid.Columns)
                                    {
                                        if (col.Header?.ToString()?.Contains("TRANSLATED") == true)
                                        {
                                            translatedColumn = col;
                                            break;
                                        }
                                    }
                                    
                                    if (translatedColumn != null)
                                    {
                                        MainGrid.CurrentCell = new DataGridCellInfo(nextItem, translatedColumn);
                                        MainGrid.BeginEdit();
                                    }
                                }));
                            }
                        }
                    }
                }
                else
                {
                    if (!isEditing)
                    {
                        e.Handled = true;
                        
                        var currentItem = MainGrid.SelectedItem;
                        if (currentItem != null)
                        {
                            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                            {
                                DataGridColumn? translatedColumn = null;
                                foreach (var col in MainGrid.Columns)
                                {
                                    if (col.Header?.ToString()?.Contains("TRANSLATED") == true)
                                    {
                                        translatedColumn = col;
                                        break;
                                    }
                                }
                                
                                if (translatedColumn != null)
                                {
                                    MainGrid.CurrentCell = new DataGridCellInfo(currentItem, translatedColumn);
                                    MainGrid.BeginEdit();
                                }
                            }));
                        }
                    }
                }
            }
            else if (e.Key == Key.PageDown)
            {
                bool isEditing = IsCurrentCellInEditMode();
                if (isEditing)
                {
                    MainGrid.CommitEdit(DataGridEditingUnit.Row, true);
                }
                
                if (MainGrid.SelectedItem is TranslationRow row)
                {
                    e.Handled = true;
                    
                    if (string.IsNullOrEmpty(row.TranslationText) && !string.IsNullOrEmpty(row.MtlText))
                    {
                        row.TranslationText = row.MtlText;
                    }
                    
                    int nextIndex = MainGrid.SelectedIndex + 1;
                    if (nextIndex < MainGrid.Items.Count)
                    {
                        MainGrid.SelectedIndex = nextIndex;
                        var nextItem = MainGrid.SelectedItem;
                        if (nextItem != null)
                        {
                            MainGrid.ScrollIntoView(nextItem);
                            
                            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                            {
                                MainGrid.Focus();
                                if (isEditing)
                                {
                                    DataGridColumn? translatedColumn = null;
                                    foreach (var col in MainGrid.Columns)
                                    {
                                        if (col.Header?.ToString()?.Contains("TRANSLATED") == true)
                                        {
                                            translatedColumn = col;
                                            break;
                                        }
                                    }
                                    if (translatedColumn != null)
                                    {
                                        MainGrid.CurrentCell = new DataGridCellInfo(nextItem, translatedColumn);
                                        MainGrid.BeginEdit();
                                    }
                                }
                            }));
                        }
                    }
                }
            }
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SelectedFile = e.NewValue as FileNode;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.F)
                {
                    e.Handled = true;
                    ShowFindReplace(showReplace: false);
                }
                else if (e.Key == Key.H)
                {
                    e.Handled = true;
                    ShowFindReplace(showReplace: true);
                }
            }
        }

        private void FindReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            ShowFindReplace(showReplace: false);
        }

        private void FileMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.IsEnabled = true;
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void ToolsMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.IsEnabled = true;
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void ShowFindReplace(bool showReplace)
        {
            if (DataContext is MainViewModel vm && vm.IsProjectLoaded)
            {
                var dialog = new FindReplaceDialog(MainGrid, showReplace);
                dialog.Show();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var currentWidths = vm.GetColumnWidths();
                var widths = new double[MainGrid.Columns.Count];
                for (int i = 0; i < MainGrid.Columns.Count; i++)
                {
                    var col = MainGrid.Columns[i];
                    if (col.Visibility == Visibility.Visible)
                    {
                        widths[i] = col.ActualWidth;
                    }
                    else
                    {
                        if (currentWidths != null && i < currentWidths.Length && currentWidths[i] > 0)
                        {
                            widths[i] = currentWidths[i];
                        }
                        else
                        {
                            widths[i] = col.Width.IsAbsolute ? col.Width.Value : 100;
                        }
                    }
                }
                vm.SaveColumnWidths(widths);
                vm.SaveSettingsAndCleanup();
            }
        }

        private void AttachColumnHeaderDoubleClick()
        {
            Dispatcher.InvokeAsync(() =>
            {
                var headersPresenter = MainGrid.FindVisualChild<DataGridColumnHeadersPresenter>();
                if (headersPresenter == null) return;

                foreach (var header in headersPresenter.FindVisualChildren<DataGridColumnHeader>())
                {
                    header.MouseDoubleClick -= ColumnHeader_MouseDoubleClick;
                    header.MouseDoubleClick += ColumnHeader_MouseDoubleClick;
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void ColumnHeader_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridColumnHeader header && header.Column != null)
            {
                header.Column.Width = DataGridLength.Auto;
                MainGrid.UpdateLayout();
                header.Column.Width = new DataGridLength(header.Column.ActualWidth);
                e.Handled = true;
            }
        }
    }
}

