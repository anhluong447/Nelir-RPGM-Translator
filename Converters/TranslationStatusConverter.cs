using System;
using System.Globalization;
using System.Windows.Data;
using Nelir.Models;

namespace Nelir.Converters
{
    public class TranslationStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TranslationStatus status)
            {
                return status switch
                {
                    TranslationStatus.Translated => "✓",
                    TranslationStatus.MtlCopied => "~",
                    _ => "○"
                };
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
