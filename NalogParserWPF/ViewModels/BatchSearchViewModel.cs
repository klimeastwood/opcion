using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Newtonsoft.Json;
using NalogParserWPF.Helpers;
using NalogParserWPF.Models;
using NalogParserWPF.Services;

namespace NalogParserWPF.ViewModels
{
    public class BatchSearchViewModel : ViewModelBase
    {
        private readonly INalogApiService _apiService;
        private readonly IHistoryRepository _historyRepository;
        private readonly INotificationService _notifications;
        private readonly IPdfExportService _pdfExportService;
        private readonly ILogger<BatchSearchViewModel> _logger;
        private CancellationTokenSource? _cancellationTokenSource;

        private string _inputList = string.Empty;
        private bool _isSearching;
        private int _progressValue;
        private string _progressText = string.Empty;
        private string _statusMessage = "Готов к работе. Введите УНП (по одному на строку).";

        public BatchSearchViewModel(
            INalogApiService apiService,
            IHistoryRepository historyRepository,
            INotificationService notifications,
            IPdfExportService pdfExportService,
            ILogger<BatchSearchViewModel> logger)
        {
            _apiService = apiService;
            _historyRepository = historyRepository;
            _notifications = notifications;
            _pdfExportService = pdfExportService;
            _logger = logger;

            Results = new ObservableCollection<BatchResultItem>();
            SearchBatchCommand = new RelayCommand(_ => _ = ExecuteBatchSearchAsync(), _ => !IsSearching && !string.IsNullOrWhiteSpace(InputList));
            LoadFileCommand = new RelayCommand(_ => LoadFile());
            ExportExcelCommand = new RelayCommand(_ => _ = ExportExcelAsync(), _ => !IsSearching && Results.Count > 0);
            ExportCsvCommand = new RelayCommand(_ => _ = ExportCsvAsync(), _ => !IsSearching && Results.Count > 0);
            ExportPdfCommand = new RelayCommand(_ => _ = ExportPdfAsync(), _ => !IsSearching && Results.Count > 0);
            CancelSearchCommand = new RelayCommand(_ => CancelSearch(), _ => IsSearching);
            ClearCommand = new RelayCommand(_ => ClearInput());
        }

        public string InputList
        {
            get => _inputList;
            set
            {
                if (SetProperty(ref _inputList, value))
                    SearchBatchCommand.RaiseCanExecuteChanged();
            }
        }

        // Обратная совместимость с ранними версиями окна
        public string UnpList
        {
            get => InputList;
            set => InputList = value;
        }

        public ObservableCollection<BatchResultItem> Results { get; }

        public bool IsSearching
        {
            get => _isSearching;
            set
            {
                if (SetProperty(ref _isSearching, value))
                {
                    SearchBatchCommand.RaiseCanExecuteChanged();
                    CancelSearchCommand.RaiseCanExecuteChanged();
                    ExportCsvCommand.RaiseCanExecuteChanged();
                    ExportExcelCommand.RaiseCanExecuteChanged();
                    ExportPdfCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public int ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public RelayCommand SearchBatchCommand { get; }
        public RelayCommand LoadFileCommand { get; }
        public RelayCommand ExportExcelCommand { get; }
        public RelayCommand ExportCsvCommand { get; }
        public RelayCommand ExportPdfCommand { get; }
        public RelayCommand CancelSearchCommand { get; }
        public RelayCommand ClearCommand { get; }

        public void LoadFile()
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "Текстовые файлы (*.txt)|*.txt|CSV файлы (*.csv)|*.csv|Excel файлы (*.xlsx)|*.xlsx|Все файлы (*.*)|*.*",
                Title = "Выберите файл со списком УНП"
            };

            if (openDialog.ShowDialog() != true) return;

            _ = LoadFileAsync(openDialog.FileName);
        }

        public async Task LoadFileAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    throw new FileNotFoundException("Файл не найден.", filePath);

                ProgressText = $"Чтение файла: {Path.GetFileName(filePath)}...";
                ProgressValue = 10;
                _logger.LogInformation("Загрузка списка запросов из файла: {Path}", filePath);

                string extension = Path.GetExtension(filePath).ToLowerInvariant();
                List<string> queries;

                if (extension == ".xlsx")
                {
                    queries = await Task.Run(() => ReadExcelQueries(filePath), CancellationToken.None);
                }
                else
                {
                    string text = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                    queries = ParseLines(text);
                }

                ProgressValue = 90;
                InputList = string.Join(Environment.NewLine, queries);
                ProgressValue = 100;
                ProgressText = $"Загружено запросов: {queries.Count}";
                _logger.LogInformation("Из файла {Path} загружено запросов: {Count}", filePath, queries.Count);
            }
            catch (Exception ex)
            {
                ProgressValue = 0;
                ProgressText = string.Empty;
                _logger.LogError(ex, "Ошибка при загрузке файла со списком запросов");
                _notifications.ShowError($"Ошибка при загрузке файла: {ex.Message}");
            }
        }

