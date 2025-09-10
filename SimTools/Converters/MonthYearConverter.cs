using System;
using System.Globalization;
using System.Windows.Data;

namespace SimTools.Converters
{
    public class MonthYearConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(value is DateTime dt)
            {
                // Ensure local time on grouping to match the displayed values
                if(dt.Kind == DateTimeKind.Utc) dt = dt.ToLocalTime();
                return dt.ToString("MMMM yyyy", culture);
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
