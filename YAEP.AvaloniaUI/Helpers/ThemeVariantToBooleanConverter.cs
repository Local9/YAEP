using Avalonia.Data.Converters;
using Avalonia.Styling;
using System.Globalization;

namespace YAEP.Helpers
{
    /// <summary>
    /// Converter for ThemeVariant to boolean. Use with parameter as "Light" or "Dark".
    /// </summary>
    internal class ThemeVariantToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is not string themeString || value is not ThemeVariant themeVariant)
            {
                return false;
            }

            return themeString switch
            {
                "Light" => themeVariant == ThemeVariant.Light,
                "Dark" => themeVariant == ThemeVariant.Dark,
                _ => false
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is not string themeString || value is not bool boolValue || !boolValue)
            {
                throw new NotImplementedException();
            }

            return themeString switch
            {
                "Light" => ThemeVariant.Light,
                "Dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Dark
            };
        }
    }
}

