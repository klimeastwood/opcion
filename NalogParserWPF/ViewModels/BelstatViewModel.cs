using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using FuzzySharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NalogParserWPF.Database;
using NalogParserWPF.Helpers;
using NalogParserWPF.Models;
using NalogParserWPF.Services;

namespace NalogParserWPF.ViewModels
{
    /// <summary>
    /// Вкладка «Госстатистика»: показатели государственной статистики Белстата
    /// (тематические рубрики, поиск показателей по каталогу, значения за выбранные годы)
    /// с экспортом в PDF, Excel и CSV и историей выполненных запросов.
    /// </summary>
    public class BelstatViewModel : ViewModelBase
    {
        private const string ExcelLicenseHolder = "NalogParserWPF";
        private const int DefaultYearsCount = 5;
        private const int MinYear = 1990;
        private const string UnspecifiedIndex = "-1";
        private const string UnspecifiedCell = "—";
        private const string ValuesColumnName = "Значение";
        private const string DefaultTitle = "Показатели государственной статистики";
        private const string DataSourceText = "dataportal.belstat.gov.by (Национальный статистический комитет Республики Беларусь)";

        /// <summary>Срок, после которого запись истории считается устаревшей (в днях).</summary>
        private const int HistoryStaleDays = 30;

        /// <summary>Сообщение о загрузке параметров запроса из локальной базы данных приложения.</summary>
        private const string HistoryLoadedMessage = "Данные загружены из локальной БД. Проверьте актуальность.";

        /// <summary>Сколько первых результатов показывает «живой» поиск (для производительности).</summary>
        private const int LiveSearchLimit = 20;

        /// <summary>Сколько результатов возвращает нечёткий поиск (FuzzySharp.ExtractTop).</summary>
        private const int FuzzyTopCount = 20;

        /// <summary>Минимальный порог схожести нечёткого поиска (в процентах).</summary>
        private const int FuzzyThreshold = 70;

        /// <summary>Минимальная длина запроса для нечёткого поиска.</summary>
        private const int FuzzyMinQueryLength = 2;

        /// <summary>Сообщение, когда точных совпадений нет, но найдены похожие.</summary>
        private const string NoExactMatchMessage = "Точных совпадений нет. Показаны похожие результаты.";

        /// <summary>Сообщение, когда поиск не дал результатов.</summary>
        private const string NothingFoundMessage = "Ничего не найдено. Попробуйте изменить запрос.";

        /// <summary>Сообщение после сброса поискового запроса.</summary>
        private const string SearchClearedMessage = "Поиск очищен. Показаны показатели выбранной рубрики.";

        /// <summary>Сообщение, когда таблицу предлагают открыть отдельным окном, а данных нет.</summary>
        private const string ResultWindowEmptyMessage = "Таблица пуста: получите данные показателя и повторите попытку.";

        /// <summary>Сообщение, когда контейнер DI недоступен при открытии окна таблицы.</summary>
        private const string ResultWindowServiceProviderMessage = "Не удалось открыть таблицу: контейнер DI не инициализирован.";

        private readonly IBelstatApiService _belstatApiService;
        private readonly IBelstatHistoryRepository _belstatHistoryRepository;
        private readonly IPdfExportService _pdfExportService;
        private readonly INotificationService _notifications;
        private readonly ILogger<BelstatViewModel> _logger;

        private CancellationTokenSource? _cts;

        private bool _isInitialized;
        private bool _isLoading;

        /// <summary>Признак восстановления параметров из истории: автоподгрузка показателей рубрики подавляется.</summary>
        private bool _suppressRubricAutoLoad;

        /// <summary>Накопленный каталог показателей (код → показатель) для нечёткого поиска.</summary>
        private readonly Dictionary<string, BelstatIndicator> _indicatorPool = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Показатели, попавшие в «живой» поиск (первые <see cref="LiveSearchLimit"/> совпадений).</summary>
        private readonly HashSet<BelstatIndicator> _liveSearchMatches = new();

        /// <summary>Запрос, по которому фильтруется дерево рубрик (пусто — показываются все рубрики).</summary>
        private string _rubricFilterQuery = string.Empty;

        /// <summary>Рубрика, которую фильтр обязан оставить в списке перед программным выбором.</summary>
        private BelstatRubric? _pendingSelectRubric;

        /// <summary>Показатель, выбранный в выпадающем списке результатов поиска.</summary>
        private BelstatIndicator? _selectedSearchResult;

        /// <summary>Признак, что последний поиск дал только похожие (нечёткие) результаты.</summary>
        private bool _lastSearchWasFuzzy;

        /// <summary>Признак, что список показателей содержит результаты поиска по каталогу (не показатели рубрики).</summary>
        private bool _indicatorsFromSearch;

        private string _statusMessage = "Готов к загрузке данных государственной статистики.";
        private BelstatRubric? _selectedRubric;
        private BelstatIndicator? _selectedIndicator;
        private string _searchQuery = string.Empty;
        private int _firstYear = DateTimeHelper.TodayBelarus.Year - DefaultYearsCount + 1;
        private int _lastYear = DateTimeHelper.TodayBelarus.Year;

        private DataTable? _resultTable;
        private string _resultTitle = string.Empty;
        private string _resultSubtitle = string.Empty;
        private string[] _resultHeaders = Array.Empty<string>();
        private List<string[]> _resultRows = new();
        private string _emptyResultMessage = "Данные ещё не загружены. Выберите рубрику или показатель и нажмите «Получить данные».";

