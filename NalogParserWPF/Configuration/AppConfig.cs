using System;

namespace NalogParserWPF.Configuration
{
    public class ApiSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
        public int RetryCount { get; set; } = 3;
    }

    public class DatabaseSettings
    {
        public string ConnectionString { get; set; } = "Data Source=search_history.db";
    }

    /// <summary>
    /// Настройки API Национального банка Республики Беларусь (api.nbrb.by/exrates).
    /// </summary>
    public class NbrbSettings
    {
        public string BaseUrl { get; set; } = "https://api.nbrb.by/exrates/";
        public int TimeoutSeconds { get; set; } = 30;
    }

    /// <summary>
    /// Настройки публичного API Белстата (dataportal.belstat.gov.by/osids-public-api).
    /// </summary>
    public class BelstatSettings
    {
        public string BaseUrl { get; set; } = "https://dataportal.belstat.gov.by/osids-public-api/";
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>Язык ответов API (заголовок Accept-Language): ru, en или be.</summary>
        public string Language { get; set; } = "ru";
    }
}
