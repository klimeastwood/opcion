using System;
using System.Globalization;
using System.Windows.Data;

namespace NalogParserWPF.Converters
{
    /// <summary>
    /// Переводит техническое значение типа записи («Legal» / «Physical», а также уже
    /// русифицированные варианты и «ЮЛ»/«ФЛ») в понятную русскую подпись.
    /// Используется в окне «Детали записи из истории» и в других местах вывода SearchType.
    /// </summary>
    public class SearchTypeToRussianConverter : IValueConverter
    {
        /// <summary>Русская подпись типа записи по её значению из локальной базы данных.</summary>
        public static string ToRussianText(string? searchType)
        {
            var normalized = (searchType ?? string.Empty).Trim();
            return normalized.ToLowerInvariant() switch
            {
                "legal" or "юрлицо" or "юридическое лицо" or "юл" => "Юридическое лицо",
                "physical" or "физлицо" or "физическое лицо" or "фл" => "Физическое лицо",
                "" => "Не указан",
                _ => normalized
            };
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => ToRussianText(value as string);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
