using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NalogParserWPF.Converters
{
    /// <summary>
    /// Скрывает элемент, если значение пустое (null, пустая строка или пробелы).
    /// Используется, чтобы не отображать «пустые» цветные плашки статуса.
    /// </summary>
    public class EmptyStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value as string)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
