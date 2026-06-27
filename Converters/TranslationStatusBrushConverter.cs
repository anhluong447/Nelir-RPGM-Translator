using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Nelir.Models;

namespace Nelir.Converters
{
    public class TranslationStatusBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TranslationStatus status)
            {
                string key = status switch
                {
                    TranslationStatus.Translated => "Success",
                    TranslationStatus.MtlCopied => "Warning",
                    _ => "TextTertiary"
                };

                if (Application.Current.TryFindResource(key) is object brush)
                {
                    return brush;
                }
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
