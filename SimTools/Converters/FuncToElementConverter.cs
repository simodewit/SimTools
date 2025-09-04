using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SimTools.Converters
{
    public sealed class FuncToElementConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Func<FrameworkElement> factory)
                return factory.Invoke();
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
