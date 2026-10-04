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
using NalogParserWPF.Helpers;
using NalogParserWPF.Models;
using NalogParserWPF.Services;

namespace NalogParserWPF.Database
{
    public class DatabaseHelper : IHistoryRepository
    {
        private const int MaxBusyRetryAttempts = 3;
        private static readonly TimeSpan BusyRetryDelay = TimeSpan.FromMilliseconds(100);

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

        private const string CreateTableQuery = @"
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
                LastChecked DATETIME,
                UpdateInfo TEXT
            );
        ";

        private const string CreateIndexesQuery = @"
            CREATE INDEX IF NOT EXISTS idx_search_date ON SearchHistory(SearchDate);
            CREATE INDEX IF NOT EXISTS idx_unp ON SearchHistory(Unp);
            CREATE INDEX IF NOT EXISTS idx_status ON SearchHistory(Status);
        ";

        private void InitializeDatabase()
        {
            _logger.LogInformation("Инициализация базы данных: {ConnectionString}", _connectionString);

            try
            {
                ExecuteWithRetry(RunMigrations, "InitializeDatabase");
            }
            catch (SqliteException ex)
            {
                _logger.LogError(ex,
                    "Миграция БД не удалась (код {Code}): {Message}. Будет выполнен сброс файла базы данных.",
                    ex.SqliteErrorCode, ex.Message);

                ResetDatabaseFile();
                ExecuteWithRetry(RunMigrations, "InitializeDatabase-Recovered");
            }
        }

        private bool RunMigrations()
        {
            using var connection = CreateConnection();

            _logger.LogInformation("Миграция БД [1/3]: создание таблицы SearchHistory (если не существует)");
            ExecuteNonQuery(connection, CreateTableQuery, "CreateTable");

            _logger.LogInformation("Миграция БД [2/3]: добавление колонки UpdateInfo (если не существует)");
            EnsureColumnExists(connection, "UpdateInfo", "TEXT");

            _logger.LogInformation("Миграция БД [3/3]: создание индексов");
            ExecuteNonQuery(connection, CreateIndexesQuery, "CreateIndexes");

            _logger.LogInformation("Миграция БД завершена успешно");
            return true;
        }

        private static void ExecuteNonQuery(SqliteConnection connection, string query, string step)
        {
            using var command = new SqliteCommand(query, connection);
            command.ExecuteNonQuery();
        }

