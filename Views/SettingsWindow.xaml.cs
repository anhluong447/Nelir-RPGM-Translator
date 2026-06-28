using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Nelir.ViewModels;

namespace Nelir.Views
{
    public class CharacterEntry
    {
        public string BustId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public partial class SettingsWindow : Window
    {
        private MainViewModel? _mainViewModel;
        private ObservableCollection<CharacterEntry>? _registryEntries;

        public SettingsWindow()
        {
            InitializeComponent();
            Loaded += SettingsWindow_Loaded;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                _mainViewModel = vm;
                _registryEntries = new ObservableCollection<CharacterEntry>(
                    _mainViewModel.CharacterRegistry.Registry.Select(kv => new CharacterEntry
                    {
                        BustId = kv.Key,
                        DisplayName = kv.Value
                    })
                );
                RegistryGrid.ItemsSource = _registryEntries;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SaveRegistry_Click(object sender, RoutedEventArgs e)
        {
            if (_mainViewModel == null || _registryEntries == null) return;

            // Clear and rebuild registry
            var registry = _mainViewModel.CharacterRegistry;
            registry.Registry.Clear();

            foreach (var entry in _registryEntries)
            {
                if (!string.IsNullOrWhiteSpace(entry.BustId))
                {
                    registry.Register(entry.BustId.Trim(), entry.DisplayName?.Trim() ?? string.Empty);
                }
            }

            registry.Save();
            MessageBox.Show("Đã lưu danh sách nhân vật (Bust Registry) thành công!", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
