using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NalogParserWPF.Helpers;
using NalogParserWPF.Models;
using NalogParserWPF.Services;

namespace NalogParserWPF.ViewModels
{
    /// <summary>
    /// ViewModel окна «📊 Интерпретация статистических данных» (вкладка «Госстатистика»).
    /// Разбирает уже загруженный ряд показателя Белстата для подготовки представлений
    /// об устранении причин и условий, способствовавших совершению преступлений.
    /// Исходная таблица результата не изменяется: сервис читает её и возвращает разбор.
    /// </summary>
    public class BelstatInterpretationViewModel : ViewModelBase
    {
        /// <summary>Владелец некоммерческой лицензии EPPlus (как в остальных экспортерах проекта).</summary>
        private const string ExcelLicenseHolder = "NalogParserWPF";

        /// <summary>Сообщение на время фонового расчёта (прогресс-индикатор неопределённый).</summary>
        private const string AnalysisMessage = "Анализ данных...";

        private const string ExcelSheetSimple = "Простая интерпретация";
        private const string ExcelSheetAdvanced = "Расширенная интерпретация";
        private const string ExportBaseName = "Интерпретация_Госстатистика";

        private const string ReadyMessage = "Интерпретация выполнена. Используйте вкладки для просмотра результатов.";
        private const string NoDataMessage = "Нет данных для интерпретации: сначала получите значения на вкладке «Госстатистика».";
        private const string EmptySeriesMessage =
            "В загруженной таблице не найдено числовых значений по годам. Проверьте состав колонок результата.";

        private readonly IBelstatInterpretationService _interpretationService;
        private readonly IPdfExportService _pdfExportService;
        private readonly INotificationService _notifications;
        private readonly ILogger<BelstatInterpretationViewModel> _logger;

        private DataTable? _sourceData;
        private InterpretationResult? _simpleResult;
        private InterpretationResult? _advancedResult;
        private string _indicatorName = string.Empty;
        private bool _isLoading;
        private string _statusMessage = NoDataMessage;
        private int _recommendationsLimit = 50;

        public BelstatInterpretationViewModel(
            IBelstatInterpretationService interpretationService,
            IPdfExportService pdfExportService,
            INotificationService notifications,
            ILogger<BelstatInterpretationViewModel> logger)
        {
            _interpretationService = interpretationService;
            _pdfExportService = pdfExportService;
            _notifications = notifications;
            _logger = logger;

            ExportPdfCommand = new RelayCommand(_ => _ = ExportPdfAsync(), _ => HasData && !IsLoading);
            ExportExcelCommand = new RelayCommand(_ => _ = ExportExcelAsync(), _ => HasData && !IsLoading);
            RefreshCommand = new RelayCommand(_ => _ = RunInterpretationAsync(), _ => _sourceData != null && !IsLoading);
        }

        // ================= Состояние =================

