using Avalonia.Data.Converters;
using System.Globalization;

namespace YAEP.Helpers
{
    public class WindowRatioDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WindowRatio ratio)
            {
                return ratio switch
                {
                    WindowRatio.None => "None (Manual)",
                    WindowRatio.Ratio21_9 => "21:9",
                    WindowRatio.Ratio21_4 => "21:4",
                    WindowRatio.Ratio16_9 => "16:9",
                    WindowRatio.Ratio4_3 => "4:3",
                    WindowRatio.Ratio1_1 => "1:1",
                    _ => value.ToString() ?? "Unknown"
                };
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

