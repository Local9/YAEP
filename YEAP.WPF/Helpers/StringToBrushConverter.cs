using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace YAEP.Helpers
{
    /// <summary>
    /// Converts a string color value (hex or named color) to a SolidColorBrush.
    /// </summary>
    public class StringToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorString && !string.IsNullOrWhiteSpace(colorString))
            {
                try
                {
                    Color color = (Color)ColorConverter.ConvertFromString(colorString);
                    SolidColorBrush brush = new SolidColorBrush(color);
                    brush.Freeze();
                    return brush;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"StringToBrushConverter error: {ex.Message}");
                    return new SolidColorBrush(Colors.Transparent);
                }
            }

            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                return brush.Color.ToString();
            }

            return "#00000000";
        }
    }
}

