using System.Windows;
using System.Windows.Input;
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
            DataContext = new MainViewModel();
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
            // Ctrl+F -> Find, Ctrl+H -> Replace
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