        public void ClearInput()
        {
            InputList = string.Empty;
            Results.Clear();
            ProgressValue = 0;
            ProgressText = string.Empty;
            StatusMessage = "Готов к работе. Введите УНП (по одному на строку).";
            _logger.LogInformation("Список запросов и результаты очищены");
        }

        public async Task ExecuteBatchSearchAsync()
        {
            if (IsSearching) return;

            var queries = ParseLines(InputList);
            if (queries.Count == 0)
            {
                StatusMessage = "Введите УНП для поиска (по одному на строку).";
                return;
            }

            _logger.LogInformation("Начало группового поиска: запросов={Count}", queries.Count);

            try
            {
                IsSearching = true;
                ProgressValue = 0;
                ProgressText = $"Обработано 0 из {queries.Count}";
                StatusMessage = $"Найдено запросов: {queries.Count}. Выполняется поиск...";
                _cancellationTokenSource = new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;

                Results.Clear();
                int total = queries.Count;
                int foundCount = 0;
                int notFoundCount = 0;
                int skippedCount = 0;
                int errorCount = 0;

                for (int i = 0; i < total; i++)
                {
                    token.ThrowIfCancellationRequested();

                    string query = queries[i];
                    int done = i + 1;
                    ProgressValue = (int)Math.Round((double)done / total * 100);
                    ProgressText = $"Обработано {done} из {total}";
                    StatusMessage = $"Обработка {done} из {total} ({ProgressValue}%): {query}...";

                    try
                    {
                        string normalizedUnp = UnpValidator.Normalize(query);
                        if (!UnpValidator.IsValid(normalizedUnp))
                        {
                            skippedCount++;
                            _logger.LogWarning("Пропущен невалидный УНП при групповом поиске: '{Query}'", query);
                            continue;
                        }

                        NalogResponse? response = await _apiService.SearchLegalEntityAsync(new LegalSearchParams { Unp = normalizedUnp }, token);

                        var row = response?.Row;
                        var item = new BatchResultItem
                        {
                            Index = i + 1,
                            Query = normalizedUnp,
                            Unp = row?.Unp ?? normalizedUnp,
                            Name = row?.FullName ?? "Не найдено",
                            Status = row?.Status ?? "Не найден",
                            StatusColor = GetStatusColor(row?.Status),
                            Address = row?.Address ?? string.Empty,
                            Inspection = row?.InspectionName ?? string.Empty,
                            RegistrationDate = row?.RegistrationDate ?? string.Empty,
                            CheckDate = DateTimeHelper.NowBelarus.ToString("dd.MM.yyyy HH:mm"),
                            Error = string.Empty,
                            RawData = row != null
                                ? JsonConvert.SerializeObject(new NalogResponse { Row = row })
                                : string.Empty
                        };

                        if (row != null)
                        {
                            foundCount++;
                            await SaveToHistoryAsync(normalizedUnp, row, token);
                        }
                        else
                        {
                            notFoundCount++;
                        }

                        Results.Add(item);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _logger.LogError(ex, "Ошибка при поиске запроса '{Query}'", query);
                        Results.Add(new BatchResultItem
                        {
                            Index = i + 1,
                            Query = query,
                            Unp = query,
                            Name = "Ошибка",
                            Status = "Ошибка запроса",
                            StatusColor = "Red",
                            Address = string.Empty,
                            Inspection = string.Empty,
                            RegistrationDate = string.Empty,
                            CheckDate = DateTimeHelper.NowBelarus.ToString("dd.MM.yyyy HH:mm"),
                            Error = ex.Message
                        });
                    }
                }

                ProgressValue = 100;
                ProgressText = $"Обработано {total} из {total}";
                StatusMessage = $"Завершено. Всего: {total}, Успешно: {foundCount}, Ошибок: {errorCount}";
                _logger.LogInformation("Групповой поиск завершен: всего={Total}, найдено={Found}, не найдено={NotFound}, пропущено={Skipped}, ошибок={Errors}",
                    total, foundCount, notFoundCount, skippedCount, errorCount);
            }
            catch (OperationCanceledException)
            {
                ProgressValue = 0;
                ProgressText = string.Empty;
                StatusMessage = "Поиск отменен";
                _logger.LogInformation("Групповой поиск отменен пользователем");
            }
            catch (Exception ex)
            {
                ProgressValue = 0;
                ProgressText = string.Empty;
                StatusMessage = $"Ошибка: {ex.Message}";
                _logger.LogError(ex, "Ошибка при выполнении группового поиска");
                _notifications.ShowError($"Ошибка при выполнении поиска: {ex.Message}");
            }
            finally
            {
                IsSearching = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        public void CancelSearch()
        {
            _logger.LogInformation("Запрошена отмена группового поиска");
            _cancellationTokenSource?.Cancel();
            StatusMessage = "Отмена поиска...";
        }

        public async Task ExportCsvAsync()
        {
            if (Results.Count == 0)
            {
                _notifications.ShowInfo("Нет данных для экспорта.");
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV файлы (*.csv)|*.csv",
                FileName = $"Групповой_поиск_{DateTimeHelper.NowBelarus:yyyy-MM-dd_HH-mm}",
                DefaultExt = "csv"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                StatusMessage = "Экспорт в CSV...";
                var sb = new StringBuilder();
                sb.AppendLine("№;Запрос;УНП;Наименование;Статус;Адрес;Инспекция;Дата регистрации;Дата проверки;Ошибка");
                foreach (var item in Results)
                {
                    sb.AppendLine(string.Join(";", new[]
                    {
                        item.Index.ToString(),
                        Csv(item.Query),
                        Csv(item.Unp),
                        Csv(item.Name),
                        Csv(item.Status),
                        Csv(item.Address),
                        Csv(item.Inspection),
                        Csv(item.RegistrationDate),
                        Csv(item.CheckDate),
                        Csv(item.Error)
                    }));
                }
                await File.WriteAllTextAsync(saveDialog.FileName, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

                StatusMessage = "Экспорт в CSV завершен";
                _notifications.ShowSuccess($"Файл CSV успешно сохранен!\n{saveDialog.FileName}");
                _logger.LogInformation("Экспорт в CSV завершен: {Path}", saveDialog.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте в CSV");
                _notifications.ShowError($"Ошибка при экспорте в CSV: {ex.Message}");
            }
        }

        public async Task ExportExcelAsync()
        {
            if (Results.Count == 0)
            {
                _notifications.ShowInfo("Нет данных для экспорта.");
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                FileName = $"Групповой_поиск_{DateTimeHelper.NowBelarus:yyyy-MM-dd_HH-mm}",
                DefaultExt = "xlsx"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                StatusMessage = "Экспорт в Excel...";
                _logger.LogInformation("Начало экспорта результатов в Excel: {Path}", saveDialog.FileName);

                OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal("NalogParserWPF");

                using var package = new OfficeOpenXml.ExcelPackage();
                var sheet = package.Workbook.Worksheets.Add("Результаты");

                string[] headers =
                {
                    "№", "Запрос", "УНП", "Наименование", "Статус",
                    "Адрес", "Инспекция", "Дата регистрации", "Дата проверки", "Ошибка"
                };

                for (int c = 0; c < headers.Length; c++)
                    sheet.Cells[1, c + 1].Value = headers[c];

                for (int i = 0; i < Results.Count; i++)
                {
                    var item = Results[i];
                    int row = i + 2;
                    sheet.Cells[row, 1].Value = item.Index;
                    sheet.Cells[row, 2].Value = item.Query;
                    sheet.Cells[row, 3].Value = item.Unp;
                    sheet.Cells[row, 4].Value = item.Name;
                    sheet.Cells[row, 5].Value = item.Status;
                    sheet.Cells[row, 6].Value = item.Address;
                    sheet.Cells[row, 7].Value = item.Inspection;
                    sheet.Cells[row, 8].Value = item.RegistrationDate;
                    sheet.Cells[row, 9].Value = item.CheckDate;
                    sheet.Cells[row, 10].Value = item.Error;
                }

                sheet.Cells[1, 1, 1, headers.Length].Style.Font.Bold = true;
                sheet.Cells.AutoFitColumns();

                await Task.Run(() => package.SaveAs(new FileInfo(saveDialog.FileName)));

                StatusMessage = "Экспорт в Excel завершен";
                _notifications.ShowSuccess($"Файл Excel успешно сохранен!\n{saveDialog.FileName}");
                _logger.LogInformation("Экспорт в Excel завершен: {Path}", saveDialog.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте в Excel");
                _notifications.ShowError($"Ошибка при экспорте в Excel: {ex.Message}");
            }
        }

        public async Task ExportPdfAsync()
        {
            if (Results.Count == 0)
            {
                _notifications.ShowInfo("Нет данных для экспорта.");
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF файлы (*.pdf)|*.pdf",
                FileName = $"Групповой_поиск_{DateTimeHelper.NowBelarus:yyyy-MM-dd_HH-mm}",
                DefaultExt = "pdf"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                StatusMessage = "Экспорт в PDF...";
                _logger.LogInformation("Начало экспорта результатов в PDF: {Path}", saveDialog.FileName);

                await _pdfExportService.ExportBatchResultsAsync(Results, saveDialog.FileName);

                StatusMessage = "Экспорт в PDF завершен";
                _notifications.ShowSuccess($"Файл PDF успешно сохранен!\n{saveDialog.FileName}");
                _logger.LogInformation("Экспорт в PDF завершен: {Path}", saveDialog.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте в PDF");
                _notifications.ShowError($"Ошибка при экспорте в PDF: {ex.Message}");
            }
        }

        private static List<string> ParseLines(string text)
        {
            return text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(x => x.Trim())
                      .Where(x => !string.IsNullOrEmpty(x))
                      .ToList();
        }

        private static List<string> ReadExcelQueries(string filePath)
        {
            var result = new List<string>();
            using var package = new OfficeOpenXml.ExcelPackage(new FileInfo(filePath));
            var sheet = package.Workbook.Worksheets.FirstOrDefault()
                ?? throw new InvalidDataException("Файл Excel не содержит рабочих листов.");

            int rows = sheet.Dimension?.Rows ?? 0;
            for (int r = 1; r <= rows; r++)
            {
                string value = (sheet.Cells[r, 1].Text ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(value))
                    result.Add(value);
            }
            return result;
        }

        private static string Csv(string? value)
        {
            string s = value ?? string.Empty;
            if (s.Contains(';') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
                return $"\"{s.Replace("\"", "\"\"")}\"";
            return s;
        }

        private async Task SaveToHistoryAsync(string query, CompanyRow row, CancellationToken token)
        {
            try
            {
                string resultData = JsonConvert.SerializeObject(new NalogResponse { Row = row });

                var history = new SearchHistory
                {
                    SearchType = "Legal",
                    SearchParams = $"Групповой поиск: {query}",
                    ResultData = resultData,
                    Unp = row.Unp ?? query,
                    FullName = row.FullName ?? string.Empty,
                    Status = row.Status ?? string.Empty,
                    IsActive = row.Status == "Действующий"
                };
                await _historyRepository.AddAsync(history, token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка сохранения в историю для запроса {Query}", query);
            }
        }

        private static string GetStatusColor(string? status)
        {
            return status switch
            {
                "Действующий" or "Активный" => "Green",
                null or "" => "Gray",
                _ => "Red"
            };
        }
    }
}