        /// <summary>Выполняется разбор данных (блокирует кнопки экспорта).</summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (SetProperty(ref _isLoading, value))
                    RaiseCommandStates();
            }
        }

        /// <summary>Строка состояния окна.</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// Максимальное число рекомендаций, показываемых в окне (по умолчанию 50).
        /// При избытке добавляется пояснение «Показаны первые N из M».
        /// </summary>
        public int RecommendationsLimit
        {
            get => _recommendationsLimit;
            set => SetProperty(ref _recommendationsLimit, value < 1 ? 1 : value);
        }

        /// <summary>Есть ли готовый разбор (хотя бы одна точка ряда).</summary>
        public bool HasData { get; private set; }

        /// <summary>Показывать подсказку вместо таблиц (данных нет).</summary>
        public bool IsEmpty => !HasData;

        /// <summary>Название показателя государственной статистики.</summary>
        public string IndicatorName
        {
            get => _indicatorName;
            private set => SetProperty(ref _indicatorName, value);
        }

        /// <summary>Подпись окна: показатель, период и источник данных.</summary>
        public string SourceInfo { get; private set; } = string.Empty;

        /// <summary>Период ряда в читаемом виде.</summary>
        public string PeriodText { get; private set; } = "Период не определён.";

        /// <summary>Число лет в ряду.</summary>
        public string PointsCountText { get; private set; } = "Наблюдений: 0.";

        /// <summary>Подсказка вместо пустых таблиц.</summary>
        public string EmptyMessage { get; private set; } = NoDataMessage;

        // ================= Простая интерпретация =================

        public ObservableCollection<InterpretationPoint> SimplePoints { get; } = new();

        public ObservableCollection<InterpretationNote> SimpleNotes { get; } = new();

        public ObservableCollection<InterpretationNote> SimpleRecommendations { get; } = new();

        /// <summary>Среднее значение за период (простая интерпретация).</summary>
        public string SimpleAverageText { get; private set; } = "—";

        /// <summary>Стандартное отклонение за период (простая интерпретация).</summary>
        public string SimpleDeviationText { get; private set; } = "—";

        /// <summary>Вывод простой интерпретации в одну строку.</summary>
        public string SimpleConclusionText { get; private set; } = string.Empty;

        // ================= Расширенная интерпретация =================

        public ObservableCollection<InterpretationPoint> AdvancedPoints { get; } = new();

        public ObservableCollection<InterpretationNote> AdvancedNotes { get; } = new();

        public ObservableCollection<InterpretationNote> AdvancedRecommendations { get; } = new();

        /// <summary>Среднее значение за период (расширенная интерпретация).</summary>
        public string AdvancedAverageText { get; private set; } = "—";

        /// <summary>Стандартное отклонение (σ) за период.</summary>
        public string AdvancedDeviationText { get; private set; } = "—";

        /// <summary>Описание тренда ряда.</summary>
        public string AdvancedTrendText { get; private set; } = "—";

        /// <summary>Краткое резюме по аномалиям.</summary>
        public string AdvancedAnomalyText { get; private set; } = "—";

        /// <summary>Количество скачков «год к году» (более 50 %).</summary>
        public string AdvancedJumpText { get; private set; } = "—";

        /// <summary>Описание устойчивого изменения ряда.</summary>
        public string AdvancedSustainedText { get; private set; } = "—";

        // ================= Команды =================

        public RelayCommand ExportPdfCommand { get; }
        public RelayCommand ExportExcelCommand { get; }

        /// <summary>Повторный разбор текущей таблицы (после обновления данных на вкладке).</summary>
        public RelayCommand RefreshCommand { get; }

        private void RaiseCommandStates()
        {
            ExportPdfCommand.RaiseCanExecuteChanged();
            ExportExcelCommand.RaiseCanExecuteChanged();
            RefreshCommand.RaiseCanExecuteChanged();
        }

        // ================= Загрузка данных и разбор =================

        /// <summary>
        /// Принимает таблицу результата вкладки «Госстатистика» и сразу выполняет разбор.
        /// Вызывается владельцем окна после получения окна из контейнера DI.
        /// </summary>
        /// <param name="data">Таблица результата (не изменяется).</param>
        /// <param name="indicatorName">Название показателя.</param>
        /// <param name="subtitle">Подпись источника/периода из вкладки «Госстатистика».</param>
        public async Task LoadDataAsync(DataTable? data, string indicatorName, string subtitle = "")
        {
            _sourceData = data;
            IndicatorName = string.IsNullOrWhiteSpace(indicatorName)
                ? "Показатель государственной статистики"
                : indicatorName.Trim();
            SourceInfo = string.IsNullOrWhiteSpace(subtitle) ? SourceInfo : subtitle.Trim();

            await RunInterpretationAsync();
        }

        /// <summary>
        /// Выполняет простой и расширенный разбор ряда и наполняет коллекции окна.
        /// Ошибки не пробрасываются в UI: пользователь получает подсказку и запись в журнале.
        /// </summary>
        public async Task RunInterpretationAsync()
        {
            if (IsLoading)
                return;

            IsLoading = true;
            StatusMessage = AnalysisMessage;
            try
            {
                DataTable? source = _sourceData;
                if (source == null || source.Rows.Count == 0)
                {
                    ResetResults(NoDataMessage);
                    return;
                }

                string indicator = IndicatorName;

                // Тяжёлые вычисления — в фоне: интерфейс не блокируется на 50 000+ строках.
                var simple = await _interpretationService
                    .AnalyzeSimpleAsync(source, indicator, CancellationToken.None)
                    .ConfigureAwait(true);
                var advanced = await _interpretationService
                    .AnalyzeAdvancedAsync(source, indicator, CancellationToken.None)
                    .ConfigureAwait(true);

                // Обновление коллекций — через диспетчер UI-потока.
                OnUi(() =>
                {
                    FillSimple(simple);
                    FillAdvanced(advanced);

                    HasData = advanced.HasData || simple.HasData;
                    EmptyMessage = HasData ? string.Empty : EmptySeriesMessage;
                });

                if (advanced.Years.Count > 0)
                {
                    PeriodText = $"Период: {advanced.Years[0]}–{advanced.Years[^1]} гг.";
                    PointsCountText = $"Наблюдений: {advanced.Years.Count}.";
                }
                else
                {
                    PeriodText = "Период не определён.";
                    PointsCountText = "Наблюдений: 0.";
                }

                StatusMessage = HasData ? ReadyMessage : EmptySeriesMessage;
                _logger.LogInformation(
                    "Интерпретация «{Indicator}»: точек — {Points}, скачков — {Jumps}, аномалий — {Anomalies}",
                    IndicatorName, advanced.Points.Count, advanced.JumpCount,
                    advanced.Points.Count(p => !string.IsNullOrEmpty(p.AnomalyMark)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка интерпретации статистических данных");
                ResetResults($"Ошибка интерпретации: {ex.Message}");
                _notifications.ShowError($"Ошибка интерпретации данных: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                RaiseCommandStates();
                NotifyResultChanged();
            }
        }

        /// <summary>Выполняет действие в UI-потоке (после фонового расчёта).</summary>
        private static void OnUi(Action action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
                dispatcher.Invoke(action);
            else
                action();
        }

        /// <summary>
        /// Переносит рекомендации в коллекцию окна с ограничением по количеству:
        /// показываются первые <see cref="RecommendationsLimit"/>, при избытке добавляется пояснение.
        /// </summary>
        private void FillRecommendations(
            ObservableCollection<InterpretationNote> target,
            IReadOnlyList<InterpretationNote> recommendations)
        {
            target.Clear();

            int limit = _recommendationsLimit < 1 ? 1 : _recommendationsLimit;
            foreach (var note in recommendations.Take(limit))
                target.Add(note);

            if (recommendations.Count > limit)
            {
                target.Add(new InterpretationNote
                {
                    Type = "Ограничение",
                    Text = string.Format(
                        "Показаны первые {0} из {1}. Уточните фильтр для детального анализа.",
                        limit, recommendations.Count),
                    Severity = InterpretationSeverity.Info
                });
            }
        }

        private void FillSimple(InterpretationResult result)
        {
            _simpleResult = result;

            SimplePoints.Clear();
            SimpleNotes.Clear();

            foreach (var point in result.Points) SimplePoints.Add(point);
            foreach (var note in result.Notes) SimpleNotes.Add(note);
            FillRecommendations(SimpleRecommendations, result.Recommendations);

            SimpleAverageText = Format(result.Average);
            SimpleDeviationText = Format(result.Deviation);
            SimpleConclusionText = BuildSimpleConclusion(result);
        }

        private void FillAdvanced(InterpretationResult result)
        {
            _advancedResult = result;

            AdvancedPoints.Clear();
            AdvancedNotes.Clear();

            foreach (var point in result.Points) AdvancedPoints.Add(point);
            foreach (var note in result.Notes) AdvancedNotes.Add(note);
            FillRecommendations(AdvancedRecommendations, result.Recommendations);

            AdvancedAverageText = Format(result.Average);
            AdvancedDeviationText = Format(result.Deviation);
            AdvancedTrendText = string.IsNullOrWhiteSpace(result.Trend) ? "—" : result.Trend;
            AdvancedAnomalyText = string.IsNullOrWhiteSpace(result.Anomaly) ? "—" : result.Anomaly;
            AdvancedJumpText = $"{result.JumpCount} (изменение «год к году» более 50 %)";
            AdvancedSustainedText = string.IsNullOrWhiteSpace(result.SustainedTrend) ? "—" : result.SustainedTrend;
        }

        /// <summary>
        /// Вывод простой интерпретации: сколько лет выше/ниже среднего более чем на 10 %
        /// и есть ли аномалии (отклонение более 30 %).
        /// </summary>
        private static string BuildSimpleConclusion(InterpretationResult result)
        {
            if (result.Points.Count == 0)
                return "Недостаточно данных для вывода.";

            int above = result.Points.Count(p => p.DeviationPercent > 10.0);
            int below = result.Points.Count(p => p.DeviationPercent < -10.0);
            int anomalies = result.Points.Count(p => !string.IsNullOrEmpty(p.AnomalyMark));

            var sb = new StringBuilder();
            sb.Append($"Среднее значение за период — {Format(result.Average)}, стандартное отклонение — {Format(result.Deviation)}. ");
            sb.Append($"Выше среднего более чем на 10 %: {above} лет; ниже более чем на 10 %: {below} лет. ");

            sb.Append(anomalies == 0
                ? "Аномальных отклонений (более 30 %) не выявлено: ряд однородный."
                : $"Выявлено аномальных отклонений (более 30 %): {anomalies}. Такие годы требуют проверки первичных данных.");

            return sb.ToString();
        }

        /// <summary>Очищает результаты разбора и показывает подсказку вместо таблиц.</summary>
        private void ResetResults(string message)
        {
            SimplePoints.Clear();
            SimpleNotes.Clear();
            SimpleRecommendations.Clear();
            AdvancedPoints.Clear();
            AdvancedNotes.Clear();
            AdvancedRecommendations.Clear();

            HasData = false;
            _simpleResult = null;
            _advancedResult = null;
            SimpleAverageText = "—";
            SimpleDeviationText = "—";
            SimpleConclusionText = string.Empty;
            AdvancedAverageText = "—";
            AdvancedDeviationText = "—";
            AdvancedTrendText = "—";
            AdvancedAnomalyText = "—";
            AdvancedJumpText = "—";
            AdvancedSustainedText = "—";
            PeriodText = "Период не определён.";
            PointsCountText = "Наблюдений: 0.";
            EmptyMessage = message;
            StatusMessage = message;
        }

        /// <summary>Оповещает UI об изменении вычисляемых свойств результата.</summary>
        private void NotifyResultChanged()
        {
            OnPropertyChanged(nameof(HasData));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(PeriodText));
            OnPropertyChanged(nameof(PointsCountText));
            OnPropertyChanged(nameof(EmptyMessage));
            OnPropertyChanged(nameof(SimpleAverageText));
            OnPropertyChanged(nameof(SimpleDeviationText));
            OnPropertyChanged(nameof(SimpleConclusionText));
            OnPropertyChanged(nameof(AdvancedAverageText));
            OnPropertyChanged(nameof(AdvancedDeviationText));
            OnPropertyChanged(nameof(AdvancedTrendText));
            OnPropertyChanged(nameof(AdvancedAnomalyText));
            OnPropertyChanged(nameof(AdvancedJumpText));
            OnPropertyChanged(nameof(AdvancedSustainedText));
        }

        // ================= Экспорт =================

        /// <summary>Экспорт интерпретации в PDF (сводка, разбор по годам, примечания, рекомендации).</summary>
        private async Task ExportPdfAsync()
        {
            if (!HasData || _advancedResult == null)
            {
                _notifications.ShowInfo("Нет данных для экспорта.");
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF файлы (*.pdf)|*.pdf",
                FileName = BuildFileName(),
                DefaultExt = "pdf"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                StatusMessage = "Экспорт интерпретации в PDF...";
                _logger.LogInformation("Начало экспорта интерпретации в PDF: {Path}", saveDialog.FileName);

                await _pdfExportService.ExportInterpretationAsync(_advancedResult, saveDialog.FileName);

                StatusMessage = "Экспорт в PDF завершен.";
                _notifications.ShowSuccess($"Файл PDF успешно сохранен!\n{saveDialog.FileName}");
                _logger.LogInformation("Экспорт интерпретации в PDF завершен: {Path}", saveDialog.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте интерпретации в PDF");
                _notifications.ShowError($"Ошибка при экспорте в PDF: {ex.Message}");
            }
        }

        /// <summary>
        /// Экспорт интерпретации в Excel: два листа — простая и расширенная интерпретация
        /// (сводка, разбор по годам, примечания и рекомендации).
        /// </summary>
        private async Task ExportExcelAsync()
        {
            if (!HasData || _simpleResult == null || _advancedResult == null)
            {
                _notifications.ShowInfo("Нет данных для экспорта.");
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                FileName = BuildFileName(),
                DefaultExt = "xlsx"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                StatusMessage = "Экспорт интерпретации в Excel...";
                _logger.LogInformation("Начало экспорта интерпретации в Excel: {Path}", saveDialog.FileName);

                OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal(ExcelLicenseHolder);

                using var package = new OfficeOpenXml.ExcelPackage();
                BuildWorksheet(package.Workbook.Worksheets.Add(ExcelSheetSimple), _simpleResult, advanced: false);
                BuildWorksheet(package.Workbook.Worksheets.Add(ExcelSheetAdvanced), _advancedResult, advanced: true);

                await Task.Run(() => package.SaveAs(new FileInfo(saveDialog.FileName)));

                StatusMessage = "Экспорт в Excel завершен.";
                _notifications.ShowSuccess($"Файл Excel успешно сохранен!\n{saveDialog.FileName}");
                _logger.LogInformation("Экспорт интерпретации в Excel завершен: {Path}", saveDialog.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте интерпретации в Excel");
                _notifications.ShowError($"Ошибка при экспорте в Excel: {ex.Message}");
            }
        }

        /// <summary>
        /// Заполняет лист Excel: заголовок, сводка, таблица по годам, примечания и рекомендации.
        /// Числовые колонки записываются числами, текстовые — строками (как в остальных экспортёрах проекта).
        /// </summary>
        private static void BuildWorksheet(OfficeOpenXml.ExcelWorksheet sheet, InterpretationResult result, bool advanced)
        {
            string[] headers = advanced
                ? new[] { "Год", "Значение", "Отклонение от среднего, %", "Сигма, σ", "Тренд", "Отметка" }
                : new[] { "Год", "Значение", "Отклонение от среднего, %", "Отметка" };

            int columns = headers.Length;
            int row = 1;

            sheet.Cells[row, 1].Value = advanced
                ? "Расширенная интерпретация статистических данных"
                : "Простая интерпретация статистических данных";
            sheet.Cells[row, 1, row, columns].Merge = true;
            sheet.Cells[row, 1].Style.Font.Bold = true;
            sheet.Cells[row, 1].Style.Font.Size = 14;
            row += 2;

            sheet.Cells[row, 1].Value = $"Показатель: {result.IndicatorName}";
            sheet.Cells[row, 1, row, columns].Merge = true;
            sheet.Cells[row, 1].Style.Font.Bold = true;
            row++;

            if (result.Years.Count > 0)
            {
                sheet.Cells[row, 1].Value =
                    $"Период: {result.Years[0]}–{result.Years[^1]} гг. Наблюдений: {result.Years.Count}.";
                sheet.Cells[row, 1, row, columns].Merge = true;
                row++;
            }

            row++;
            sheet.Cells[row, 1].Value = "Сводные показатели";
            sheet.Cells[row, 1, row, columns].Merge = true;
            sheet.Cells[row, 1].Style.Font.Bold = true;
            row++;

            row = WriteSummaryRow(sheet, row, "Среднее значение за период", result.AverageDisplay, columns);
            row = WriteSummaryRow(sheet, row, "Стандартное отклонение (σ)", result.DeviationDisplay, columns);

            if (advanced)
            {
                row = WriteSummaryRow(sheet, row, "Тренд", result.Trend, columns);
                row = WriteSummaryRow(sheet, row, "Аномалии (более 2σ)", result.Anomaly, columns);
                row = WriteSummaryRow(sheet, row, "Скачки «год к году» (более 50 %)",
                    result.JumpCount.ToString(CultureInfo.InvariantCulture), columns);
                row = WriteSummaryRow(sheet, row, "Устойчивое изменение", result.SustainedTrend, columns);
            }
            else
            {
                row = WriteSummaryRow(sheet, row, "Вывод", BuildSimpleConclusion(result), columns);
            }

            row++;
            int headerRow = row;
            for (int c = 0; c < headers.Length; c++)
                sheet.Cells[headerRow, c + 1].Value = headers[c];

            sheet.Cells[headerRow, 1, headerRow, headers.Length].Style.Font.Bold = true;
            row++;

            foreach (var point in result.Points)
            {
                sheet.Cells[row, 1].Value = point.Year;
                sheet.Cells[row, 2].Value = point.Value;
                sheet.Cells[row, 3].Value = Math.Round(point.DeviationPercent, 1);

                if (advanced)
                {
                    sheet.Cells[row, 4].Value = Math.Round(point.Sigma, 2);
                    sheet.Cells[row, 5].Value = point.Trend;
                    sheet.Cells[row, 6].Value = InterpretationHelper.GetPointNote(point);
                }
                else
                {
                    sheet.Cells[row, 4].Value = InterpretationHelper.GetPointNote(point);
                }

                sheet.Cells[row, 1, row, headers.Length].Style.Font.Color
                    .SetColor(System.Drawing.ColorTranslator.FromHtml(InterpretationHelper.GetHexColor(point.Severity)));
                row++;
            }

            sheet.Cells[headerRow, 1, Math.Max(headerRow, row - 1), headers.Length].Style.Border
                .BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);

            if (result.Notes.Count > 0)
            {
                row++;
                row = WriteNoteBlock(sheet, row, "Статистический анализ", result.Notes, columns);
            }

            if (result.Recommendations.Count > 0)
            {
                row++;
                WriteNoteBlock(sheet, row, "Рекомендации для следственной работы", result.Recommendations, columns);
            }

            ApplyColumnWidths(sheet, advanced);
        }

        /// <summary>Записывает строку сводки: подпись в первой колонке, значение — объединёнными колонками.</summary>
        private static int WriteSummaryRow(OfficeOpenXml.ExcelWorksheet sheet, int row, string label, string value, int columns)
        {
            sheet.Cells[row, 1].Value = label;
            sheet.Cells[row, 1].Style.Font.Bold = true;

            if (columns > 1)
            {
                sheet.Cells[row, 2, row, columns].Merge = true;
                sheet.Cells[row, 2].Value = value;
            }

            return row + 1;
        }

        /// <summary>Записывает блок записей «подпись: текст», окрашивая текст по важности записи.</summary>
        private static int WriteNoteBlock(
            OfficeOpenXml.ExcelWorksheet sheet, int row, string title,
            IReadOnlyList<InterpretationNote> notes, int columns)
        {
            sheet.Cells[row, 1].Value = title;
            if (columns > 1) sheet.Cells[row, 1, row, columns].Merge = true;
            sheet.Cells[row, 1].Style.Font.Bold = true;
            row++;

            foreach (var note in notes)
            {
                sheet.Cells[row, 1].Value = string.IsNullOrWhiteSpace(note.Type)
                    ? note.Text
                    : $"{note.Type}: {note.Text}";
                sheet.Cells[row, 1].Style.Font.Color
                    .SetColor(System.Drawing.ColorTranslator.FromHtml(InterpretationHelper.GetHexColor(note.Severity)));
                row++;
            }

            return row;
        }

        /// <summary>Устанавливает ширину колонок листа (лист без обтекания текста).</summary>
        private static void ApplyColumnWidths(OfficeOpenXml.ExcelWorksheet sheet, bool advanced)
        {
            sheet.Column(1).Width = 12;
            sheet.Column(2).Width = 14;
            sheet.Column(3).Width = 26;

            if (advanced)
            {
                sheet.Column(4).Width = 12;
                sheet.Column(5).Width = 8;
                sheet.Column(6).Width = 52;
            }
            else
            {
                sheet.Column(4).Width = 52;
            }
        }

        /// <summary>Имя файла экспорта: «Интерпретация_Госстатистика_дата_время» (как в других экспортёрах).</summary>
        private static string BuildFileName()
        {
            string name = $"{ExportBaseName}_{DateTimeHelper.NowBelarus:yyyy-MM-dd_HH-mm}";
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');

            return name;
        }

        private static string Format(double value) =>
            value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
