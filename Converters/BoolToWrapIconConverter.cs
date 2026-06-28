using System;
using System.Globalization;
using System.Windows.Data;

namespace Nelir.Converters
{
    public class BoolToWrapIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool wrap && wrap)
            {
                return "↵ Wrap";
            }
            return "→ No Wrap";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
