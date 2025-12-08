using Avalonia.Data.Converters;
using System.Globalization;

namespace YAEP.Helpers
{
    /// <summary>
    /// Generic enum to boolean converter. Use with parameter as the enum value string to compare against.
    /// </summary>
    internal class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (parameter is not string enumString || value == null)
            {
                return false;
            }

            if (!Enum.IsDefined(value.GetType(), value))
            {
                return false;
            }

            try
            {
                object enumValue = Enum.Parse(value.GetType(), enumString);
                return enumValue.Equals(value);
            }
            catch
            {
                return false;
            }
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (parameter is not string enumString || value is not bool boolValue || !boolValue)
            {
                throw new NotImplementedException();
            }

            if (targetType == null || !targetType.IsEnum)
            {
                throw new ArgumentException("Target type must be an enum");
            }

            return Enum.Parse(targetType, enumString);
        }
    }
}
