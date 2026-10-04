using System;

namespace NalogParserWPF.Helpers
{
    /// <summary>
    /// Единая точка получения времени для всего приложения.
    /// Все временные метки ведутся по локальному времени Республики Беларусь (UTC+3).
    /// </summary>
    public static class DateTimeHelper
    {
        /// <summary>Смещение локального времени Республики Беларусь относительно UTC.</summary>
        public static TimeSpan BelarusOffset { get; } = TimeSpan.FromHours(3);

        /// <summary>Текущее локальное время Республики Беларусь (UTC+3).</summary>
        public static DateTime NowBelarus => DateTime.UtcNow.Add(BelarusOffset);

        /// <summary>Текущая дата по локальному времени Республики Беларусь (UTC+3).</summary>
        public static DateTime TodayBelarus => NowBelarus.Date;
    }
}
