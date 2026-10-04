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
    /// SQLite-репозиторий истории запросов государственной статистики.
    /// Работает с отдельной таблицей BelstatHistory и не затрагивает таблицы SearchHistory и ExchangeHistory.
    /// </summary>
    public class BelstatHistoryRepository : IBelstatHistoryRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<BelstatHistoryRepository> _logger;

        private const string CreateTableQuery = @"
            CREATE TABLE IF NOT EXISTS BelstatHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RubricCode TEXT,
                IndicatorCode TEXT NOT NULL,
                IndicatorName TEXT,
                Years TEXT NOT NULL,
                Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
            );
            CREATE INDEX IF NOT EXISTS idx_belstat_timestamp ON BelstatHistory(Timestamp);
            CREATE INDEX IF NOT EXISTS idx_belstat_indicator ON BelstatHistory(IndicatorCode);
        ";

        static BelstatHistoryRepository()
        {
            SQLitePCL.Batteries.Init();
        }

        public BelstatHistoryRepository(IOptions<DatabaseSettings> settings, ILogger<BelstatHistoryRepository> logger)
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
                _logger.LogInformation("Таблица BelstatHistory инициализирована");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при инициализации таблицы BelstatHistory");
                throw;
            }
        }

        public async Task AddAsync(BelstatHistoryItem item, CancellationToken cancellationToken = default)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            await Task.Run(() =>
            {
                try
                {
                    using var connection = CreateConnection();
                    const string query = @"
                        INSERT INTO BelstatHistory (RubricCode, IndicatorCode, IndicatorName, Years, Timestamp)
                        VALUES (@RubricCode, @IndicatorCode, @IndicatorName, @Years, @Timestamp);
                    ";

                    using var command = new SqliteCommand(query, connection);
                    command.Parameters.AddWithValue("@RubricCode", string.IsNullOrWhiteSpace(item.RubricCode) ? DBNull.Value : (object)item.RubricCode);
                    command.Parameters.AddWithValue("@IndicatorCode", string.IsNullOrWhiteSpace(item.IndicatorCode) ? string.Empty : item.IndicatorCode);
                    command.Parameters.AddWithValue("@IndicatorName", string.IsNullOrWhiteSpace(item.IndicatorName) ? DBNull.Value : (object)item.IndicatorName);
                    command.Parameters.AddWithValue("@Years", string.IsNullOrWhiteSpace(item.Years) ? string.Empty : item.Years);
                    command.Parameters.AddWithValue("@Timestamp", item.Timestamp == default ? DateTimeHelper.NowBelarus : item.Timestamp);
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при добавлении записи в историю статистики");
                    throw;
                }
            }, cancellationToken);
        }

        public async Task<List<BelstatHistoryItem>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var items = new List<BelstatHistoryItem>();
                try
                {
                    using var connection = CreateConnection();
                    const string query = @"
                        SELECT Id, RubricCode, IndicatorCode, IndicatorName, Years, Timestamp
                        FROM BelstatHistory
                        ORDER BY Timestamp DESC, Id DESC;
                    ";

                    using var command = new SqliteCommand(query, connection);
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        items.Add(new BelstatHistoryItem
                        {
                            Id = reader.GetInt32(0),
                            RubricCode = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            IndicatorCode = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            IndicatorName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                            Years = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                            Timestamp = reader.IsDBNull(5) ? default : reader.GetDateTime(5)
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при чтении истории статистики");
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
                    using var command = new SqliteCommand("DELETE FROM BelstatHistory;", connection);
                    command.ExecuteNonQuery();
                    _logger.LogInformation("История статистики очищена");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при очистке истории статистики");
                    throw;
                }
            }, cancellationToken);
        }
    }
}
