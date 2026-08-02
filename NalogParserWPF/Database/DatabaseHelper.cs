using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NalogParserWPF.Configuration;
using NalogParserWPF.Models;
using NalogParserWPF.Services;

namespace NalogParserWPF.Database
{
    public class DatabaseHelper : IHistoryRepository
    {
        private static readonly ILogger<DatabaseHelper> FallbackLogger =
            LoggerFactory.Create(builder => builder.AddDebug()).CreateLogger<DatabaseHelper>();

        private readonly string _connectionString;
        private readonly ILogger<DatabaseHelper> _logger;

        static DatabaseHelper()
        {
            SQLitePCL.Batteries.Init();
        }

        public DatabaseHelper(IOptions<DatabaseSettings> settings, ILogger<DatabaseHelper> logger)
        {
            _connectionString = settings.Value.ConnectionString;
            _logger = logger;
            InitializeDatabase();
        }

        // Backward-compatible shim для старых окон (new DatabaseHelper()); удалить после Шага 3.
        public DatabaseHelper()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            _connectionString = config.GetSection("DatabaseSettings").Get<DatabaseSettings>()?.ConnectionString
                                ?? new DatabaseSettings().ConnectionString;
            _logger = FallbackLogger;
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
            _logger.LogInformation("Инициализация базы данных: {ConnectionString}", _connectionString);

            using var connection = CreateConnection();

            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS SearchHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SearchDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                    SearchType TEXT NOT NULL,
                    SearchParams TEXT NOT NULL,
                    ResultData TEXT,
                    Unp TEXT,
                    FullName TEXT,
                    Status TEXT,
                    IsActive INTEGER DEFAULT 0,
                    PreviousStatus TEXT,
                    LastChecked DATETIME
                );

                CREATE INDEX IF NOT EXISTS idx_search_date ON SearchHistory(SearchDate);
                CREATE INDEX IF NOT EXISTS idx_unp ON SearchHistory(Unp);
                CREATE INDEX IF NOT EXISTS idx_status ON SearchHistory(Status);
            ";

