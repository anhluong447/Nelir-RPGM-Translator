using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Nelir.Models;

namespace Nelir.Converters
{
    public class RowTypeColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RowType type)
            {
                switch (type)
                {
                    case RowType.SectionHeader:
                        return Application.Current.TryFindResource("SecondaryAccentMuted") as Brush ?? Brushes.Purple;
                    case RowType.Choice:
                        return Application.Current.TryFindResource("WarningMuted") as Brush ?? Brushes.DarkGoldenrod;
                    case RowType.Comment:
                        return Application.Current.TryFindResource("BgCrust") as Brush ?? Brushes.DarkGray;
                    case RowType.Dialog:
                    default:
                        return Brushes.Transparent; // Falls back to default alternating row background
                }
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

