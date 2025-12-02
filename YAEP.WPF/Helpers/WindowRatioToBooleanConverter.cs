using System.Globalization;
using System.Windows.Data;

namespace YAEP.Helpers
{
    public class WindowRatioToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WindowRatio ratio)
            {
                if (parameter is string param && param == "Inverse")
                {
                    return ratio == WindowRatio.None;
                }
                return ratio != WindowRatio.None;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

