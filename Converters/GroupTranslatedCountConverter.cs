using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using Nelir.Models;

namespace Nelir.Converters
{
    public class GroupTranslatedCountConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not System.Collections.IEnumerable items) return "0/0";

            int total = 0, translated = 0;
            foreach (var item in items)
            {
                if (item is not TranslationRow row) continue;
                if (row.RowType == RowType.SectionHeader) continue;
                total++;
                if (row.Status != TranslationStatus.Empty) translated++;
            }
            return $"{translated}/{total}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