            using var command = new SqliteCommand(createTableQuery, connection);
            command.ExecuteNonQuery();
        }

        private static SearchHistory MapSearchHistory(SqliteDataReader reader)
        {
            return new SearchHistory
            {
                Id = Convert.ToInt32(reader["Id"]),
                SearchDate = Convert.ToDateTime(reader["SearchDate"]),
                SearchType = reader["SearchType"].ToString(),
                SearchParams = reader["SearchParams"].ToString(),
                ResultData = reader["ResultData"]?.ToString(),
                Unp = reader["Unp"]?.ToString(),
                FullName = reader["FullName"]?.ToString(),
                Status = reader["Status"]?.ToString(),
                IsActive = Convert.ToBoolean(reader["IsActive"]),
                PreviousStatus = reader["PreviousStatus"]?.ToString(),
                LastChecked = reader["LastChecked"] != DBNull.Value ? Convert.ToDateTime(reader["LastChecked"]) : (DateTime?)null
            };
        }

        public int AddHistory(SearchHistory history)
        {
            try
            {
                _logger.LogInformation("Добавление записи в историю, УНП: {Unp}", history.Unp);

                using var connection = CreateConnection();

                string query = @"
                    INSERT INTO SearchHistory 
                    (SearchType, SearchParams, ResultData, Unp, FullName, Status, IsActive, PreviousStatus, LastChecked)
                    VALUES (@SearchType, @SearchParams, @ResultData, @Unp, @FullName, @Status, @IsActive, @PreviousStatus, @LastChecked);
                    SELECT last_insert_rowid();
                ";

                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@SearchType", history.SearchType);
                command.Parameters.AddWithValue("@SearchParams", history.SearchParams);
                command.Parameters.AddWithValue("@ResultData", history.ResultData ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Unp", history.Unp ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@FullName", history.FullName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@Status", history.Status ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@IsActive", history.IsActive ? 1 : 0);
                command.Parameters.AddWithValue("@PreviousStatus", history.PreviousStatus ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@LastChecked", DateTime.Now);

                return Convert.ToInt32(command.ExecuteScalar());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении записи в историю");
                throw;
            }
        }

        public List<HistoryItem> GetHistory(int limit = 1000)
        {
            try
            {
                _logger.LogInformation("Загрузка истории, лимит: {Limit}", limit);

                var history = new List<HistoryItem>();

                using var connection = CreateConnection();

                string query = @"
                    SELECT Id, SearchDate, SearchType, SearchParams, FullName, Unp, Status, IsActive, PreviousStatus
                    FROM SearchHistory
                    ORDER BY SearchDate DESC
                    LIMIT @Limit
                ";

                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@Limit", limit);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string searchType = reader["SearchType"].ToString();

                    var item = new HistoryItem
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        SearchDate = Convert.ToDateTime(reader["SearchDate"]).ToString("dd.MM.yyyy HH:mm"),
                        Type = searchType == "Legal" ? "Юрлицо/ИП" : "Физлицо",
                        SearchParams = reader["SearchParams"].ToString(),
                        FullName = reader["FullName"]?.ToString() ?? "-",
                        Unp = reader["Unp"]?.ToString() ?? "-",
                        Status = reader["Status"]?.ToString() ?? "-",
                        IsActive = Convert.ToBoolean(reader["IsActive"]),
                        PreviousStatus = reader["PreviousStatus"]?.ToString()
                    };

                    if (item.Status == "Действующий" || item.Status == "Активный")
                    {
                        item.StatusColor = "Green";
                    }
                    else if (item.Status == "Ликвидирован" || item.Status == "Исключен")
                    {
                        item.StatusColor = "Red";
                    }
                    else
                    {
                        item.StatusColor = "Orange";
                    }

                    history.Add(item);
                }

                return history;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке истории");
                throw;
            }
        }

        public SearchHistory GetHistoryById(int id)
        {
            try
            {
                _logger.LogInformation("Загрузка записи истории по Id: {Id}", id);

                using var connection = CreateConnection();

                string query = "SELECT * FROM SearchHistory WHERE Id = @Id";
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@Id", id);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return MapSearchHistory(reader);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке записи по Id: {Id}", id);
                throw;
            }
        }

        public List<SearchHistory> GetHistoryByUnp(string unp)
        {
            try
            {
                _logger.LogInformation("Загрузка истории по УНП: {Unp}", unp);

                var history = new List<SearchHistory>();

                using var connection = CreateConnection();

                string query = "SELECT * FROM SearchHistory WHERE Unp = @Unp ORDER BY SearchDate DESC";
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@Unp", unp);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    history.Add(MapSearchHistory(reader));
                }

                return history;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке истории по УНП: {Unp}", unp);
                throw;
            }
        }

        private List<SearchHistory> GetAllCore()
        {
            var history = new List<SearchHistory>();

            using var connection = CreateConnection();

            string query = "SELECT * FROM SearchHistory ORDER BY SearchDate DESC";
            using var command = new SqliteCommand(query, connection);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                history.Add(MapSearchHistory(reader));
            }

            return history;
        }

        public int GetHistoryCount()
        {
            try
            {
                using var connection = CreateConnection();

                string query = "SELECT COUNT(*) FROM SearchHistory";
                using var command = new SqliteCommand(query, connection);
                return Convert.ToInt32(command.ExecuteScalar());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при подсчете количества записей истории");
                throw;
            }
        }

        public void DeleteHistory(int id)
        {
            try
            {
                _logger.LogInformation("Удаление записи истории, Id: {Id}", id);

                using var connection = CreateConnection();

                string query = "DELETE FROM SearchHistory WHERE Id = @Id";
                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@Id", id);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении записи истории, Id: {Id}", id);
                throw;
            }
        }

        public void ClearHistory()
        {
            try
            {
                _logger.LogWarning("Очистка всей истории поиска");

                using var connection = CreateConnection();

                string query = "DELETE FROM SearchHistory";
                using var command = new SqliteCommand(query, connection);
                command.ExecuteNonQuery();

                command.CommandText = "DELETE FROM sqlite_sequence WHERE name='SearchHistory'";
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при очистке истории поиска");
                throw;
            }
        }

        public void UpdateStatus(int id, string status, bool isActive, string previousStatus)
        {
            try
            {
                _logger.LogInformation("Обновление статуса записи, Id: {Id}, Статус: {Status}", id, status);

                using var connection = CreateConnection();

                string query = @"
                    UPDATE SearchHistory 
                    SET Status = @Status, 
                        IsActive = @IsActive, 
                        PreviousStatus = @PreviousStatus,
                        LastChecked = @LastChecked
                    WHERE Id = @Id
                ";

                using var command = new SqliteCommand(query, connection);
                command.Parameters.AddWithValue("@Id", id);
                command.Parameters.AddWithValue("@Status", status ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@IsActive", isActive ? 1 : 0);
                command.Parameters.AddWithValue("@PreviousStatus", previousStatus ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@LastChecked", DateTime.Now);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении статуса записи, Id: {Id}", id);
                throw;
            }
        }

        public List<SearchHistory> GetActiveUnps()
        {
            try
            {
                _logger.LogInformation("Загрузка активных УНП для автопроверки");

                var history = new List<SearchHistory>();

                using var connection = CreateConnection();

                string query = @"
                    SELECT * FROM SearchHistory 
                    WHERE Id IN (
                        SELECT MAX(Id) FROM SearchHistory 
                        WHERE Unp IS NOT NULL AND Unp != ''
                        GROUP BY Unp
                    )
                    AND IsActive = 1
                ";

                using var command = new SqliteCommand(query, connection);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    history.Add(MapSearchHistory(reader));
                }

                return history;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке активных УНП");
                throw;
            }
        }

        // --- Реализация IHistoryRepository ---

        public async Task AddAsync(SearchHistory entry, CancellationToken cancellationToken = default)
        {
            await Task.Run(() => AddHistory(entry), cancellationToken);
        }

        public async Task<List<SearchHistory>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(GetAllCore, cancellationToken);
        }

        public async Task<SearchHistory?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => GetHistoryById(id), cancellationToken);
        }

        public async Task<List<SearchHistory>> GetByUnpAsync(string unp, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => GetHistoryByUnp(unp), cancellationToken);
        }

        public async Task UpdateStatusAsync(int id, string newStatus, CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                var existing = GetHistoryById(id);
                if (existing == null) return;
                UpdateStatus(id, newStatus, newStatus == "Действующий", existing.Status);
            }, cancellationToken);
        }

        public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            await Task.Run(() => DeleteHistory(id), cancellationToken);
        }

        public async Task ClearAllAsync(CancellationToken cancellationToken = default)
        {
            await Task.Run(ClearHistory, cancellationToken);
        }

        public async Task<List<SearchHistory>> GetActiveUnpsAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(GetActiveUnps, cancellationToken);
        }
    }
}
