using System.Windows.Media;

namespace NalogParserWPF.Helpers
{
    public static class StatusHelper
    {
        /// <summary>
        /// Легенда статусов проверки УНП: используется в контекстном меню колонки «Статус»
        /// вкладки «История проверок УНП».
        /// </summary>
        public const string StatusLegend =
            "Действующий — организация зарегистрирована и действует.\n" +
            "Ликвидирован — деятельность прекращена, организация исключена из ЕГР.\n" +
            "В процессе ликвидации — решение о ликвидации принято, процедура не завершена.";

        public static SolidColorBrush GetStatusColor(string status)
        {
            return status?.ToLower() switch
            {
                "активный" or "действующий" => new SolidColorBrush(Colors.Green),
                "недействующий" or "неактивный" => new SolidColorBrush(Colors.Red),
                "в процессе" or "проверяется" => new SolidColorBrush(Colors.Orange),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }

        public static string GetRussianStatus(string status)
        {
            return status?.ToLower() switch
            {
                "active" => "Активный",
                "inactive" => "Недействующий",
                "pending" => "В процессе",
                _ => status ?? "Неизвестно"
            };
        }

        /// <summary>
        /// Подсказка к плашке статуса в таблице истории: значение из реестра плюс расшифровка.
        /// </summary>
        public static string GetStatusHint(string status)
        {
            var shown = string.IsNullOrWhiteSpace(status) ? "статус не определён" : status.Trim();

            var meaning = (status ?? string.Empty).Trim().ToLower() switch
            {
                "действующий" or "активный" or "active" =>
                    "организация зарегистрирована и действует.",
                "ликвидирован" or "недействующий" or "неактивный" or "inactive" =>
                    "деятельность прекращена, организация исключена из ЕГР.",
                "в процессе ликвидации" or "в процессе" or "проверяется" or "pending" =>
                    "решение о ликвидации принято, процедура не завершена.",
                _ => "значение сохранено в локальной истории при последней проверке УНП."
            };

            return $"{shown}: {meaning}";
        }
    }
}
