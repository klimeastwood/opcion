using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NalogParserWPF.Database;
using NalogParserWPF.Helpers;
using NalogParserWPF.Models;
using NalogParserWPF.Services;

namespace NalogParserWPF.ViewModels
{
    /// <summary>
    /// Вкладка «Курсы валют»: официальные курсы НБ РБ (одна валюта на дату,
    /// все валюты на дату, динамика курса за период) с экспортом в PDF, Excel и CSV
    /// и историей выполненных запросов.
    /// </summary>
    public class ExchangeRateViewModel : ViewModelBase
    {
        private const string DefaultCurrencyCode = "USD";
        private const string ExcelLicenseHolder = "NalogParserWPF";

        private readonly INBRBApiService _nbrbApiService;
        private readonly IExchangeHistoryRepository _exchangeHistoryRepository;
        private readonly IPdfExportService _pdfExportService;
        private readonly INotificationService _notifications;
        private readonly ILogger<ExchangeRateViewModel> _logger;

        private bool _isInitialized;
        private bool _isLoading;

        private string _statusMessage = "Готов к загрузке курсов НБ РБ.";
        private Currency? _selectedCurrency;
        private DateTime _selectedDate = DateTimeHelper.TodayBelarus;
        private DateTime _startDate = DateTimeHelper.TodayBelarus.AddMonths(-1);
        private DateTime _endDate = DateTimeHelper.TodayBelarus;
        private Rate? _currentRate;

        private string _currencyFilter = string.Empty;

        private bool _isSingleRateMode = true;
        private bool _isAllRatesMode;
        private bool _isDynamicsMode;

        public ExchangeRateViewModel(
            INBRBApiService nbrbApiService,
            IExchangeHistoryRepository exchangeHistoryRepository,
            IPdfExportService pdfExportService,
            INotificationService notifications,
            ILogger<ExchangeRateViewModel> logger)
        {
            _nbrbApiService = nbrbApiService;
            _exchangeHistoryRepository = exchangeHistoryRepository;
            _pdfExportService = pdfExportService;
            _notifications = notifications;
            _logger = logger;

            Currencies = new ObservableCollection<Currency>();
            FilteredCurrencies = new ObservableCollection<Currency>();
            AllRatesOnDate = new ObservableCollection<Rate>();
            Dynamics = new ObservableCollection<RateShort>();
            HistoryItems = new ObservableCollection<ExchangeHistoryItem>();

            LoadCurrenciesCommand = new RelayCommand(_ => _ = LoadCurrenciesAsync(), _ => !IsLoading);
            LoadSingleRateCommand = new RelayCommand(_ => _ = LoadSingleRateAsync(), _ => !IsLoading);
            LoadAllRatesCommand = new RelayCommand(_ => _ = LoadAllRatesAsync(), _ => !IsLoading);
            LoadDynamicsCommand = new RelayCommand(_ => _ = LoadDynamicsAsync(), _ => !IsLoading);
            ClearHistoryCommand = new RelayCommand(_ => _ = ClearHistoryAsync(), _ => HistoryItems.Count > 0);
            ApplyCurrencyFilterCommand = new RelayCommand(_ => ApplyCurrencyFilter());
            ExportPdfCommand = new RelayCommand(_ => _ = ExportPdfAsync(), _ => HasExportData);
            ExportExcelCommand = new RelayCommand(_ => _ = ExportExcelAsync(), _ => HasExportData);
            ExportCsvCommand = new RelayCommand(_ => _ = ExportCsvAsync(), _ => HasExportData);
        }

        /// <summary>
        /// Первичная загрузка справочника валют и истории запросов.
        /// Вызывается один раз при первом открытии вкладки.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            _isInitialized = true;
            await LoadCurrenciesAsync();
            await LoadHistoryAsync();
        }

        // ================= Режим работы =================

