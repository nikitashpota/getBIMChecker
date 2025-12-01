using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using getBIMChecker.Models;

namespace getBIMChecker.Converters
{
    public class LevelErrorTypesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is LevelValidationResult result ? result.GetErrorTypesString() : "-";
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class LevelDeviationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is LevelValidationResult result ? result.GetDeviationString() : "-";
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class LevelPinnedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is LevelValidationResult result ? result.GetPinnedString() : "-";
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class LevelCanFixConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LevelValidationResult result)
            {
                if (!result.HasErrors) return false;
                return !result.ErrorTypes.All(e => e == LevelErrorType.NotInReference);
            }
            return false;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}