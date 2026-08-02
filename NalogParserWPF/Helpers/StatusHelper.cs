using System.Windows.Media;

namespace NalogParserWPF.Helpers
{
    public static class StatusHelper
    {
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
    }
}
