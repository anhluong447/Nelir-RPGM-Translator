using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Nelir.Converters
{
    public class BoolToTextWrappingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool wrap && wrap)
            {
                return TextWrapping.Wrap;
            }
            return TextWrapping.NoWrap;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TextWrapping wrapping)
            {
                return wrapping == TextWrapping.Wrap;
            }
            return false;
        }
    }
}
