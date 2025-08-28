﻿using System;
using System.Globalization;
using System.Windows.Data;

namespace SimTools.Converters
{
    public sealed class EqualityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => Equals(value, parameter);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