        /// <summary>Режим «Курс одной валюты на дату».</summary>
        public bool IsSingleRateMode
        {
            get => _isSingleRateMode;
            set
            {
                if (value)
                    SetMode(single: true, all: false, dynamics: false);
                else if (_isSingleRateMode)
                    OnPropertyChanged(nameof(IsSingleRateMode));
            }
        }

        /// <summary>Режим «Курсы всех валют на дату».</summary>
        public bool IsAllRatesMode
        {
            get => _isAllRatesMode;
            set
            {
                if (value)
                    SetMode(single: false, all: true, dynamics: false);
                else if (_isAllRatesMode)
                    OnPropertyChanged(nameof(IsAllRatesMode));
            }
        }

        /// <summary>Режим «Динамика курса за период».</summary>
        public bool IsDynamicsMode
        {
            get => _isDynamicsMode;
            set
            {
                if (value)
                    SetMode(single: false, all: false, dynamics: true);
                else if (_isDynamicsMode)
                    OnPropertyChanged(nameof(IsDynamicsMode));
            }
        }

        /// <summary>
        /// Активирует взаимоисключающий режим работы вкладки (переключатели-«плитки» ToggleButton).
        /// Снятие галочки с активного режима игнорируется — вкладка не остаётся без выбранного режима.
        /// </summary>
        private void SetMode(bool single, bool all, bool dynamics)
        {
            bool changed = SetProperty(ref _isSingleRateMode, single, nameof(IsSingleRateMode));
            changed |= SetProperty(ref _isAllRatesMode, all, nameof(IsAllRatesMode));
            changed |= SetProperty(ref _isDynamicsMode, dynamics, nameof(IsDynamicsMode));

            if (changed)
                NotifyModeChanged();
        }

        /// <summary>Виден ли выбор валюты (режимы «одна валюта» и «динамика»).</summary>
        public bool IsCurrencySelectorVisible => IsSingleRateMode || IsDynamicsMode;

        /// <summary>Виден ли выбор периода (режим «динамика»).</summary>
        public bool IsPeriodSelectorVisible => IsDynamicsMode;

        /// <summary>Показывать карточку курса одной валюты.</summary>
        public bool IsSingleRateResultVisible => IsSingleRateMode && _currentRate != null;

        /// <summary>Показывать таблицу курсов всех валют на дату.</summary>
        public bool IsAllRatesResultVisible => IsAllRatesMode && AllRatesOnDate.Count > 0;

        /// <summary>Показывать таблицу динамики курса за период.</summary>
        public bool IsDynamicsResultVisible => IsDynamicsMode && Dynamics.Count > 0;

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

        public Currency? SelectedCurrency
        {
            get => _selectedCurrency;
            set
            {
                if (SetProperty(ref _selectedCurrency, value))
                    OnPropertyChanged(nameof(SelectedCurrencyLabel));
            }
        }

        public string SelectedCurrencyLabel => _selectedCurrency == null
            ? "Валюта не выбрана"
            : $"{_selectedCurrency.Cur_Abbreviation} — {_selectedCurrency.Cur_Name}";