        public BelstatViewModel(
            IBelstatApiService belstatApiService,
            IBelstatHistoryRepository belstatHistoryRepository,
            IPdfExportService pdfExportService,
            INotificationService notifications,
            ILogger<BelstatViewModel> logger)
        {
            _belstatApiService = belstatApiService;
            _belstatHistoryRepository = belstatHistoryRepository;
            _pdfExportService = pdfExportService;
            _notifications = notifications;
            _logger = logger;

            Rubrics = new ObservableCollection<BelstatRubric>();
            Indicators = new ObservableCollection<BelstatIndicator>();
            HistoryItems = new ObservableCollection<BelstatHistoryItem>();
            AvailableYears = BuildAvailableYears();

            // «Живой» поиск: FilteredIndicatorsView фильтрует показатели, FilteredRubricsView —
            // дерево рубрик. Оба представления построены через CollectionViewSource с Filter.
            var indicatorViewSource = new CollectionViewSource { Source = Indicators };
            indicatorViewSource.Filter += (_, e) => e.Accepted = FilterIndicatorsView(e.Item);
            FilteredIndicatorsView = indicatorViewSource.View;

            var rubricViewSource = new CollectionViewSource { Source = Rubrics };
            rubricViewSource.Filter += (_, e) => e.Accepted = FilterRubricsView(e.Item);
            FilteredRubricsView = rubricViewSource.View;

            LoadRubricsCommand = new RelayCommand(_ => _ = LoadRubricsAsync(), _ => !IsLoading);
            SearchIndicatorsCommand = new RelayCommand(_ => _ = SearchIndicatorsAsync(), _ => !IsLoading);
            ClearSearchCommand = new RelayCommand(_ => ClearSearch(), _ => !IsLoading);
            LoadDataCommand = new RelayCommand(_ => _ = LoadDataAsync(), _ => !IsLoading && HasSelection);
            CancelCommand = new RelayCommand(_ => CancelLoading(), _ => IsLoading);
            RefreshHistoryCommand = new RelayCommand(_ => _ = LoadHistoryAsync(), _ => !IsLoading);
            ClearHistoryCommand = new RelayCommand(_ => _ = ClearHistoryAsync(), _ => HistoryItems.Count > 0);
            LoadFromHistoryCommand = new RelayCommand(
                parameter => _ = LoadFromHistoryAsync(parameter as BelstatHistoryItem),
                parameter => !IsLoading && parameter is BelstatHistoryItem);
            ExportPdfCommand = new RelayCommand(_ => _ = ExportPdfAsync(), _ => HasExportData);
            ExportExcelCommand = new RelayCommand(_ => _ = ExportExcelAsync(), _ => HasExportData);
            ExportCsvCommand = new RelayCommand(_ => _ = ExportCsvAsync(), _ => HasExportData);
            OpenInNewWindowCommand = new RelayCommand(_ => OpenInNewWindow(), _ => IsResultVisible);
        }

        /// <summary>
        /// Первичная загрузка дерева рубрик и истории запросов.
        /// Вызывается один раз при первом открытии вкладки.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            _isInitialized = true;
            await LoadRubricsAsync();
            await LoadHistoryAsync();
        }

        // ================= Состояние =================

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (SetProperty(ref _isLoading, value))
                    RaiseCommandStates();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // ================= Параметры запроса =================

        /// <summary>Плоский список рубрик каталога Белстата (с отступами по уровню вложенности).</summary>
        public ObservableCollection<BelstatRubric> Rubrics { get; }

        public BelstatRubric? SelectedRubric
        {
            get => _selectedRubric;
            set
            {
                if (!SetProperty(ref _selectedRubric, value))
                    return;

                OnPropertyChanged(nameof(SelectedRubricLabel));
                OnPropertyChanged(nameof(HasSelection));
                RaiseCommandStates();

                // Смена рубрики подгружает её показатели и сбрасывает ранее выбранный показатель.
                // При восстановлении параметров из истории автоподгрузка подавляется:
                // показатели запрашиваются явно в LoadFromHistoryAsync.
                SelectedIndicator = null;
                if (!_suppressRubricAutoLoad)
                    _ = LoadIndicatorsForRubricAsync(value);
            }
        }

        public string SelectedRubricLabel => _selectedRubric == null
            ? "Рубрика не выбрана"
            : $"{(_selectedRubric.Name ?? string.Empty).Trim()} (код {_selectedRubric.Code})";

        /// <summary>Показатели выбранной рубрики либо результаты поиска по каталогу.</summary>
        public ObservableCollection<BelstatIndicator> Indicators { get; }

        public BelstatIndicator? SelectedIndicator
        {
            get => _selectedIndicator;
            set
            {
                if (!SetProperty(ref _selectedIndicator, value))
                    return;

                OnPropertyChanged(nameof(SelectedIndicatorLabel));
                OnPropertyChanged(nameof(HasSelection));
                RaiseCommandStates();
            }
        }

        public string SelectedIndicatorLabel => _selectedIndicator == null
            ? "Показатель не выбран — будут загружены значения всех показателей рубрики"
            : $"Показатель: {_selectedIndicator.Name} (код {_selectedIndicator.Code})";

        /// <summary>
        /// Строка поиска показателей по каталогу Белстата. При каждом изменении текста
        /// выполняется «живой» поиск: фильтруются показатели и дерево рубрик.
        /// </summary>
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (!SetProperty(ref _searchQuery, value))
                    return;

