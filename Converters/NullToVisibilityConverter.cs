using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Nelir.Converters
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return Visibility.Collapsed;
            }

            if (value is bool b && !b)
            {
                return Visibility.Collapsed;
            }

            if (value is string str && string.IsNullOrEmpty(str))
            {
                return Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = true;
            if (value is bool b)
            {
                isVisible = b;
            }

            if (parameter?.ToString()?.Equals("invert", StringComparison.OrdinalIgnoreCase) == true)
            {
                isVisible = !isVisible;
            }

            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
