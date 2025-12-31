using Avalonia.Data.Converters;
using System.Globalization;
using YAEP.Services;

namespace YAEP.Helpers
{
    public class CanDeleteConverter : IValueConverter
    {
        public static DatabaseService? DatabaseService { get; set; }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (DatabaseService == null || value == null)
                return true;

            if (value is DatabaseService.Profile profile)
            {
                return !DatabaseService.IsDefaultProfile(profile.Id);
            }

            if (value is DatabaseService.ClientGroup group)
            {
                return !DatabaseService.IsDefaultGroup(group.Id);
            }

            return true;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

