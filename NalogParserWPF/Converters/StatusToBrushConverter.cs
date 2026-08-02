using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using NalogParserWPF.Helpers;

namespace NalogParserWPF.Converters
{
    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
                return StatusHelper.GetStatusColor(status);
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
