using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NalogParserWPF.Configuration;
using NalogParserWPF.Helpers;
using NalogParserWPF.Models;

namespace NalogParserWPF.Database
{
    /// <summary>
    /// SQLite-репозиторий истории запросов курсов валют.
    /// Работает с отдельной таблицей ExchangeHistory и не затрагивает таблицу SearchHistory.
    /// </summary>
    public class ExchangeHistoryRepository : IExchangeHistoryRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<ExchangeHistoryRepository> _logger;

        private const string CreateTableQuery = @"
            CREATE TABLE IF NOT EXISTS ExchangeHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CurrencyCode TEXT NOT NULL,
                CurrencyName TEXT,
                Date DATETIME NOT NULL,
                Rate REAL NOT NULL,
                Scale INTEGER DEFAULT 1,
                Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
            );
            CREATE INDEX IF NOT EXISTS idx_exchange_timestamp ON ExchangeHistory(Timestamp);
            CREATE INDEX IF NOT EXISTS idx_exchange_code ON ExchangeHistory(CurrencyCode);
        ";

        static ExchangeHistoryRepository()
        {
            SQLitePCL.Batteries.Init();
        }

        public ExchangeHistoryRepository(IOptions<DatabaseSettings> settings, ILogger<ExchangeHistoryRepository> logger)
        {
            _connectionString = settings.Value.ConnectionString;
            _logger = logger;
            InitializeDatabase();
        }

        private SqliteConnection CreateConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }

        private void InitializeDatabase()
        {
            try
            {
                using var connection = CreateConnection();
                using var command = new SqliteCommand(CreateTableQuery, connection);
                command.ExecuteNonQuery();
                _logger.LogInformation("Таблица ExchangeHistory инициализирована");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при инициализации таблицы ExchangeHistory");
                throw;
            }
        }

        public async Task AddAsync(ExchangeHistoryItem item, CancellationToken cancellationToken = default)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            await Task.Run(() =>
            {
                try
                {
                    using var connection = CreateConnection();
                    const string query = @"
                        INSERT INTO ExchangeHistory (CurrencyCode, CurrencyName, Date, Rate, Scale, Timestamp)
                        VALUES (@CurrencyCode, @CurrencyName, @Date, @Rate, @Scale, @Timestamp);
                    ";

                    using var command = new SqliteCommand(query, connection);
                    command.Parameters.AddWithValue("@CurrencyCode", string.IsNullOrWhiteSpace(item.CurrencyCode) ? string.Empty : item.CurrencyCode);
                    command.Parameters.AddWithValue("@CurrencyName", string.IsNullOrWhiteSpace(item.CurrencyName) ? DBNull.Value : (object)item.CurrencyName);
                    command.Parameters.AddWithValue("@Date", item.Date);
                    command.Parameters.AddWithValue("@Rate", (double)item.Rate);
                    command.Parameters.AddWithValue("@Scale", item.Scale <= 0 ? 1 : item.Scale);
                    command.Parameters.AddWithValue("@Timestamp", item.Timestamp == default ? DateTimeHelper.NowBelarus : item.Timestamp);
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при добавлении записи в историю курсов");
                    throw;
                }
            }, cancellationToken);
        }

        public async Task<List<ExchangeHistoryItem>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var items = new List<ExchangeHistoryItem>();
                try
                {
                    using var connection = CreateConnection();
                    const string query = @"
                        SELECT Id, CurrencyCode, CurrencyName, Date, Rate, Scale, Timestamp
                        FROM ExchangeHistory
                        ORDER BY Timestamp DESC, Id DESC;
                    ";

                    using var command = new SqliteCommand(query, connection);
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        items.Add(new ExchangeHistoryItem
                        {
                            Id = reader.GetInt32(0),
                            CurrencyCode = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            CurrencyName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            Date = reader.IsDBNull(3) ? default : reader.GetDateTime(3),
                            Rate = reader.IsDBNull(4) ? 0m : (decimal)reader.GetDouble(4),
                            Scale = reader.IsDBNull(5) ? 1 : reader.GetInt32(5),
                            Timestamp = reader.IsDBNull(6) ? default : reader.GetDateTime(6)
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при чтении истории курсов");
                    throw;
                }

                return items;
            }, cancellationToken);
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                try
                {
                    using var connection = CreateConnection();
                    using var command = new SqliteCommand("DELETE FROM ExchangeHistory;", connection);
                    command.ExecuteNonQuery();
                    _logger.LogInformation("История курсов очищена");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при очистке истории курсов");
                    throw;
                }
            }, cancellationToken);
        }
    }
}
