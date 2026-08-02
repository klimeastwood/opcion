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
}
