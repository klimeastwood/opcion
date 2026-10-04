using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NalogParserWPF.Helpers;
using NalogParserWPF.Models;
using NalogParserWPF.Services;

namespace NalogParserWPF.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly INalogApiService _apiService;
        private readonly IHistoryRepository _historyRepository;
        private readonly IResultFormatter _resultFormatter;
        private readonly IPdfExportService _pdfExportService;
        private readonly INotificationService _notifications;
        private readonly ILogger<MainViewModel> _logger;
        private readonly IServiceProvider _serviceProvider;
        private CancellationTokenSource? _cancellationTokenSource;

        private readonly List<string> _allUnps = new();
        private string _searchQuery = string.Empty;
        private bool _isSearching;
        private bool _isValidInput;
        private string _statusMessage = "Готов к поиску";
        private bool _isChecking;
        private string _checkStatus = string.Empty;
        private NalogResponse? _currentResult;
        private CompanyRow? _currentCompany;
        private bool _isDetailedView;
        private List<HistoryItemViewModel> _selectedHistoryItems = new();

        public MainViewModel(
            INalogApiService apiService,
            IHistoryRepository historyRepository,
            IResultFormatter resultFormatter,
            IPdfExportService pdfExportService,
            INotificationService notifications,
            ILogger<MainViewModel> logger,
            IServiceProvider serviceProvider)
        {
            _apiService = apiService;
            _historyRepository = historyRepository;
            _resultFormatter = resultFormatter;
            _pdfExportService = pdfExportService;
            _notifications = notifications;
            _logger = logger;
            _serviceProvider = serviceProvider;

            HistoryItems = new ObservableCollection<HistoryItemViewModel>();
            LoadHistoryCommand = new RelayCommand(_ => _ = LoadHistoryAsync());
            SearchCommand = new RelayCommand(_ => _ = ExecuteSearchAsync(), _ => !IsSearching && IsValidInput);
            ClearHistoryCommand = new RelayCommand(_ => ClearHistoryAsync());
            // CanExecute проверяет параметр: команды вызываются и из контекстного меню строки,
            // где при отсутствии выделения параметр равен null (раньше это давало исключение).
            ViewHistoryCommand = new RelayCommand<HistoryItemViewModel>(item => ViewHistory(item!), item => item != null);
            RepeatSearchCommand = new RelayCommand<HistoryItemViewModel>(item => RepeatSearchAsync(item!), item => item != null);
            DeleteHistoryItemCommand = new RelayCommand<HistoryItemViewModel>(item => _ = DeleteHistoryItemAsync(item), item => item != null);
            DeleteSelectedCommand = new RelayCommand(_ => _ = DeleteSelectedHistoryAsync(),
                _ => SelectedHistoryItems.Count > 0 && !IsChecking);
            ClearResultCommand = new RelayCommand(_ => ClearResult(), _ => CurrentCompany != null);
            CopyResultCommand = new RelayCommand(_ => CopyResult(), _ => CurrentCompany != null);
            ExportPdfCommand = new RelayCommand(_ => _ = ExportPdfAsync(), _ => CurrentCompany != null);
            OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
            OpenBatchSearchCommand = new RelayCommand(_ => OpenBatchSearch());
            OpenAboutCommand = new RelayCommand(_ => OpenAbout());
            CheckAllCommand = new RelayCommand(_ => _ = CheckAllUnpsAsync(), _ => !IsChecking);

            ExchangeRates = _serviceProvider.GetRequiredService<ExchangeRateViewModel>();
            Belstat = _serviceProvider.GetRequiredService<BelstatViewModel>();
            BatchSearch = _serviceProvider.GetRequiredService<BatchSearchViewModel>();

            _ = LoadHistoryAsync();
            _ = LoadUnpSuggestionsAsync();
            _ = CheckAllUnpsAsync();
        }

        /// <summary>
        /// Вкладка «Курсы валют» НБ РБ: официальные курсы, динамика, экспорт и история запросов.
        /// Создается один раз вместе с главным окном.
        /// </summary>
        public ExchangeRateViewModel ExchangeRates { get; }

        /// <summary>
        /// Вкладка «Госстатистика» (публичный API Белстата).
        /// Создается один раз вместе с главным окном.
        /// </summary>
        public BelstatViewModel Belstat { get; }

        /// <summary>
        /// Вкладка «Массовый поиск»: пакетная проверка списка УНП.
        /// Создается один раз вместе с главным окном.
        /// </summary>
        public BatchSearchViewModel BatchSearch { get; }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    UpdateSuggestions(value);
                    IsValidInput = ValidateInput(value);
                    SearchCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsValidInput
        {
            get => _isValidInput;
            private set => SetProperty(ref _isValidInput, value);
        }

        public bool IsSearching
        {
            get => _isSearching;
            set
            {
                if (SetProperty(ref _isSearching, value))
                    SearchCommand.RaiseCanExecuteChanged();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsChecking
        {
            get => _isChecking;
            set
            {
                if (SetProperty(ref _isChecking, value))
                    CheckAllCommand.RaiseCanExecuteChanged();
            }
        }

        public string CheckStatus
        {
            get => _checkStatus;
            set => SetProperty(ref _checkStatus, value);
        }

        public NalogResponse? CurrentResult
        {
            get => _currentResult;
            set => SetProperty(ref _currentResult, value);
        }

        public CompanyRow? CurrentCompany
        {
            get => _currentCompany;
            set
            {
                if (SetProperty(ref _currentCompany, value))
                {
                    CopyResultCommand.RaiseCanExecuteChanged();
                    ExportPdfCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsDetailedView
        {
            get => _isDetailedView;
            set
            {
                if (SetProperty(ref _isDetailedView, value))
                    _logger.LogInformation("Переключение режима отображения: {Mode}", value ? "Подробно" : "Кратко");
            }
        }

        public ObservableCollection<HistoryItemViewModel> HistoryItems { get; }

        public ObservableCollection<string> SearchHistorySuggestions { get; } = new();

        /// <summary>
        /// Выделенные в таблице записи истории (DataGrid с SelectionMode="Extended"):
        /// список заполняет код-behind через обработчик SelectionChanged, команда
        /// «Удалить выделенные» работает с этим набором.
        /// </summary>
        public List<HistoryItemViewModel> SelectedHistoryItems
        {
            get => _selectedHistoryItems;
            set
            {
                _selectedHistoryItems = value ?? new List<HistoryItemViewModel>();
                OnPropertyChanged();
                DeleteSelectedCommand.RaiseCanExecuteChanged();
            }
        }

        public RelayCommand SearchCommand { get; }
        public RelayCommand LoadHistoryCommand { get; }
        public RelayCommand ClearHistoryCommand { get; }
        public RelayCommand<HistoryItemViewModel> ViewHistoryCommand { get; }
        public RelayCommand<HistoryItemViewModel> RepeatSearchCommand { get; }
        public RelayCommand<HistoryItemViewModel> DeleteHistoryItemCommand { get; }
        public RelayCommand DeleteSelectedCommand { get; }
        public RelayCommand ClearResultCommand { get; }
        public RelayCommand CopyResultCommand { get; }
        public RelayCommand ExportPdfCommand { get; }
        public RelayCommand OpenSettingsCommand { get; }
        public RelayCommand CheckAllCommand { get; }
        public RelayCommand OpenBatchSearchCommand { get; }
        public RelayCommand OpenAboutCommand { get; }

        private bool ValidateInput(string input)
        {
            return !string.IsNullOrWhiteSpace(input);
        }

        private async Task ExecuteSearchAsync()
        {
            if (IsSearching) return;

            string normalizedUnp = UnpValidator.Normalize(SearchQuery);

            if (!UnpValidator.IsValid(SearchQuery))
            {
                const string message = "УНП должен состоять из 9 цифр";
                _logger.LogWarning("Поиск по невалидному УНП отклонен: '{Input}'", SearchQuery);
                StatusMessage = message;
                return;
            }

            try
            {
                IsSearching = true;
                StatusMessage = "Выполняется поиск...";
                _cancellationTokenSource = new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;

                var parameters = new LegalSearchParams { Unp = normalizedUnp };
                NalogResponse? result = await _apiService.SearchLegalEntityAsync(parameters, token);

                if (result?.Row != null)
                {
                    CurrentResult = result;
                    CurrentCompany = result.Row;
                    StatusMessage = $"Найдено: {result.Row.FullName ?? SearchQuery}";

                    AddUnpSuggestion(normalizedUnp);

                    // Сохраняем в историю
                    var historyEntry = new SearchHistory
                    {
                        SearchType = "Legal",
                        SearchParams = SearchQuery,
                        ResultData = Newtonsoft.Json.JsonConvert.SerializeObject(result),
                        Unp = SearchQuery,
                        FullName = result.Row.FullName ?? SearchQuery,
                        Status = "Активный",
                        IsActive = true
                    };
                    await _historyRepository.AddAsync(historyEntry, token);
                    await LoadHistoryAsync();
                }
                else
                {
                    StatusMessage = "Ничего не найдено";
                    CurrentResult = null;
                    CurrentCompany = null;
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Поиск отменен";
                _logger.LogInformation("Поиск отменен пользователем");
            }
            // API Налоговой отдаёт 400 и при отсутствии УНП в базе, и при неверном запросе.
            // Оба случая показываем в статусной строке окна — без модального MessageBox.
            catch (NalogApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                CurrentResult = null;
                CurrentCompany = null;
                StatusMessage = string.Equals(ex.Message, NalogApiService.UnpNotFoundMessage, StringComparison.Ordinal)
                    ? NalogApiService.UnpNotFoundMessage
                    : "Нет данных по запросу. Проверьте УНП.";
                _logger.LogWarning(ex, "API вернул 400 при поиске УНП {Unp}. Ответ сервера: {ResponseBody}", normalizedUnp, ex.ResponseBody);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка: {ex.Message}";
                _logger.LogError(ex, "Ошибка при выполнении поиска");
                _notifications.ShowError($"Ошибка: {ex.Message}");
            }
            finally
            {
                IsSearching = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private async Task LoadHistoryAsync()
        {
            try
            {
                var history = await _historyRepository.GetAllAsync();
                HistoryItems.Clear();
                foreach (var item in history)
                {
                    HistoryItems.Add(HistoryItemViewModel.CreateFromModel(item));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке истории");
            }
        }

        private async Task LoadUnpSuggestionsAsync()
        {
            try
            {
                var unps = await _historyRepository.GetDistinctUnpsAsync();
                _allUnps.Clear();
                _allUnps.AddRange(unps);
                UpdateSuggestions(SearchQuery);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке УНП для автодополнения");
            }
        }

        public async Task CheckAllUnpsAsync()
        {
            if (IsChecking) return;

            try
            {
                IsChecking = true;
                CheckStatus = "Загрузка УНП из истории...";

                var allHistory = await _historyRepository.GetAllAsync();
                var latestByUnp = allHistory
                    .Where(e => !string.IsNullOrWhiteSpace(e.Unp))
                    .GroupBy(e => e.Unp)
                    .Select(g => g.OrderByDescending(e => e.SearchDate).First())
                    .ToList();

                if (latestByUnp.Count == 0)
                {
                    CheckStatus = "Нет записей для проверки";
                    return;
                }

                int updated = 0;
                int notFound = 0;
                int failed = 0;

                for (int i = 0; i < latestByUnp.Count; i++)
                {
                    var entry = latestByUnp[i];
                    string unp = entry.Unp ?? string.Empty;
                    CheckStatus = $"Проверка {i + 1} из {latestByUnp.Count}: {unp}";

                    try
                    {
                        var result = await _apiService.GetCompanyInfoAsync(unp);
                        if (result?.Row == null)
                        {
                            notFound++;
                            string info = $"УНП не найден в базе\nДата изменения: {DateTimeHelper.NowBelarus:dd.MM.yyyy HH:mm}";
                            await _historyRepository.UpdateStatusAndInfoAsync(entry.Id, entry.Status, info);
                            continue;
                        }

                        var apiStatus = result.Row.Status ?? string.Empty;
                        var savedStatus = entry.Status ?? string.Empty;

                        if (!string.Equals(apiStatus.Trim(), savedStatus.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            string info = $"Статус изменен: {savedStatus} → {apiStatus}\nДата изменения: {DateTimeHelper.NowBelarus:dd.MM.yyyy HH:mm}";
                            await _historyRepository.UpdateStatusAndInfoAsync(entry.Id, apiStatus, info);
                            updated++;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (NalogApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest
                                                       && string.Equals(ex.Message, NalogApiService.UnpNotFoundMessage, StringComparison.Ordinal))
                    {
                        notFound++;
                        string info = $"УНП не найден в базе\nДата изменения: {DateTimeHelper.NowBelarus:dd.MM.yyyy HH:mm}";
                        try
                        {
                            await _historyRepository.UpdateStatusAndInfoAsync(entry.Id, entry.Status, info);
                        }
                        catch (Exception innerEx)
                        {
                            _logger.LogError(innerEx, "Ошибка при сохранении статуса 'УНП не найден' для {Unp}", unp);
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        _logger.LogError(ex, "Ошибка при проверке УНП {Unp}", unp);
                    }
                }

                CheckStatus = updated > 0
                    ? $"Проверка завершена: обновлено {updated}, не найдено {notFound}, ошибок {failed}"
                    : $"Проверка завершена: изменений нет (не найдено {notFound}, ошибок {failed})";

                await LoadHistoryAsync();
            }
            catch (Exception ex)
            {
                CheckStatus = $"Ошибка при проверке: {ex.Message}";
                _logger.LogError(ex, "Ошибка при автоматической проверке УНП");
            }
            finally
            {
                IsChecking = false;
            }
        }

        public void UpdateSuggestions(string query)
        {
            string normalized = string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();

            SearchHistorySuggestions.Clear();
            foreach (string unp in _allUnps)
            {
                if (normalized.Length == 0 || unp.StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
                    SearchHistorySuggestions.Add(unp);
            }
        }

        private void AddUnpSuggestion(string unp)
        {
            if (string.IsNullOrWhiteSpace(unp) || _allUnps.Contains(unp))
                return;

            _allUnps.Add(unp);
            _allUnps.Sort(StringComparer.OrdinalIgnoreCase);
            UpdateSuggestions(SearchQuery);
        }

        private async void ClearHistoryAsync()
        {
            if (MessageBox.Show("Очистить всю историю?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    await _historyRepository.ClearAllAsync();
                    HistoryItems.Clear();
                    StatusMessage = "История очищена";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при очистке истории");
                    _notifications.ShowError($"Ошибка: {ex.Message}");
                }
            }
        }

        private void ViewHistory(HistoryItemViewModel item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            try
            {
                var window = _serviceProvider.GetRequiredService<HistoryDetailWindow>();
                if (window.DataContext is HistoryDetailViewModel viewModel)
                {
                    _ = viewModel.LoadEntryAsync(item.Id);
                }
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при открытии деталей записи #{Id}", item.Id);
                _notifications.ShowError($"Ошибка при открытии деталей: {ex.Message}");
            }
        }

        private async void RepeatSearchAsync(HistoryItemViewModel item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            SearchQuery = item.Unp;
            await ExecuteSearchAsync();
        }

        /// <summary>
        /// Очистка результата поиска: карточка организации, найденные данные и поле ввода УНП.
        /// История проверок при этом не затрагивается (для неё есть «Очистить всё»).
        /// </summary>
        private void ClearResult()
        {
            CurrentResult = null;
            CurrentCompany = null;
            SearchQuery = string.Empty;
            StatusMessage = "Готов к поиску";
            _logger.LogInformation("Результат поиска очищен пользователем");
        }

        /// <summary>
        /// Удаляет одну запись истории УНП (кнопка в строке таблицы или пункт контекстного меню).
        /// </summary>
        private async Task DeleteHistoryItemAsync(HistoryItemViewModel? item)
        {
            if (item == null)
                return;

            if (MessageBox.Show($"Удалить запись истории по УНП {item.Unp}?", "Подтверждение удаления",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            if (!await TryDeleteHistoryEntryAsync(item.Id))
                return;

            await LoadHistoryAsync();
            await LoadUnpSuggestionsAsync();
            SelectedHistoryItems = new List<HistoryItemViewModel>();
            StatusMessage = $"Запись истории по УНП {item.Unp} удалена";
        }

        /// <summary>
        /// Удаляет все выделенные в таблице записи истории УНП (массовая операция;
        /// выделение нескольких строк — Ctrl или Shift в таблице).
        /// </summary>
        private async Task DeleteSelectedHistoryAsync()
        {
            var targets = SelectedHistoryItems.Where(entry => entry != null).Distinct().ToList();
            if (targets.Count == 0)
            {
                _notifications.ShowWarning("Не выбрано ни одной записи истории.");
                return;
            }

            if (MessageBox.Show($"Удалить выбранные записи истории ({targets.Count} шт.)?",
                    "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var deleted = 0;
            foreach (var entry in targets)
            {
                if (await TryDeleteHistoryEntryAsync(entry.Id))
                    deleted++;
            }

            await LoadHistoryAsync();
            await LoadUnpSuggestionsAsync();
            SelectedHistoryItems = new List<HistoryItemViewModel>();
            StatusMessage = deleted > 0
                ? $"Удалено записей истории: {deleted}"
                : "Записи истории не удалены";
        }

        /// <summary>Удаление записи истории из локальной базы с логированием ошибок.</summary>
        private async Task<bool> TryDeleteHistoryEntryAsync(int id)
        {
            try
            {
                await _historyRepository.DeleteAsync(id);
                _logger.LogInformation("Запись истории #{Id} удалена", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении записи истории #{Id}", id);
                _notifications.ShowError($"Не удалось удалить запись истории: {ex.Message}");
                return false;
            }
        }

        private void CopyResult()
        {
            var company = CurrentCompany;
            if (company == null)
            {
                _logger.LogWarning("Копирование результата отменено: нет данных о компании");
                return;
            }

            try
            {
                string text = _resultFormatter.FormatCompanyInfo(company);
                Clipboard.SetText(text);
                StatusMessage = "Результат скопирован в буфер обмена";
                _logger.LogInformation("Результат поиска скопирован в буфер обмена");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при копировании результата в буфер обмена");
                _notifications.ShowError($"Ошибка при копировании: {ex.Message}");
            }
        }

        private async Task ExportPdfAsync()
        {
            var company = CurrentCompany;
            if (company == null)
            {
                _logger.LogWarning("Экспорт в PDF отменен: нет данных о компании");
                return;
            }

            string fileName = $"Отчет_{company.Unp ?? "Компания"}";
            if (!string.IsNullOrEmpty(company.ShortName))
                fileName += $"_{company.ShortName}";
            else if (!string.IsNullOrEmpty(company.FullName))
                fileName += $"_{company.FullName}";
            fileName += $"_{DateTimeHelper.NowBelarus:yyyy-MM-dd}";

            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF файлы (*.pdf)|*.pdf",
                FileName = fileName,
                DefaultExt = "pdf"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                StatusMessage = "Подготовка PDF...";
                _logger.LogInformation("Начало экспорта результата в PDF для {Unp}", company.Unp);

                await _pdfExportService.ExportCompanyInfoAsync(company, saveDialog.FileName);

                StatusMessage = "PDF отчет сформирован";
                _logger.LogInformation("Экспорт в PDF успешно завершен для {Unp}", company.Unp);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка при экспорте в PDF";
                _logger.LogError(ex, "Ошибка при экспорте в PDF для {Unp}", company.Unp);
                _notifications.ShowError($"Ошибка при экспорте в PDF: {ex.Message}");
            }
        }

        private void OpenSettings()
        {
            try
            {
                var window = _serviceProvider.GetRequiredService<Views.SettingsWindow>();
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при открытии окна настроек");
                _notifications.ShowError($"Ошибка при открытии настроек: {ex.Message}");
            }
        }

        private void OpenBatchSearch()
        {
            try
            {
                var window = App.ServiceProvider?.GetRequiredService<BatchSearchWindow>();
                if (window == null)
                {
                    _logger.LogWarning("App.ServiceProvider не инициализирован, окно массового поиска не открыто");
                    _notifications.ShowError("Ошибка при открытии массового поиска: контейнер DI не инициализирован");
                    return;
                }
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при открытии окна массового поиска");
                _notifications.ShowError($"Ошибка при открытии массового поиска: {ex.Message}");
            }
        }

        private void OpenAbout()
        {
            try
            {
                var window = _serviceProvider.GetRequiredService<Views.AboutWindow>();
                window.Owner = Application.Current?.MainWindow;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при открытии окна 'О программе'");
                _notifications.ShowError($"Ошибка при открытии окна 'О программе': {ex.Message}");
            }
        }
    }
}
