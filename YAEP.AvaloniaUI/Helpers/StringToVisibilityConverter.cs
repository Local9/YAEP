using Avalonia.Data.Converters;
using System.Globalization;

namespace YAEP.Helpers
{
    internal class StringToVisibilityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string? str = value as string;
            return !string.IsNullOrWhiteSpace(str);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

