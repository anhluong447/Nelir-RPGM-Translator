using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Nelir.Models;
using Nelir.ViewModels;
using Nelir.Views;

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
                vm.SaveSettingsAndCleanup();
            }
        }
    }
}