        /// <summary>Дата, на которую запрашивается курс.</summary>
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set => SetProperty(ref _selectedDate, value);
        }

        /// <summary>Начало периода для динамики курса.</summary>
        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        /// <summary>Окончание периода для динамики курса.</summary>
        public DateTime EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        // ================= Результаты =================

        public Rate? CurrentRate
        {
            get => _currentRate;
            set
            {
                if (SetProperty(ref _currentRate, value))
                {
                    OnPropertyChanged(nameof(CurrentRateCaption));
                    NotifyModeChanged();
                }
            }
        }

        public string CurrentRateCaption => _currentRate == null
            ? string.Empty
            : $"1 {_currentRate.Cur_Abbreviation} = {_currentRate.UnitRate.ToString("0.0000", CultureInfo.InvariantCulture)} BYN "
              + $"({_currentRate.Cur_Scale} ед. = {_currentRate.Cur_OfficialRate.ToString("0.0000", CultureInfo.InvariantCulture)} BYN)";

        public ObservableCollection<Currency> Currencies { get; }

        /// <summary>Валюты, отфильтрованные по строке поиска (код ISO, цифровой код или наименование).</summary>
        public ObservableCollection<Currency> FilteredCurrencies { get; }

        /// <summary>Строка фильтрации справочника валют (поле над списком валют).</summary>
        public string CurrencyFilter
        {
            get => _currencyFilter;
            set
            {
                if (SetProperty(ref _currencyFilter, value))
                    ApplyCurrencyFilter();
            }
        }

        public ObservableCollection<Rate> AllRatesOnDate { get; }

        public ObservableCollection<RateShort> Dynamics { get; }

        public ObservableCollection<ExchangeHistoryItem> HistoryItems { get; }

        /// <summary>Есть ли данные, пригодные для экспорта в текущем режиме.</summary>
        public bool HasExportData =>
            (IsSingleRateMode && _currentRate != null) ||
            (IsAllRatesMode && AllRatesOnDate.Count > 0) ||
            (IsDynamicsMode && Dynamics.Count > 0);

        // ================= Команды =================

        public RelayCommand LoadCurrenciesCommand { get; }
        public RelayCommand LoadSingleRateCommand { get; }
        public RelayCommand LoadAllRatesCommand { get; }
        public RelayCommand LoadDynamicsCommand { get; }
        public RelayCommand ClearHistoryCommand { get; }

        /// <summary>
        /// Применяет фильтр справочника валют по текущему значению <see cref="CurrencyFilter"/>.
        /// Вызывается кнопкой «Найти» и нажатием Enter в поле поиска.
        /// </summary>
        public RelayCommand ApplyCurrencyFilterCommand { get; }

        public RelayCommand ExportPdfCommand { get; }
        public RelayCommand ExportExcelCommand { get; }
        public RelayCommand ExportCsvCommand { get; }

        private void RaiseCommandStates()
        {
            LoadCurrenciesCommand.RaiseCanExecuteChanged();
            LoadSingleRateCommand.RaiseCanExecuteChanged();
            LoadAllRatesCommand.RaiseCanExecuteChanged();
            LoadDynamicsCommand.RaiseCanExecuteChanged();
            ClearHistoryCommand.RaiseCanExecuteChanged();
            ExportPdfCommand.RaiseCanExecuteChanged();
            ExportExcelCommand.RaiseCanExecuteChanged();
            ExportCsvCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Уведомляет UI об изменении режима работы и связанных с ним вычисляемых свойств.
        /// </summary>
        private void NotifyModeChanged()
        {
            OnPropertyChanged(nameof(IsCurrencySelectorVisible));
            OnPropertyChanged(nameof(IsPeriodSelectorVisible));
            OnPropertyChanged(nameof(IsSingleRateResultVisible));
            OnPropertyChanged(nameof(IsAllRatesResultVisible));
            OnPropertyChanged(nameof(IsDynamicsResultVisible));
            OnPropertyChanged(nameof(HasExportData));
            RaiseCommandStates();
        }

        // ================= Загрузка данных из API НБ РБ =================

        private async Task LoadCurrenciesAsync()
        {
            try
            {
                StatusMessage = "Загрузка справочника валют НБ РБ...";
                var currencies = await _nbrbApiService.GetCurrenciesAsync();

                // Метод /currencies НБ РБ возвращает и архивные записи: одна и та же валюта
                // (например, USD) присутствует несколько раз с разными Cur_ID. Курсы запрашиваются
                // только по актуальной записи – это запись с наибольшим Cur_ID (её же возвращают
                // методы rates?parammode=2 и rates/dynamics).
                var actualCurrencies = currencies
                    .GroupBy(c => c.Cur_Abbreviation, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(c => c.Cur_ID).First())
                    .OrderBy(c => c.Cur_Abbreviation, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                Currencies.Clear();
                foreach (var currency in actualCurrencies)
                    Currencies.Add(currency);

                var preferred = Currencies.FirstOrDefault(c =>
                    string.Equals(c.Cur_Abbreviation, DefaultCurrencyCode, StringComparison.OrdinalIgnoreCase));
                SelectedCurrency = preferred ?? Currencies.FirstOrDefault();
                ApplyCurrencyFilter();

                StatusMessage = Currencies.Count == 0
                    ? "Справочник валют пуст."
                    : $"Справочник валют загружен: {Currencies.Count} валют.";
                _logger.LogInformation(
                    "Справочник валют НБ РБ загружен: {Actual} актуальных записей из {Total} полученных",
                    Currencies.Count, currencies.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке справочника валют НБ РБ");
                StatusMessage = $"Ошибка загрузки справочника валют: {ex.Message}";
                _notifications.ShowError($"Не удалось загрузить справочник валют НБ РБ: {ex.Message}");
            }
        }

        /// <summary>
        /// Пересобирает отфильтрованный список валют по строке <see cref="CurrencyFilter"/>.
        /// Совпадение ищется по коду ISO (USD/EUR/RUB), цифровому коду и наименованию.
        /// </summary>
        private void ApplyCurrencyFilter()
        {
            string filter = _currencyFilter?.Trim() ?? string.Empty;

            FilteredCurrencies.Clear();
            foreach (var currency in Currencies)
            {
                if (filter.Length == 0 || MatchesCurrencyFilter(currency, filter))
                    FilteredCurrencies.Add(currency);
            }

            // Текущая выбранная валюта остаётся доступной, даже если не удовлетворяет фильтру.
            if (SelectedCurrency != null && !FilteredCurrencies.Contains(SelectedCurrency))
                FilteredCurrencies.Insert(0, SelectedCurrency);
        }

        private static bool MatchesCurrencyFilter(Currency currency, string filter)
        {
            return Contains(currency.Cur_Abbreviation, filter)
                || Contains(currency.Cur_Name, filter)
                || Contains(currency.Cur_Name_Eng, filter)
                || Contains(currency.Cur_Code, filter);

            static bool Contains(string? value, string search) =>
                !string.IsNullOrEmpty(value) && value.Contains(search, StringComparison.OrdinalIgnoreCase);
        }

        private async Task LoadSingleRateAsync()
        {
            if (SelectedCurrency == null)
            {
                _notifications.ShowWarning("Выберите валюту из справочника.");
                return;
            }

            if (IsLoading)
                return;

            IsLoading = true;
            try
            {
                StatusMessage = $"Запрос курса {SelectedCurrency.Cur_Abbreviation} на {SelectedDate:dd.MM.yyyy}...";
                var rate = await _nbrbApiService.GetRateAsync(SelectedCurrency.Cur_ID, SelectedDate);
                if (rate == null)
                {
                    // Идентификатор валюты мог устареть – повторяем запрос по ISO-коду (parammode=2).
                    _logger.LogInformation(
                        "Курс по Cur_ID {CurId} не найден, повторный запрос по коду {Code}",
                        SelectedCurrency.Cur_ID, SelectedCurrency.Cur_Abbreviation);
                    rate = await _nbrbApiService.GetRateByCodeAsync(SelectedCurrency.Cur_Abbreviation, SelectedDate);
                }

                CurrentRate = rate;
                if (rate == null)
                {
                    StatusMessage = $"Курс {SelectedCurrency.Cur_Abbreviation} на {SelectedDate:dd.MM.yyyy} не установлен (выходной или праздничный день).";
                    _notifications.ShowWarning(StatusMessage, "Курс не найден");
                    return;
                }

                StatusMessage = $"Курс {rate.Cur_Abbreviation} на {rate.Date:dd.MM.yyyy}: " +
                                $"{rate.Cur_OfficialRate.ToString("0.0000", CultureInfo.InvariantCulture)} BYN за {rate.Cur_Scale} ед.";
                _logger.LogInformation("Получен курс {Code} на {Date}", rate.Cur_Abbreviation, rate.Date);

                await SaveRateToHistoryAsync(rate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении курса валюты");
                StatusMessage = $"Ошибка получения курса: {ex.Message}";
                _notifications.ShowError($"Не удалось получить курс валюты: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadAllRatesAsync()
        {
            if (IsLoading)
                return;

            IsLoading = true;
            try
            {
                StatusMessage = $"Загрузка курсов всех валют на {SelectedDate:dd.MM.yyyy}...";
                var rates = await _nbrbApiService.GetAllRatesAsync(SelectedDate);

                AllRatesOnDate.Clear();
                foreach (var rate in rates.OrderBy(r => r.Cur_Abbreviation, StringComparer.OrdinalIgnoreCase))
                    AllRatesOnDate.Add(rate);

                NotifyModeChanged();

                if (AllRatesOnDate.Count == 0)
                {
                    StatusMessage = $"Курсы валют на {SelectedDate:dd.MM.yyyy} не найдены (выходной или праздничный день).";
                    _notifications.ShowWarning(StatusMessage, "Нет данных");
                    return;
                }

                StatusMessage = $"Получены курсы {AllRatesOnDate.Count} валют на {SelectedDate:dd.MM.yyyy}.";
                _logger.LogInformation("Получены курсы валют на {Date}: {Count}", SelectedDate, AllRatesOnDate.Count);

                foreach (var rate in AllRatesOnDate)
                    await SaveRateToHistoryAsync(rate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении курсов всех валют");
                StatusMessage = $"Ошибка получения курсов: {ex.Message}";
                _notifications.ShowError($"Не удалось получить курсы валют: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadDynamicsAsync()
        {
            if (SelectedCurrency == null)
            {
                _notifications.ShowWarning("Выберите валюту из справочника.");
                return;
            }

            if (StartDate > EndDate)
            {
                _notifications.ShowWarning("Начальная дата периода позже конечной.");
                return;
            }

            if (IsLoading)
                return;

            IsLoading = true;
            try
            {
                StatusMessage = $"Загрузка динамики курса {SelectedCurrency.Cur_Abbreviation} " +
                                $"с {StartDate:dd.MM.yyyy} по {EndDate:dd.MM.yyyy}...";

                var dynamics = await _nbrbApiService.GetRateDynamicsAsync(SelectedCurrency.Cur_ID, StartDate, EndDate);

                Dynamics.Clear();
                foreach (var point in dynamics.OrderBy(p => p.Date))
                    Dynamics.Add(point);

                NotifyModeChanged();

                if (Dynamics.Count == 0)
                {
                    StatusMessage = $"Динамика курса {SelectedCurrency.Cur_Abbreviation} за указанный период не найдена.";
                    _notifications.ShowWarning(StatusMessage, "Нет данных");
                    return;
                }

                StatusMessage = $"Получено точек динамики: {Dynamics.Count} " +
                                $"(мин. {Dynamics.Min(p => p.Cur_OfficialRate).ToString("0.0000", CultureInfo.InvariantCulture)}, " +
                                $"макс. {Dynamics.Max(p => p.Cur_OfficialRate).ToString("0.0000", CultureInfo.InvariantCulture)}).";
                _logger.LogInformation("Получена динамика курса {Code}: {Count} точек",
                    SelectedCurrency.Cur_Abbreviation, Dynamics.Count);

                var currency = SelectedCurrency;
                foreach (var point in Dynamics)
                {
                    await AddHistoryItemAsync(new ExchangeHistoryItem
                    {
                        CurrencyCode = currency.Cur_Abbreviation,
                        CurrencyName = currency.Cur_Name,
                        Date = point.Date,
                        Rate = point.Cur_OfficialRate,
                        Scale = currency.Cur_Scale,
                        Timestamp = DateTimeHelper.NowBelarus
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении динамики курса");
                StatusMessage = $"Ошибка получения динамики: {ex.Message}";
                _notifications.ShowError($"Не удалось получить динамику курса: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ================= История запросов =================

        private async Task LoadHistoryAsync()
        {
            try
            {
                var items = await _exchangeHistoryRepository.GetAllAsync();

                HistoryItems.Clear();
                foreach (var item in items.Take(500))
                    HistoryItems.Add(item);

                RaiseCommandStates();
                _logger.LogInformation("История курсов валют загружена: {Count} записей", HistoryItems.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке истории курсов валют");
                _notifications.ShowError($"Не удалось загрузить историю курсов: {ex.Message}");
            }
        }

        private async Task ClearHistoryAsync()
        {
            if (MessageBox.Show("Очистить историю запросов курсов валют?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                await _exchangeHistoryRepository.ClearAsync();
                HistoryItems.Clear();
                RaiseCommandStates();
                StatusMessage = "История курсов очищена.";
                _notifications.ShowSuccess("История запросов курсов очищена.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при очистке истории курсов валют");
                _notifications.ShowError($"Не удалось очистить историю курсов: {ex.Message}");
            }
        }

        private async Task SaveRateToHistoryAsync(Rate rate)
        {
            await AddHistoryItemAsync(new ExchangeHistoryItem
            {
                CurrencyCode = rate.Cur_Abbreviation,
                CurrencyName = rate.Cur_Name,
                Date = rate.Date,
                Rate = rate.Cur_OfficialRate,
                Scale = rate.Cur_Scale,
                Timestamp = DateTimeHelper.NowBelarus
            });
        }

        private async Task AddHistoryItemAsync(ExchangeHistoryItem item)
        {
            try
            {
                await _exchangeHistoryRepository.AddAsync(item);
                HistoryItems.Insert(0, item);
                RaiseCommandStates();
            }
            catch (Exception ex)
            {
                // Ошибка журналирования не должна прерывать основной сценарий загрузки курсов.
                _logger.LogError(ex, "Ошибка при сохранении записи в историю курсов");
            }
        }

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
                FileName = BuildFileName("Курсы_валют"),
                DefaultExt = "pdf"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                StatusMessage = "Экспорт в PDF...";
                _logger.LogInformation("Начало экспорта курсов валют в PDF: {Path}", saveDialog.FileName);

                if (IsSingleRateMode && _currentRate != null)
                {
                    await _pdfExportService.ExportExchangeRateAsync(_currentRate, saveDialog.FileName);
                }
                else if (IsAllRatesMode)
                {
                    await _pdfExportService.ExportAllRatesAsync(AllRatesOnDate, SelectedDate, saveDialog.FileName);
                }
                else if (IsDynamicsMode && SelectedCurrency != null)
                {
                    await _pdfExportService.ExportDynamicsAsync(
                        Dynamics,
                        $"{SelectedCurrency.Cur_Abbreviation} — {SelectedCurrency.Cur_Name}",
                        StartDate,
                        EndDate,
                        saveDialog.FileName);
                }

                StatusMessage = "Экспорт в PDF завершен.";
                _notifications.ShowSuccess($"Файл PDF успешно сохранен!\n{saveDialog.FileName}");
                _logger.LogInformation("Экспорт в PDF завершен: {Path}", saveDialog.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте курсов валют в PDF");
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
                FileName = BuildFileName("Курсы_валют"),
                DefaultExt = "xlsx"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                StatusMessage = "Экспорт в Excel...";
                _logger.LogInformation("Начало экспорта курсов валют в Excel: {Path}", saveDialog.FileName);

                var (title, headers, rows) = BuildCurrentTable();

                OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal(ExcelLicenseHolder);

                using var package = new OfficeOpenXml.ExcelPackage();
                var sheet = package.Workbook.Worksheets.Add("Курсы валют");

                sheet.Cells[1, 1].Value = title;
                sheet.Cells[1, 1, 1, headers.Length].Merge = true;
                sheet.Cells[1, 1].Style.Font.Bold = true;

                for (int c = 0; c < headers.Length; c++)
                    sheet.Cells[2, c + 1].Value = headers[c];

                for (int r = 0; r < rows.Count; r++)
                {
                    for (int c = 0; c < rows[r].Length && c < headers.Length; c++)
                        sheet.Cells[r + 3, c + 1].Value = rows[r][c];
                }

                sheet.Cells[2, 1, 2, headers.Length].Style.Font.Bold = true;
                sheet.Cells[2, 1, Math.Max(2, rows.Count + 2), headers.Length].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                sheet.Cells.AutoFitColumns();

                await Task.Run(() => package.SaveAs(new System.IO.FileInfo(saveDialog.FileName)));

                StatusMessage = "Экспорт в Excel завершен.";
                _notifications.ShowSuccess($"Файл Excel успешно сохранен!\n{saveDialog.FileName}");
                _logger.LogInformation("Экспорт в Excel завершен: {Path}", saveDialog.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте курсов валют в Excel");
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
                FileName = BuildFileName("Курсы_валют"),
                DefaultExt = "csv"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                StatusMessage = "Экспорт в CSV...";
                var (title, headers, rows) = BuildCurrentTable();

                var sb = new StringBuilder();
                sb.AppendLine(CsvEscape(title));
                sb.AppendLine(string.Join(";", headers.Select(CsvEscape)));
                foreach (var row in rows)
                    sb.AppendLine(string.Join(";", row.Select(CsvEscape)));

                await File.WriteAllTextAsync(saveDialog.FileName, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

                StatusMessage = "Экспорт в CSV завершен.";
                _notifications.ShowSuccess($"Файл CSV успешно сохранен!\n{saveDialog.FileName}");
                _logger.LogInformation("Экспорт в CSV завершен: {Path}", saveDialog.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте курсов валют в CSV");
                _notifications.ShowError($"Ошибка при экспорте в CSV: {ex.Message}");
            }
        }

        // ================= Вспомогательные методы =================

        /// <summary>
        /// Формирует заголовок, шапку и строки таблицы для текущего режима работы.
        /// </summary>
        private (string Title, string[] Headers, List<string[]> Rows) BuildCurrentTable()
        {
            if (IsSingleRateMode && _currentRate != null)
            {
                var rate = _currentRate;
                return ("Официальный курс валюты НБ РБ",
                    new[] { "Показатель", "Значение" },
                    new List<string[]>
                    {
                        new[] { "Код валюты", rate.Cur_Abbreviation },
                        new[] { "Наименование", rate.Cur_Name },
                        new[] { "Дата", rate.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) },
                        new[] { "Масштаб (ед.)", rate.Cur_Scale.ToString(CultureInfo.InvariantCulture) },
                        new[] { "Официальный курс, BYN", rate.Cur_OfficialRate.ToString("0.0000", CultureInfo.InvariantCulture) },
                        new[] { "Курс за 1 ед., BYN", rate.UnitRate.ToString("0.0000", CultureInfo.InvariantCulture) }
                    });
            }

            if (IsAllRatesMode)
            {
                var rows = AllRatesOnDate
                    .Select(r => new[]
                    {
                        r.Cur_Abbreviation,
                        r.Cur_Name,
                        r.Cur_Scale.ToString(CultureInfo.InvariantCulture),
                        r.Cur_OfficialRate.ToString("0.0000", CultureInfo.InvariantCulture)
                    })
                    .ToList();

                return ($"Курсы валют НБ РБ на {SelectedDate:dd.MM.yyyy}",
                    new[] { "Код", "Наименование", "Масштаб (ед.)", "Курс, BYN" },
                    rows);
            }

            var dynamicsRows = Dynamics
                .Select(p => new[]
                {
                    p.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    p.Cur_OfficialRate.ToString("0.0000", CultureInfo.InvariantCulture)
                })
                .ToList();

            string code = SelectedCurrency?.Cur_Abbreviation ?? string.Empty;
            return ($"Динамика курса {code} (НБ РБ) с {StartDate:dd.MM.yyyy} по {EndDate:dd.MM.yyyy}",
                new[] { "Дата", "Курс, BYN" },
                dynamicsRows);
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
