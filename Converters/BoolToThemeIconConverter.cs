using System;
using System.Globalization;
using System.Windows.Data;

namespace Nelir.Converters
{
    public class BoolToThemeIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isDark && isDark)
            {
                return "🌙";
            }
            return "☀️";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
