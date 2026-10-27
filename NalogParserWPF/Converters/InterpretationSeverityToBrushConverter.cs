using System;
using System.Globalization;
using System.Windows.Data;
using NalogParserWPF.Helpers;
using NalogParserWPF.Models;

namespace NalogParserWPF.Converters
{
    /// <summary>
    /// Важность записи интерпретации (<see cref="InterpretationSeverity"/>) → цвет текста
    /// или бледный фон плашки. Используется в окне «📊 Интерпретация статистических данных»:
    /// пометки годов, примечания и рекомендации окрашиваются по важности. Палитра вынесена в
    /// <see cref="InterpretationHelper"/> — те же цвета используются при экспорте в Excel и PDF.
    /// <para>
    /// <c>ConverterParameter="soft"</c> возвращает полупрозрачный фон того же цвета: рамка и
    /// пометка типа записи остаются насыщенными, а текст читается обычным цветом палитры.
    /// </para>
    /// </summary>
    public class InterpretationSeverityToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var severity = value is InterpretationSeverity parsed
                ? parsed
                : InterpretationSeverity.Normal;

            bool soft = parameter is string text
                        && text.Equals("soft", StringComparison.OrdinalIgnoreCase);

            return soft
                ? InterpretationHelper.GetSoftBrush(severity)
                : InterpretationHelper.GetBrush(severity);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