        private void ResetDatabaseFile()
        {
            try
            {
                var builder = new SqliteConnectionStringBuilder(_connectionString);
                string? dbPath = builder.DataSource;
                if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
                {
                    _logger.LogWarning("Файл базы данных не найден, сброс пропущен");
                    return;
                }

                string backupPath = dbPath + $".backup_{DateTimeHelper.NowBelarus:yyyyMMdd_HHmmss}";
                File.Copy(dbPath, backupPath, overwrite: true);
                File.Delete(dbPath);
                _logger.LogWarning("Файл базы данных сброшен. Бэкап сохранен: {Backup}", backupPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось сбросить файл базы данных");
            }
        }

        private T ExecuteWithRetry<T>(Func<T> operation, string operationName)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return operation();
                }
                catch (SqliteException ex) when (IsBusyOrLocked(ex))
                {
                    if (attempt >= MaxBusyRetryAttempts)
                    {
                        _logger.LogError(ex,
                            "Операция '{Operation}' не выполнена: база данных заблокирована/занята (код {Code}) после {Attempts} попыток",
                            operationName, ex.SqliteErrorCode, MaxBusyRetryAttempts);
                        throw;
                    }

                    int delayMs = (int)(BusyRetryDelay * attempt).TotalMilliseconds;
                    _logger.LogWarning(ex,
                        "Операция '{Operation}': база данных заблокирована/занята (код {Code}), попытка {Attempt}/{Max} через {Delay} мс",
                        operationName, ex.SqliteErrorCode, attempt, MaxBusyRetryAttempts, delayMs);
                    Thread.Sleep(delayMs);
                }
            }
        }

        private static bool IsBusyOrLocked(SqliteException ex)
        {
            // SQLITE_BUSY = 5, SQLITE_LOCKED = 6
            return ex.SqliteErrorCode is 5 or 6;
        }

        private static bool IsConstraintViolation(SqliteException ex)
        {
            // SQLITE_CONSTRAINT = 19
            return ex.SqliteErrorCode == 19;
        }

        // Миграция для уже существующих баз: добавляет колонку, если ее нет.
        // Сначала проверка (DoesColumnExist), затем ALTER TABLE только при необходимости.
        private void EnsureColumnExists(SqliteConnection connection, string columnName, string columnDefinition)
        {
            if (DoesColumnExist(connection, "SearchHistory", columnName))
            {
                _logger.LogInformation("Колонка '{Column}' уже существует, ALTER TABLE пропущен", columnName);
                return;
            }

            try
            {
                using var alterCommand = new SqliteCommand(
                    $"ALTER TABLE SearchHistory ADD COLUMN {columnName} {columnDefinition}",
                    connection);
                alterCommand.ExecuteNonQuery();
                _logger.LogInformation("Колонка '{Column}' добавлена через ALTER TABLE", columnName);
            }
            catch (SqliteException ex)
            {
                // Если колонка была добавлена конкурентно, пропускаем ошибку и продолжаем
                if (DoesColumnExist(connection, "SearchHistory", columnName))
                {
                    _logger.LogWarning(ex, "ALTER TABLE для '{Column}' упал, но колонка уже существует", columnName);
                    return;
                }

                _logger.LogError(ex, "ALTER TABLE для '{Column}' не выполнен (код {Code})", columnName, ex.SqliteErrorCode);
                throw;
            }
        }

        private static bool DoesColumnExist(SqliteConnection connection, string tableName, string columnName)
        {
            using var command = new SqliteCommand(
                "SELECT COUNT(*) FROM pragma_table_info($table) WHERE name = $column",
                connection);
            command.Parameters.AddWithValue("$table", tableName);
            command.Parameters.AddWithValue("$column", columnName);

            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        private static SearchHistory MapRowToSearchHistory(SqliteDataReader reader)
        {
            return new SearchHistory
            {
                Id = Convert.ToInt32(reader["Id"]),
                SearchDate = Convert.ToDateTime(reader["SearchDate"]),
                SearchType = reader["SearchType"].ToString() ?? string.Empty,
                SearchParams = reader["SearchParams"].ToString() ?? string.Empty,
                ResultData = reader["ResultData"]?.ToString() ?? string.Empty,
                Unp = reader["Unp"]?.ToString() ?? string.Empty,
                FullName = reader["FullName"]?.ToString() ?? string.Empty,
                Status = reader["Status"]?.ToString() ?? string.Empty,
                IsActive = Convert.ToBoolean(reader["IsActive"]),
                PreviousStatus = reader["PreviousStatus"]?.ToString() ?? string.Empty,
                LastChecked = reader["LastChecked"] != DBNull.Value ? Convert.ToDateTime(reader["LastChecked"]) : (DateTime?)null,
                UpdateInfo = reader["UpdateInfo"]?.ToString()
            };
        }

        public int AddHistory(SearchHistory history)
        {
            try
            {
                _logger.LogInformation("Добавление записи в историю, УНП: {Unp}", history.Unp);

                return ExecuteWithRetry(() =>
                {
                    using var connection = CreateConnection();

                    string query = @"
                        INSERT INTO SearchHistory 
                        (SearchType, SearchParams, ResultData, Unp, FullName, Status, IsActive, PreviousStatus, LastChecked, UpdateInfo, SearchDate)
                        VALUES (@SearchType, @SearchParams, @ResultData, @Unp, @FullName, @Status, @IsActive, @PreviousStatus, @LastChecked, @UpdateInfo, @SearchDate);
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
                    command.Parameters.AddWithValue("@LastChecked", DateTimeHelper.NowBelarus);
                    command.Parameters.AddWithValue("@UpdateInfo", history.UpdateInfo ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@SearchDate", DateTimeHelper.NowBelarus);

                    return Convert.ToInt32(command.ExecuteScalar());
                }, "AddHistory");
            }
            catch (SqliteException ex) when (IsConstraintViolation(ex))
            {
                _logger.LogError(ex, "Ошибка целостности данных (код {Code}) при добавлении записи в историю", ex.SqliteErrorCode);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении записи в историю");
                throw;
            }
        }

        public SearchHistory? GetHistoryById(int id)
        {
            try
            {
                _logger.LogInformation("Загрузка записи истории по Id: {Id}", id);

                return ExecuteWithRetry(() =>
                {
                    using var connection = CreateConnection();

                    string query = "SELECT * FROM SearchHistory WHERE Id = @Id";
                    using var command = new SqliteCommand(query, connection);
                    command.Parameters.AddWithValue("@Id", id);

                    using var reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        return MapRowToSearchHistory(reader);
                    }

                    return null;
                }, "GetHistoryById");
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

                return ExecuteWithRetry(() =>
                {
                    var history = new List<SearchHistory>();

                    using var connection = CreateConnection();

                    string query = "SELECT * FROM SearchHistory WHERE Unp = @Unp ORDER BY SearchDate DESC";
                    using var command = new SqliteCommand(query, connection);
                    command.Parameters.AddWithValue("@Unp", unp);

                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        history.Add(MapRowToSearchHistory(reader));
                    }

                    return history;
                }, "GetHistoryByUnp");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке истории по УНП: {Unp}", unp);
                throw;
            }
        }

        private List<SearchHistory> GetAllCore()
        {
            return ExecuteWithRetry(() =>
            {
                var history = new List<SearchHistory>();

                using var connection = CreateConnection();

                string query = "SELECT * FROM SearchHistory ORDER BY SearchDate DESC";
                using var command = new SqliteCommand(query, connection);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    history.Add(MapRowToSearchHistory(reader));
                }

                return history;
            }, "GetAll");
        }

        public int GetHistoryCount()
        {
            try
            {
                return ExecuteWithRetry(() =>
                {
                    using var connection = CreateConnection();

                    string query = "SELECT COUNT(*) FROM SearchHistory";
                    using var command = new SqliteCommand(query, connection);
                    return Convert.ToInt32(command.ExecuteScalar());
                }, "GetHistoryCount");
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

                ExecuteWithRetry(() =>
                {
                    using var connection = CreateConnection();

                    string query = "DELETE FROM SearchHistory WHERE Id = @Id";
                    using var command = new SqliteCommand(query, connection);
                    command.Parameters.AddWithValue("@Id", id);
                    command.ExecuteNonQuery();
                    return true;
                }, "DeleteHistory");
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

                ExecuteWithRetry(() =>
                {
                    using var connection = CreateConnection();

                    string query = "DELETE FROM SearchHistory";
                    using var command = new SqliteCommand(query, connection);
                    command.ExecuteNonQuery();

                    command.CommandText = "DELETE FROM sqlite_sequence WHERE name='SearchHistory'";
                    command.ExecuteNonQuery();
                    return true;
                }, "ClearHistory");
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

                ExecuteWithRetry(() =>
                {
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
                    command.Parameters.AddWithValue("@LastChecked", DateTimeHelper.NowBelarus);
                    command.ExecuteNonQuery();
                    return true;
                }, "UpdateStatus");
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

                return ExecuteWithRetry(() =>
                {
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
                        history.Add(MapRowToSearchHistory(reader));
                    }

                    return history;
                }, "GetActiveUnps");
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

        public async Task UpdateStatusAndInfoAsync(int id, string newStatus, string updateInfo, CancellationToken cancellationToken = default)
        {
            await Task.Run(() => UpdateStatusAndInfo(id, newStatus, updateInfo), cancellationToken);
        }

        public void UpdateStatusAndInfo(int id, string newStatus, string updateInfo)
        {
            try
            {
                _logger.LogInformation("Обновление статуса и информации об изменениях записи, Id: {Id}, Статус: {Status}", id, newStatus);

                ExecuteWithRetry(() =>
                {
                    var existing = GetHistoryById(id);
                    if (existing == null) return true;

                    using var connection = CreateConnection();

                    string query = @"
                        UPDATE SearchHistory 
                        SET Status = @Status, 
                            IsActive = @IsActive, 
                            PreviousStatus = @PreviousStatus,
                            LastChecked = @LastChecked,
                            UpdateInfo = @UpdateInfo
                        WHERE Id = @Id
                    ";

                    using var command = new SqliteCommand(query, connection);
                    command.Parameters.AddWithValue("@Id", id);
                    command.Parameters.AddWithValue("@Status", newStatus ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@IsActive", newStatus == "Действующий" ? 1 : 0);
                    command.Parameters.AddWithValue("@PreviousStatus", existing.Status ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@LastChecked", DateTimeHelper.NowBelarus);
                    command.Parameters.AddWithValue("@UpdateInfo", string.IsNullOrWhiteSpace(updateInfo) ? (object)DBNull.Value : updateInfo);
                    command.ExecuteNonQuery();
                    return true;
                }, "UpdateStatusAndInfo");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении статуса и информации записи, Id: {Id}", id);
                throw;
            }
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

        public List<string> GetDistinctUnps()
        {
            try
            {
                _logger.LogInformation("Загрузка уникальных УНП для автодополнения");

                return ExecuteWithRetry(() =>
                {
                    var unps = new List<string>();

                    using var connection = CreateConnection();

                    string query = @"
                        SELECT DISTINCT Unp
                        FROM SearchHistory
                        WHERE Unp IS NOT NULL AND Unp != ''
                        ORDER BY Unp
                    ";

                    using var command = new SqliteCommand(query, connection);
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        unps.Add(reader["Unp"].ToString() ?? string.Empty);
                    }

                    return unps;
                }, "GetDistinctUnps");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке уникальных УНП");
                throw;
            }
        }

        public async Task<List<string>> GetDistinctUnpsAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(GetDistinctUnps, cancellationToken);
        }
    }
}
