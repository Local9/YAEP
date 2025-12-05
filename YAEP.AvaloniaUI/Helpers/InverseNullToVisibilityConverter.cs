using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using System.Globalization;

namespace YAEP.Helpers
{
    internal class InverseNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

