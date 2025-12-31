using Avalonia.Data.Converters;
using System.Globalization;

namespace YAEP.Helpers
{
    /// <summary>
    /// Converts an integer to a uniform Thickness (all sides equal).
    /// </summary>
    public class IntToThicknessConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int thickness)
            {
                return new Thickness(thickness);
            }

            return new Thickness(0);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Thickness thickness)
            {
                return (int)thickness.Left;
            }

            return 0;
        }
    }
}

