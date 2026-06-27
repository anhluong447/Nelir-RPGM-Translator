using System;
using System.Linq;
using System.Windows;

namespace Nelir.Services
{
    public class ThemeService
    {
        private const string LightThemeUri = "pack://application:,,,/Nelir;component/Resources/Themes/Light.xaml";
        private const string DarkThemeUri  = "pack://application:,,,/Nelir;component/Resources/Themes/Dark.xaml";

        public bool IsDarkMode { get; private set; }

        public void ApplyTheme(bool dark)
        {
            IsDarkMode = dark;
            var newThemeUri = dark ? DarkThemeUri : LightThemeUri;

            try
            {
                var dict = new ResourceDictionary
                {
                    Source = new Uri(newThemeUri, UriKind.Absolute)
                };

                // Find and replace the existing theme dictionary in App.Resources
                var mergedDicts = Application.Current.Resources.MergedDictionaries;
                var existing = mergedDicts.FirstOrDefault(d => d.Source != null &&
                                     (d.Source.OriginalString.Contains("Light.xaml") ||
                                      d.Source.OriginalString.Contains("Dark.xaml")));

                if (existing != null)
                {
                    int index = mergedDicts.IndexOf(existing);
                    mergedDicts[index] = dict;
                }
                else
                {
                    mergedDicts.Add(dict);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi áp dụng giao diện: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
