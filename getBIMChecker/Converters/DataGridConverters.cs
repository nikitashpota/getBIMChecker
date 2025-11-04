using System;
using System.Globalization;
using System.Windows.Data;
using getBIMChecker.Models;

namespace getBIMChecker.Converters
{
    /// <summary>
    /// Конвертер для отображения типов ошибок
    /// </summary>
    public class ErrorTypesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AxisValidationResult result)
            {
                return result.GetErrorTypesString();
            }
            return "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Конвертер для отображения смещения
    /// </summary>
    public class DeviationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AxisValidationResult result)
            {
                return result.GetDeviationString();
            }
            return "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Конвертер для отображения закрепления
    /// </summary>
    public class PinnedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AxisValidationResult result)
            {
                return result.GetPinnedString();
            }
            return "-";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