                RefreshSearchResults();
                UpdateLiveSearchStatus();
            }
        }

        /// <summary>
        /// Представление показателей для «живого» поиска: фильтр по вхождению запроса
        /// в название или код (OrdinalIgnoreCase), не более <see cref="LiveSearchLimit"/> совпадений.
        /// </summary>
        public ICollectionView FilteredIndicatorsView { get; }

        /// <summary>Представление дерева рубрик, отфильтрованное по текущему поисковому запросу.</summary>
        public ICollectionView FilteredRubricsView { get; }

        /// <summary>Число показателей, попавших в «живой» поиск (0…<see cref="LiveSearchLimit"/>).</summary>
        public int LiveSearchResultCount => _liveSearchMatches.Count;

        /// <summary>Есть ли результаты «живого» поиска (управляет видимостью выпадающего списка).</summary>
        public bool HasSearchResults => _liveSearchMatches.Count > 0;

        /// <summary>
        /// Показатель, выбранный в выпадающем списке результатов поиска. При установке
        /// автоматически выбирается соответствующая рубрика и показатель, после чего
        /// выделение сбрасывается, чтобы повторный выбор того же пункта снова срабатывал.
        /// </summary>
        public BelstatIndicator? SelectedSearchResult
        {
            get => _selectedSearchResult;
            set
            {
                if (!SetProperty(ref _selectedSearchResult, value))
                    return;

                if (value == null)
                    return;

                ApplySearchResult(value);

                _selectedSearchResult = null;
                OnPropertyChanged(nameof(SelectedSearchResult));
            }
        }

        /// <summary>Есть ли рубрика или показатель для запроса значений.</summary>
        public bool HasSelection => _selectedIndicator != null || _selectedRubric != null;

        /// <summary>Годы, доступные для выбора (от 1990 до текущего года включительно, по убыванию).</summary>
        public List<int> AvailableYears { get; }

        public int FirstYear
        {
            get => _firstYear;
            set => SetProperty(ref _firstYear, value);
        }

        public int LastYear
        {
            get => _lastYear;
            set => SetProperty(ref _lastYear, value);
        }

        // ================= Результат =================

        /// <summary>Таблица значений в «плоском» виде: колонки создаются по измерениям ответа SDMX-JSON.</summary>
        public DataTable? ResultTable
        {
            get => _resultTable;
            private set
            {
                if (SetProperty(ref _resultTable, value))
                    NotifyResultChanged();
            }
        }

        public int ResultRowCount => _resultTable?.Rows.Count ?? 0;

        public string ResultTitle => _resultTitle;

        public string ResultSubtitle => _resultSubtitle;

        /// <summary>Показывать таблицу результатов (есть хотя бы одна строка).</summary>
        public bool IsResultVisible => ResultRowCount > 0;

        /// <summary>Есть ли данные, пригодные для экспорта.</summary>
        public bool HasExportData => _resultRows.Count > 0 && _resultHeaders.Length > 0;

        /// <summary>Подсказка, отображаемая вместо пустой таблицы результатов.</summary>
        public string EmptyResultMessage
        {
            get => _emptyResultMessage;
            private set => SetProperty(ref _emptyResultMessage, value);
        }

        // ================= История запросов =================

        public ObservableCollection<BelstatHistoryItem> HistoryItems { get; }

        // ================= Команды =================

        public RelayCommand LoadRubricsCommand { get; }
        public RelayCommand SearchIndicatorsCommand { get; }
        public RelayCommand ClearSearchCommand { get; }
        public RelayCommand LoadDataCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand RefreshHistoryCommand { get; }
        public RelayCommand ClearHistoryCommand { get; }

        /// <summary>Восстановление параметров запроса (показатель, рубрика, период) из записи истории.</summary>
        public RelayCommand LoadFromHistoryCommand { get; }
        public RelayCommand ExportPdfCommand { get; }
        public RelayCommand ExportExcelCommand { get; }
        public RelayCommand ExportCsvCommand { get; }

        /// <summary>Открытие таблицы текущего результата отдельным окном (крупный просмотр).</summary>
        public RelayCommand OpenInNewWindowCommand { get; }

        private void RaiseCommandStates()
        {
            LoadRubricsCommand.RaiseCanExecuteChanged();
            SearchIndicatorsCommand.RaiseCanExecuteChanged();
            ClearSearchCommand.RaiseCanExecuteChanged();
            LoadDataCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
            RefreshHistoryCommand.RaiseCanExecuteChanged();
            ClearHistoryCommand.RaiseCanExecuteChanged();
            LoadFromHistoryCommand.RaiseCanExecuteChanged();
            ExportPdfCommand.RaiseCanExecuteChanged();
            ExportExcelCommand.RaiseCanExecuteChanged();
            ExportCsvCommand.RaiseCanExecuteChanged();
            OpenInNewWindowCommand.RaiseCanExecuteChanged();
        }

        private void NotifyResultChanged()
        {
            OnPropertyChanged(nameof(ResultRowCount));
            OnPropertyChanged(nameof(ResultTitle));
            OnPropertyChanged(nameof(ResultSubtitle));
            OnPropertyChanged(nameof(IsResultVisible));
            OnPropertyChanged(nameof(HasExportData));
            OnPropertyChanged(nameof(EmptyResultMessage));
            RaiseCommandStates();
        }

        // ================= «Живой» и нечёткий поиск показателей =================

        /// <summary>
        /// Пересчитывает результаты «живого» поиска (первые <see cref="LiveSearchLimit"/> совпадений
        /// по вхождению запроса в название или код) и обновляет представления показателей и рубрик.
        /// </summary>
        private void RefreshSearchResults()
        {
            var query = (_searchQuery ?? string.Empty).Trim();

            _liveSearchMatches.Clear();
            _lastSearchWasFuzzy = false;

            if (query.Length > 0)
            {
                // 1) Точный «живой» фильтр по текущему списку показателей
                //    (вхождение запроса в название или код, OrdinalIgnoreCase).
                foreach (var indicator in Indicators)
                {
                    if (!MatchesQuery(indicator, query))
                        continue;

                    _liveSearchMatches.Add(indicator);
                    if (_liveSearchMatches.Count >= LiveSearchLimit)
                        break;
                }

                // 2) Если точных совпадений нет — предлагаем похожие показатели
                //    из накопленного каталога (нечёткий поиск, порог FuzzyThreshold %).
                if (_liveSearchMatches.Count == 0 && query.Length >= FuzzyMinQueryLength)
                {
                    foreach (var indicator in FuzzySearchIndicators(query))
                    {
                        if (_liveSearchMatches.Count >= LiveSearchLimit)
                            break;

                        if (_liveSearchMatches.Add(indicator))
                            _lastSearchWasFuzzy = true;
                    }
                }
            }

            FilterIndicatorsViewRefresh();
            FilterRubricTree(query);

            OnPropertyChanged(nameof(LiveSearchResultCount));
            OnPropertyChanged(nameof(HasSearchResults));
        }

        /// <summary>Обновляет статус по результатам «живого» поиска (пустой запрос статус не трогает).</summary>
        private void UpdateLiveSearchStatus()
        {
            var query = (_searchQuery ?? string.Empty).Trim();
            if (query.Length == 0)
                return;

            StatusMessage = _liveSearchMatches.Count == 0
                ? NothingFoundMessage
                : _lastSearchWasFuzzy
                    ? NoExactMatchMessage
                    : $"Найдено показателей: {_liveSearchMatches.Count}.";
        }

        /// <summary>
        /// Фильтрация дерева рубрик по поисковому запросу: рекурсивно проходим дерево и
        /// оставляем рубрику, если запрос встречается в её названии либо в названии любого потомка.
        /// </summary>
        public void FilterRubricTree(string query)
        {
            _rubricFilterQuery = (query ?? string.Empty).Trim();
            FilteredRubricsView.Refresh();
        }

        /// <summary>
        /// Нечёткий поиск похожих показателей по накопленному каталогу (FuzzySharp.Process.ExtractTop,
        /// порог <see cref="FuzzyThreshold"/> %). Возвращает до <see cref="FuzzyTopCount"/> наиболее похожих.
        /// </summary>
        public List<BelstatIndicator> FuzzySearchIndicators(string query)
        {
            var needle = (query ?? string.Empty).Trim();
            if (needle.Length == 0)
                return new List<BelstatIndicator>();

            var pool = _indicatorPool.Values
                .Where(i => !string.IsNullOrWhiteSpace(i.Name))
                .ToList();
            if (pool.Count == 0)
                return new List<BelstatIndicator>();

            var names = pool.Select(i => i.Name).ToList();

            // Предпроцессор оставляем кириллице-безопасным: стандартный обработчик FuzzySharp
            // удаляет символы, отличные от латиницы, поэтому передаём собственную нормализацию.
            var matches = Process.ExtractTop(
                needle,
                names,
                processor: s => s.ToLowerInvariant(),
                limit: FuzzyTopCount,
                cutoff: FuzzyThreshold);

            return matches.Select(m => pool[m.Index]).ToList();
        }

        /// <summary>Фильтр «живого» поиска для <see cref="FilteredIndicatorsView"/>.</summary>
        private bool FilterIndicatorsView(object item) =>
            item is BelstatIndicator indicator && _liveSearchMatches.Contains(indicator);

        /// <summary>Фильтр дерева рубрик для <see cref="FilteredRubricsView"/>.</summary>
        private bool FilterRubricsView(object item)
        {
            if (item is not BelstatRubric rubric)
                return false;

            if (_rubricFilterQuery.Length == 0)
                return true;

            // Выбранную (или программно выбираемую) рубрику оставляем в списке, чтобы не потерять
            // текущий выбор ComboBox и не сбросить показатели при вводе поискового запроса.
            if (ReferenceEquals(rubric, _selectedRubric) || ReferenceEquals(rubric, _pendingSelectRubric))
                return true;

            return SubtreeMatches(rubric, _rubricFilterQuery);
        }

        private void FilterIndicatorsViewRefresh() => FilteredIndicatorsView.Refresh();

        /// <summary>Рекурсивная проверка: встречается ли запрос в рубрике или в любом её потомке.</summary>
        private static bool SubtreeMatches(BelstatRubric rubric, string query)
        {
            if (rubric.Name?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            foreach (var child in rubric.Children)
            {
                if (SubtreeMatches(child, query))
                    return true;
            }

            return false;
        }

        /// <summary>Вхождение запроса в название или код показателя (OrdinalIgnoreCase).</summary>
        private static bool MatchesQuery(BelstatIndicator indicator, string query) =>
            indicator.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            || indicator.Code.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Запрос состоит только из цифр (поиск по коду показателя).</summary>
        private static bool IsNumericQuery(string query) =>
            query.Length > 0 && query.All(char.IsDigit);

        // ================= Загрузка данных из API Белстата =================

        private async Task LoadRubricsAsync()
        {
            try
            {
                StatusMessage = "Загрузка дерева рубрик Белстата...";
                var rubrics = await _belstatApiService.GetRubricTreeAsync();

                Rubrics.Clear();
                foreach (var rubric in rubrics)
                    Rubrics.Add(rubric);

                StatusMessage = Rubrics.Count == 0
                    ? "Справочник рубрик пуст: сервис Белстата не вернул данные."
                    : $"Дерево рубрик загружено: {Rubrics.Count} разделов.";
                _logger.LogInformation("Дерево рубрик Белстата загружено: {Count} узлов", Rubrics.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке дерева рубрик Белстата");
                StatusMessage = $"Ошибка загрузки рубрик: {ex.Message}";
                _notifications.ShowError($"Не удалось загрузить рубрики Белстата: {ex.Message}");
            }
        }

        private async Task LoadIndicatorsForRubricAsync(BelstatRubric? rubric)
        {
            if (rubric == null || string.IsNullOrWhiteSpace(rubric.Code))
            {
                _indicatorsFromSearch = false;
                Indicators.Clear();
                RefreshSearchResults();
                return;
            }

            if (IsLoading)
                return;

            IsLoading = true;
            try
            {
                StatusMessage = $"Загрузка показателей рубрики «{rubric.Name}»...";
                var indicators = await _belstatApiService.GetIndicatorsByRubricAsync(rubric.Code);

                _indicatorsFromSearch = false;
                Indicators.Clear();
                foreach (var indicator in indicators)
                {
                    indicator.RubricCode = rubric.Code;
                    Indicators.Add(indicator);
                }

                // Пополняем каталог для нечёткого поиска и пересчитываем «живой» поиск
                // под новый набор показателей (введённый запрос при этом не сбрасывается).
                RememberIndicators(Indicators);
                RefreshSearchResults();

                StatusMessage = Indicators.Count == 0
                    ? $"Рубрика «{rubric.Name}» не содержит показателей."
                    : $"Показателей в рубрике: {Indicators.Count}.";
                _logger.LogInformation("Показатели рубрики {Code} загружены: {Count}", rubric.Code, Indicators.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке показателей рубрики {Code}", rubric.Code);
                StatusMessage = $"Ошибка загрузки показателей: {ex.Message}";
                _notifications.ShowError($"Не удалось загрузить показатели рубрики: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Поиск показателей по каталогу Белстата. Пустой запрос возвращает список
        /// показателей выбранной рубрики (сброс поиска).
        /// </summary>
        private async Task SearchIndicatorsAsync()
        {
            if (IsLoading)
                return;

            var query = (_searchQuery ?? string.Empty).Trim();
            if (query.Length == 0)
            {
                await LoadIndicatorsForRubricAsync(_selectedRubric);
                return;
            }

            IsLoading = true;
            try
            {
                StatusMessage = $"Поиск показателей по запросу «{query}»...";
                var indicators = await _belstatApiService.SearchIndicatorsAsync(query);

                // Числовой запрос трактуем как поиск по коду показателя.
                if (IsNumericQuery(query))
                {
                    indicators = indicators
                        .Where(i => i.Code.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                }

                if (indicators.Count > 0)
                {
                    _indicatorsFromSearch = true;
                    ReplaceIndicators(indicators);
                    StatusMessage = $"Найдено показателей: {Indicators.Count}.";
                    _logger.LogInformation("Поиск показателей Белстата по запросу «{Query}»: {Count}", query, indicators.Count);
                }
                else
                {
                    // Точных совпадений нет — пробуем нечёткий поиск по названию.
                    var similar = query.Length >= FuzzyMinQueryLength
                        ? FuzzySearchIndicators(query)
                        : new List<BelstatIndicator>();

                    _indicatorsFromSearch = similar.Count > 0;
                    ReplaceIndicators(similar);

                    if (similar.Count > 0)
                    {
                        StatusMessage = NoExactMatchMessage;
                        _logger.LogInformation(
                            "Точных совпадений по запросу «{Query}» нет, найдены похожие показатели: {Count}",
                            query, similar.Count);
                    }
                    else
                    {
                        StatusMessage = NothingFoundMessage;
                        _logger.LogInformation("По запросу «{Query}» показатели не найдены", query);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при поиске показателей Белстата");
                StatusMessage = $"Ошибка поиска показателей: {ex.Message}";
                _notifications.ShowError($"Не удалось выполнить поиск показателей: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Применяет выбранный в выпадающем списке результат поиска: подставляет показатель
        /// в список, автоматически выбирает соответствующую рубрику и выделяет показатель.
        /// </summary>
        private void ApplySearchResult(BelstatIndicator indicator)
        {
            var rubric = FindRubricForIndicator(indicator);

            if (rubric != null && !ReferenceEquals(rubric, _selectedRubric))
            {
                // Рубрику выбираем с подавлением автоподгрузки: её показатели загрузим сами
                // и только после этого восстановим выбор показателя, иначе асинхронная
                // загрузка показателей рубрики вытеснит выбранный из поиска показатель.
                _pendingSelectRubric = rubric;
                _suppressRubricAutoLoad = true;
                try
                {
                    // Оставляем рубрику в отфильтрованном списке перед программным выбором,
                    // иначе ComboBox не сможет её выделить.
                    FilteredRubricsView.Refresh();
                    SelectedRubric = rubric;
                }
                finally
                {
                    _suppressRubricAutoLoad = false;
                    _pendingSelectRubric = null;
                }

                _ = LoadRubricIndicatorsAndSelectAsync(rubric, indicator);
                return;
            }

            SelectIndicator(indicator);
        }

        /// <summary>
        /// Загружает показатели рубрики и после этого выделяет показатель, выбранный в
        /// результатах поиска (загрузка списка показателей рубрики не должна сбрасывать выбор).
        /// </summary>
        private async Task LoadRubricIndicatorsAndSelectAsync(BelstatRubric rubric, BelstatIndicator indicator)
        {
            await LoadIndicatorsForRubricAsync(rubric);
            SelectIndicator(indicator);
        }

        /// <summary>
        /// Выделяет показатель в списке, при необходимости добавляя его. Показатель ищется по коду:
        /// загруженный из API экземпляр — не тот же объект, что найден поиском, а дубликат
        /// в списке показателей недопустим.
        /// </summary>
        private void SelectIndicator(BelstatIndicator indicator)
        {
            var selected = Indicators.FirstOrDefault(
                i => string.Equals(i.Code, indicator.Code, StringComparison.OrdinalIgnoreCase));

            if (selected == null)
            {
                Indicators.Add(indicator);
                RememberIndicators(new[] { indicator });
                selected = indicator;
            }

            SelectedIndicator = selected;
            StatusMessage = $"Выбран показатель «{selected.Name}».";
        }

        /// <summary>Ищет рубрику, к которой относится показатель (по коду рубрики или каталогу).</summary>
        private BelstatRubric? FindRubricForIndicator(BelstatIndicator indicator)
        {
            var rubricCode = indicator.RubricCode;

            if (string.IsNullOrWhiteSpace(rubricCode)
                && _indicatorPool.TryGetValue(indicator.Code, out var known))
            {
                rubricCode = known.RubricCode;
            }

            if (string.IsNullOrWhiteSpace(rubricCode))
                return null;

            return FindRubricByCode(Rubrics, rubricCode);
        }

        /// <summary>Рекурсивный поиск рубрики по её коду в дереве.</summary>
        private static BelstatRubric? FindRubricByCode(IEnumerable<BelstatRubric> rubrics, string code)
        {
            foreach (var rubric in rubrics)
            {
                if (string.Equals(rubric.Code, code, StringComparison.OrdinalIgnoreCase))
                    return rubric;

                var found = FindRubricByCode(rubric.Children, code);
                if (found != null)
                    return found;
            }

            return null;
        }


        /// <summary>
        /// Сбрасывает поисковый запрос и результаты «живого» поиска. Если список показателей
        /// был получен поиском по каталогу, возвращает показатели выбранной рубрики.
        /// </summary>
        private void ClearSearch()
        {
            // Значение меняем напрямую (без сеттера), чтобы «живой» поиск не срабатывал
            // дважды: ниже выполняется единый пересчёт фильтров и представлений.
            SetProperty(ref _searchQuery, string.Empty, nameof(SearchQuery));

            _selectedSearchResult = null;
            OnPropertyChanged(nameof(SelectedSearchResult));

            // Единый пересчёт: снимает фильтры с показателей и дерева рубрик.
            RefreshSearchResults();

            if (_indicatorsFromSearch && _selectedRubric != null)
            {
                _ = LoadIndicatorsForRubricAsync(_selectedRubric);
            }
            else
            {
                StatusMessage = SearchClearedMessage;
            }
        }

        /// <summary>
        /// Заменяет список показателей переданной коллекцией, пополняя каталог для
        /// нечёткого поиска, и пересчитывает результаты «живого» поиска.
        /// </summary>
        private void ReplaceIndicators(IEnumerable<BelstatIndicator> indicators)
        {
            Indicators.Clear();
            foreach (var indicator in indicators)
                Indicators.Add(indicator);

            RememberIndicators(Indicators);
            RefreshSearchResults();
        }

        /// <summary>Запоминает показатели в каталоге (код → показатель) для нечёткого поиска.</summary>
        private void RememberIndicators(IEnumerable<BelstatIndicator> indicators)
        {
            foreach (var indicator in indicators)
            {
                if (!string.IsNullOrWhiteSpace(indicator.Code))
                    _indicatorPool[indicator.Code] = indicator;
            }
        }

        /// <summary>
        /// Загрузка значений показателя (если он выбран) либо всех показателей рубрики
        /// за указанный период с построением плоской таблицы результатов.
        /// </summary>
        private async Task LoadDataAsync()
        {
            if (IsLoading)
                return;

            var years = BuildYears();
            if (years.Length == 0)
            {
                _notifications.ShowWarning(
                    $"Укажите корректный период: не ранее {MinYear} года и не позднее {DateTimeHelper.TodayBelarus.Year + 1} года.");
                return;
            }

            var indicator = _selectedIndicator;
            var rubric = _selectedRubric;
            if (indicator == null && rubric == null)
            {
                _notifications.ShowWarning("Выберите рубрику или показатель для запроса данных.");
                return;
            }

            CancelLoading();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IsLoading = true;
            try
            {
                var source = indicator != null
                    ? $"показателя «{indicator.Name}»"
                    : $"рубрики «{(rubric!.Name ?? string.Empty).Trim()}»";
                StatusMessage = $"Запрос значений {source} за {years[0]}–{years[^1]} гг...";

                BelstatDataResponse? response = indicator != null
                    ? await _belstatApiService.GetIndicatorDataAsync(indicator.Code, years, token)
                    : await _belstatApiService.GetRubricDataAsync(rubric!.Code, years, token);

                if (response == null)
                {
                    ClearResult("API Белстата не вернул данные за выбранный период. Измените период или выберите другой показатель.");
                    StatusMessage = "API Белстата не вернул данные за выбранный период.";
                    _notifications.ShowWarning(StatusMessage);
                    return;
                }

                var (headers, rows) = BuildRowsFromResponse(response);
                if (rows.Count == 0)
                {
                    ClearResult("В ответе Белстата нет наблюдений за выбранный период.");
                    StatusMessage = "В ответе Белстата нет наблюдений за выбранный период.";
                    _notifications.ShowWarning(StatusMessage);
                    return;
                }

                var title = indicator != null
                    ? $"Показатель государственной статистики: {indicator.Name}"
                    : $"Показатели рубрики государственной статистики: {(rubric!.Name ?? string.Empty).Trim()}";
                var subtitle = $"Период: {years[0]}–{years[^1]} гг. Источник данных: {DataSourceText}";

                ApplyResult(headers, rows, title, subtitle);

                StatusMessage = $"Загружено значений: {rows.Count}.";
                _logger.LogInformation("Данные Белстата загружены: строк {Rows}, колонок {Columns}", rows.Count, headers.Length);

                await SaveHistoryAsync(indicator, rubric, years);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Запрос данных Белстата отменён.";
                _logger.LogInformation("Запрос данных Белстата отменён пользователем");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении данных Белстата");
                StatusMessage = $"Ошибка получения данных: {ex.Message}";
                _notifications.ShowError($"Не удалось получить данные Белстата: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>Отмена текущего запроса к API Белстата (кнопка «Отменить»).</summary>
        private void CancelLoading()
        {
            try
            {
                _cts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Источник отмены уже освобождён — отменять нечего.
            }
        }

        /// <summary>
        /// Открывает таблицу текущего результата отдельным окном (кнопка «Открыть в окне»).
        /// На вкладке ширина таблицы ограничена шириной колонки, поэтому широкие таблицы
        /// Белстата (много измерений) удобнее смотреть в отдельном окне. Окно создаётся
        /// контейнером DI и получает этот же экземпляр ViewModel, то есть показывает
        /// ровно тот результат, что и таблица на вкладке.
        /// </summary>
        private void OpenInNewWindow()
        {
            if (!IsResultVisible)
            {
                _notifications.ShowWarning(ResultWindowEmptyMessage);
                return;
            }

            try
            {
                var window = App.ServiceProvider?.GetRequiredService<Views.BelstatDataWindow>();
                if (window == null)
                {
                    _logger.LogWarning("App.ServiceProvider не инициализирован, окно таблицы не открыто");
                    _notifications.ShowError(ResultWindowServiceProviderMessage);
                    return;
                }

                window.Owner = Application.Current?.MainWindow;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при открытии таблицы результата в отдельном окне");
                _notifications.ShowError($"Ошибка при открытии таблицы в отдельном окне: {ex.Message}");
            }
        }

        // ================= Построение таблицы результата =================

        private void ApplyResult(string[] headers, List<string[]> rows, string title, string subtitle)
        {
            _resultHeaders = headers;
            _resultRows = rows;
            _resultTitle = title;
            _resultSubtitle = subtitle;
            EmptyResultMessage = $"Данные загружены: строк — {rows.Count}, колонок — {headers.Length}.";
            ResultTable = BuildDataTable(headers, rows);
        }

        private void ClearResult(string message)
        {
            _resultHeaders = Array.Empty<string>();
            _resultRows = new List<string[]>();
            _resultTitle = string.Empty;
            _resultSubtitle = string.Empty;
            EmptyResultMessage = message;
            ResultTable = null;
        }

        /// <summary>
        /// Преобразует ответ SDMX-JSON в плоскую таблицу: каждая колонка — измерение наблюдения,
        /// последняя колонка — значение показателя. Строки сортируются по порядку измерений API.
        /// Значения переносятся «как есть» (без арифметических преобразований).
        /// Подписи измерений и расшифровки значений при необходимости переводятся на русский язык
        /// (BelstatLocalizationHelper — резервный словарь, если справочник API отдал английские подписи).
        /// </summary>
        private static (string[] Headers, List<string[]> Rows) BuildRowsFromResponse(BelstatDataResponse response)
        {
            var dimensions = response.Structure?.Dimensions?.Observation ?? new List<BelstatDimension>();
            var headers = BuildHeaders(dimensions);
            var rows = new List<(int[] Sort, string[] Cells)>();

            foreach (var dataSet in response.DataSets ?? new List<BelstatDataSet>())
            {
                foreach (var observation in dataSet.Observations)
                {
                    var parts = observation.Key.Split(':');
                    var cells = new string[dimensions.Count];
                    var sort = new int[dimensions.Count];

                    for (var i = 0; i < dimensions.Count; i++)
                    {
                        var raw = i < parts.Length ? parts[i] : UnspecifiedIndex;
                        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                            && index >= 0 && index < dimensions[i].Values.Count)
                        {
                            var valueCaption = dimensions[i].Values[index].Caption;
                            cells[i] = BelstatLocalizationHelper.TranslateValueName(valueCaption) ?? valueCaption;
                            sort[i] = index;
                        }
                        else
                        {
                            // Значение измерения не задано (служебный индекс "-1").
                            cells[i] = UnspecifiedCell;
                            sort[i] = int.MaxValue;
                        }
                    }

                    var value = observation.Value is { Length: > 0 } ? observation.Value[0] : string.Empty;
                    rows.Add((sort, cells.Append(value).ToArray()));
                }
            }

            rows.Sort((left, right) => CompareKeys(left.Sort, right.Sort));
            return (headers, rows.Select(row => row.Cells).ToList());
        }

        private static string[] BuildHeaders(IReadOnlyList<BelstatDimension> dimensions)
        {
            var headers = new List<string>();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < dimensions.Count; i++)
            {
                var caption = dimensions[i].Caption;
                if (string.IsNullOrWhiteSpace(caption))
                    caption = $"Колонка {i + 1}";
                else
                    caption = BelstatLocalizationHelper.TranslateDimensionName(caption) ?? caption;

                headers.Add(MakeUnique(caption.Trim(), used));
            }

            headers.Add(MakeUnique(ValuesColumnName, used));
            return headers.ToArray();
        }

        private static string MakeUnique(string caption, HashSet<string> used)
        {
            var candidate = caption;
            var suffix = 2;
            while (!used.Add(candidate))
                candidate = $"{caption} ({suffix++})";

            return candidate;
        }

        private static int CompareKeys(IReadOnlyList<int> left, IReadOnlyList<int> right)
        {
            var length = Math.Min(left.Count, right.Count);
            for (var i = 0; i < length; i++)
            {
                var compare = left[i].CompareTo(right[i]);
                if (compare != 0)
                    return compare;
            }

            return left.Count.CompareTo(right.Count);
        }

        private static DataTable BuildDataTable(string[] headers, List<string[]> rows)
        {
            var table = new DataTable();
            foreach (var header in headers)
                table.Columns.Add(header, typeof(string));

            foreach (var row in rows)
            {
                var dataRow = table.NewRow();
                for (var i = 0; i < headers.Length && i < row.Length; i++)
                    dataRow[i] = row[i] ?? string.Empty;

                table.Rows.Add(dataRow);
            }

            return table;
        }

        // ================= История запросов =================

        private async Task LoadHistoryAsync()
        {
            try
            {
                var items = await _belstatHistoryRepository.GetAllAsync();

                HistoryItems.Clear();
                foreach (var item in items.Take(500))
                    HistoryItems.Add(item);

                RaiseCommandStates();
                _logger.LogInformation("История запросов госстатистики загружена: {Count} записей", HistoryItems.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке истории запросов госстатистики");
                _notifications.ShowError($"Не удалось загрузить историю запросов: {ex.Message}");
            }
        }

        /// <summary>
        /// Восстанавливает параметры запроса (рубрику, показатель и период) из выбранной записи
        /// истории: значения показателя берутся из локальной базы данных приложения, поэтому
        /// актуальность данных подтверждает пользователь повторным запросом «Получить данные».
        /// Для записей старше <see cref="HistoryStaleDays"/> дней выводится предупреждение.
        /// </summary>
        private async Task LoadFromHistoryAsync(BelstatHistoryItem? item)
        {
            if (item == null)
            {
                _notifications.ShowWarning("Выберите запись в списке истории запросов госстатистики.");
                return;
            }

            var (firstYear, lastYear) = ParseYears(item.Years);
            var upperBound = DateTimeHelper.TodayBelarus.Year + 1;

            if (firstYear > 0)
                FirstYear = Math.Clamp(firstYear, MinYear, upperBound);

            if (lastYear > 0)
                LastYear = Math.Clamp(lastYear, MinYear, upperBound);

            var rubric = string.IsNullOrWhiteSpace(item.RubricCode)
                ? null
                : Rubrics.FirstOrDefault(r => string.Equals(r.Code, item.RubricCode, StringComparison.OrdinalIgnoreCase));

            if (rubric != null)
            {
                // Автоподгрузка показателей подавляется: запрос выполняется ниже, явным вызовом.
                _suppressRubricAutoLoad = true;
                try
                {
                    SelectedRubric = rubric;
                }
                finally
                {
                    _suppressRubricAutoLoad = false;
                }

                await LoadIndicatorsForRubricAsync(rubric);
            }

            SelectedIndicator = ResolveIndicator(item);

            StatusMessage = HistoryLoadedMessage;
            _logger.LogInformation(
                "Параметры запроса восстановлены из истории госстатистики: рубрика {Rubric}, показатель {Indicator}, период {Years}",
                item.RubricCode, item.IndicatorCode, item.Years);

            var ageDays = (DateTimeHelper.TodayBelarus - item.Timestamp).TotalDays;
            if (ageDays > HistoryStaleDays)
            {
                _notifications.ShowWarning(
                    $"Запись истории от {item.Timestamp:dd.MM.yyyy} старше {HistoryStaleDays} дней. " +
                    "Данные за такой период могли измениться — выполните запрос повторно.");
            }
        }

        /// <summary>
        /// Находит показатель записи истории в текущем списке показателей. Если показатель
        /// отсутствует в каталоге Белстата, в список добавляется строка с сохранённым названием,
        /// чтобы восстановленные параметры полностью отобразились в интерфейсе.
        /// </summary>
        private BelstatIndicator? ResolveIndicator(BelstatHistoryItem item)
        {
            if (string.IsNullOrWhiteSpace(item.IndicatorCode))
                return null;

            var indicator = Indicators.FirstOrDefault(
                i => string.Equals(i.Code, item.IndicatorCode, StringComparison.OrdinalIgnoreCase));

            if (indicator != null)
                return indicator;

            indicator = new BelstatIndicator
            {
                Code = item.IndicatorCode,
                Name = string.IsNullOrWhiteSpace(item.IndicatorName) ? item.IndicatorCode : item.IndicatorName,
                RubricCode = item.RubricCode
            };
            Indicators.Insert(0, indicator);

            return indicator;
        }

        /// <summary>Разбирает строку периода из истории ("2021-2023" либо "2023") в границы периода.</summary>
        private static (int First, int Last) ParseYears(string? years)
        {
            if (string.IsNullOrWhiteSpace(years))
                return (0, 0);

            var parts = years.Split(new[] { '-', '–', '—', ';', ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            int first = 0;
            int last = 0;

            foreach (var part in parts)
            {
                if (!int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var year))
                    continue;

                if (first == 0)
                    first = year;

                last = year;
            }

            return (first, last);
        }

        private async Task ClearHistoryAsync()
        {
            if (MessageBox.Show("Очистить историю запросов государственной статистики?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                await _belstatHistoryRepository.ClearAsync();
                HistoryItems.Clear();
                RaiseCommandStates();
                StatusMessage = "История запросов очищена.";
                _notifications.ShowSuccess("История запросов государственной статистики очищена.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при очистке истории запросов госстатистики");
                _notifications.ShowError($"Не удалось очистить историю запросов: {ex.Message}");
            }
        }

        /// <summary>Сохраняет в историю факт выполненного запроса значений.</summary>
        private async Task SaveHistoryAsync(BelstatIndicator? indicator, BelstatRubric? rubric, int[] years)
        {
            var rubricName = (rubric?.Name ?? string.Empty).Trim();
            var item = new BelstatHistoryItem
            {
                RubricCode = rubric?.Code ?? indicator?.RubricCode ?? string.Empty,
                IndicatorCode = indicator?.Code ?? string.Empty,
                IndicatorName = indicator?.Name ?? (rubricName.Length > 0 ? rubricName : rubric?.Code ?? string.Empty),
                Years = FormatYears(years),
                Timestamp = DateTimeHelper.NowBelarus
            };

            await AddHistoryItemAsync(item);
        }

        private async Task AddHistoryItemAsync(BelstatHistoryItem item)
        {
            try
            {
                await _belstatHistoryRepository.AddAsync(item);
                HistoryItems.Insert(0, item);
                RaiseCommandStates();
            }
            catch (Exception ex)
            {
                // Ошибка журналирования не должна прерывать основной сценарий загрузки данных.
                _logger.LogError(ex, "Ошибка при сохранении записи в историю запросов госстатистики");
            }
        }

        // ================= Вспомогательные методы =================

        /// <summary>Список годов запроса (по возрастанию) с ограничением допустимого диапазона.</summary>
        private int[] BuildYears()
        {
            var upperBound = DateTimeHelper.TodayBelarus.Year + 1;
            var first = Math.Clamp(Math.Min(_firstYear, _lastYear), MinYear, upperBound);
            var last = Math.Clamp(Math.Max(_firstYear, _lastYear), MinYear, upperBound);

            var years = new List<int>();
            for (var year = first; year <= last; year++)
                years.Add(year);

            return years.ToArray();
        }

        private static List<int> BuildAvailableYears()
        {
            var upperBound = DateTimeHelper.TodayBelarus.Year + 1;
            var years = new List<int>();
            for (var year = upperBound; year >= MinYear; year--)
                years.Add(year);

            return years;
        }

        private static string FormatYears(int[] years) => years.Length switch
        {
            0 => string.Empty,
            1 => years[0].ToString(CultureInfo.InvariantCulture),
            _ => $"{years[0]}-{years[^1]}"
        };

        // ================= Экспорт =================

        private async Task ExportPdfAsync()
        {
            if (!HasExportData)
            {
                _notifications.ShowInfo("Нет данных для экспорта.");
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF файлы (*.pdf)|*.pdf",
                FileName = BuildFileName("Госстатистика"),
                DefaultExt = "pdf"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                StatusMessage = "Экспорт в PDF...";
                _logger.LogInformation("Начало экспорта данных госстатистики в PDF: {Path}", saveDialog.FileName);

                await _pdfExportService.ExportBelstatDataAsync(
                    _resultTitle, _resultSubtitle, _resultHeaders, _resultRows, saveDialog.FileName);

                StatusMessage = "Экспорт в PDF завершен.";
                _notifications.ShowSuccess($"Файл PDF успешно сохранен!\n{saveDialog.FileName}");
                _logger.LogInformation("Экспорт в PDF завершен: {Path}", saveDialog.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте данных госстатистики в PDF");
                _notifications.ShowError($"Ошибка при экспорте в PDF: {ex.Message}");
            }
        }

        private async Task ExportExcelAsync()
        {
            if (!HasExportData)
            {
                _notifications.ShowInfo("Нет данных для экспорта.");
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                FileName = BuildFileName("Госстатистика"),
                DefaultExt = "xlsx"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                StatusMessage = "Экспорт в Excel...";
                _logger.LogInformation("Начало экспорта данных госстатистики в Excel: {Path}", saveDialog.FileName);

                var headers = _resultHeaders;
                var rows = _resultRows;

                OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal(ExcelLicenseHolder);

                using var package = new OfficeOpenXml.ExcelPackage();
                var sheet = package.Workbook.Worksheets.Add("Госстатистика");

                sheet.Cells[1, 1].Value = _resultTitle;
                sheet.Cells[1, 1, 1, headers.Length].Merge = true;
                sheet.Cells[1, 1].Style.Font.Bold = true;

                if (!string.IsNullOrWhiteSpace(_resultSubtitle))
                {
                    sheet.Cells[2, 1].Value = _resultSubtitle;
                    sheet.Cells[2, 1, 2, headers.Length].Merge = true;
                }

                for (int c = 0; c < headers.Length; c++)
                    sheet.Cells[3, c + 1].Value = headers[c];

                for (int r = 0; r < rows.Count; r++)
                {
                    for (int c = 0; c < rows[r].Length && c < headers.Length; c++)
                        sheet.Cells[r + 4, c + 1].Value = rows[r][c];
                }

                sheet.Cells[3, 1, 3, headers.Length].Style.Font.Bold = true;
                sheet.Cells[3, 1, Math.Max(3, rows.Count + 3), headers.Length].Style.Border
                    .BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                sheet.Cells.AutoFitColumns();

                await Task.Run(() => package.SaveAs(new System.IO.FileInfo(saveDialog.FileName)));

                StatusMessage = "Экспорт в Excel завершен.";
                _notifications.ShowSuccess($"Файл Excel успешно сохранен!\n{saveDialog.FileName}");
                _logger.LogInformation("Экспорт в Excel завершен: {Path}", saveDialog.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте данных госстатистики в Excel");
                _notifications.ShowError($"Ошибка при экспорте в Excel: {ex.Message}");
            }
        }

        private async Task ExportCsvAsync()
        {
            if (!HasExportData)
            {
                _notifications.ShowInfo("Нет данных для экспорта.");
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV файлы (*.csv)|*.csv",
                FileName = BuildFileName("Госстатистика"),
                DefaultExt = "csv"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                StatusMessage = "Экспорт в CSV...";

                var sb = new StringBuilder();
                sb.AppendLine(CsvEscape(_resultTitle));
                if (!string.IsNullOrWhiteSpace(_resultSubtitle))
                    sb.AppendLine(CsvEscape(_resultSubtitle));
                sb.AppendLine(string.Join(";", _resultHeaders.Select(CsvEscape)));
                foreach (var row in _resultRows)
                    sb.AppendLine(string.Join(";", row.Select(CsvEscape)));

                await File.WriteAllTextAsync(saveDialog.FileName, sb.ToString(),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

                StatusMessage = "Экспорт в CSV завершен.";
                _notifications.ShowSuccess($"Файл CSV успешно сохранен!\n{saveDialog.FileName}");
                _logger.LogInformation("Экспорт в CSV завершен: {Path}", saveDialog.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте данных госстатистики в CSV");
                _notifications.ShowError($"Ошибка при экспорте в CSV: {ex.Message}");
            }
        }

        private static string BuildFileName(string baseName)
        {
            string name = $"{baseName}_{DateTimeHelper.NowBelarus:yyyy-MM-dd_HH-mm}";
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');

            return name;
        }

        private static string CsvEscape(string? value)
        {
            value ??= string.Empty;
            if (value.IndexOf(';') >= 0 || value.IndexOf('"') >= 0 || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
                return "\"" + value.Replace("\"", "\"\"") + "\"";

            return value;
        }
    }
}
