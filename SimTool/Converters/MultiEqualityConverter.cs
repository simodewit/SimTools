using System;
using System.Globalization;
using System.Windows.Data;

namespace SimTools.Converters
{
    /// <summary>
    /// Returns true when both inputs are equal (object.Equals).
    /// Use with:
    ///   <MultiBinding Converter="{StaticResource MultiEqualityConverter}">
    ///       <Binding .../>  <!-- A -->
    ///       <Binding .../>  <!-- B -->
    ///   </MultiBinding>
    /// </summary>
    public sealed class MultiEqualityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return false;
            return Equals(values[0], values[1]);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => Binding.DoNothing as object[];
    }
}
