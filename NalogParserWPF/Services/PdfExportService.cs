using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using NalogParserWPF.Helpers;
using NalogParserWPF.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NalogParserWPF.Services
{
    public class PdfExportService : IPdfExportService
    {
        private const string HeaderText = "Следственный комитет Республики Беларусь";
        private const string HeaderSubText = "Проверка контрагентов по данным grp.nalog.gov.by";
        private const string CurrencyHeaderSubText = "Информация о курсах валют по данным НБ РБ";
        private const string BelstatHeaderSubText = "Государственная статистика по данным Национального статистического комитета";
        private const string BelstatDataSource = "dataportal.belstat.gov.by";
        private const string NotSpecified = "Не указано";
        private const string DefaultFont = "Lato";
        private const float BodyFontSize = 11;
        private const float MaxFieldWidth = 450;

        private static byte[]? _logoBytes;

        private static byte[]? LogoBytes => _logoBytes ??= LoadLogo();

        private readonly ILogger<PdfExportService> _logger;

        private static byte[]? LoadLogo()
        {
            try
            {
                using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/sk-logo-2.png"))?.Stream;
                if (stream != null)
                {
                    using var memory = new MemoryStream();
                    stream.CopyTo(memory);
                    return memory.ToArray();
                }

                var directory = new DirectoryInfo(AppContext.BaseDirectory);
                for (int i = 0; i < 6 && directory != null; i++)
                {
                    var candidate = Path.Combine(directory.FullName, "sk-logo-2.png");
                    if (File.Exists(candidate))
                        return File.ReadAllBytes(candidate);
                    directory = directory.Parent;
                }
            }
            catch
            {
            }

            return null;
        }

        static PdfExportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public PdfExportService(ILogger<PdfExportService> logger)
        {
            _logger = logger;
        }

        public async Task ExportCompanyInfoAsync(CompanyRow company, string filePath, CancellationToken cancellationToken = default)
        {
            if (company == null)
                throw new ArgumentNullException(nameof(company));

            _logger.LogInformation("Начало экспорта информации о компании {Unp} в PDF", company.Unp);

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Document.Create(container => BuildCompanyPage(container, company, null))
                        .GeneratePdf(filePath);
                }, cancellationToken);

                _logger.LogInformation("Экспорт информации о компании {Unp} завершен", company.Unp);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте информации о компании {Unp} в PDF", company.Unp);
                throw;
            }
        }

        public async Task ExportHistoryEntryAsync(SearchHistory entry, string filePath, CancellationToken cancellationToken = default)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            _logger.LogInformation("Начало экспорта записи истории #{Id} в PDF", entry.Id);

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var company = TryParseCompany(entry.ResultData);
                    Document.Create(container => BuildCompanyPage(container, company, entry))
                        .GeneratePdf(filePath);
                }, cancellationToken);

                _logger.LogInformation("Экспорт записи истории #{Id} завершен", entry.Id);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте записи истории #{Id} в PDF", entry.Id);
                throw;
            }
        }

        public async Task ExportBatchResultsAsync(IEnumerable<BatchResultItem> results, string filePath, CancellationToken cancellationToken = default)
        {
            var items = results?.ToList() ?? new List<BatchResultItem>();

            _logger.LogInformation("Начало экспорта группового отчета ({Count} записей) в PDF", items.Count);

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Document.Create(container => BuildBatchPage(container, items))
                        .GeneratePdf(filePath);
                }, cancellationToken);

                _logger.LogInformation("Экспорт группового отчета завершен");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте группового отчета в PDF");
                throw;
            }
        }

        public async Task ExportExchangeRateAsync(Rate rate, string filePath, CancellationToken cancellationToken = default)
        {
            if (rate == null)
                throw new ArgumentNullException(nameof(rate));

            _logger.LogInformation("Начало экспорта курса валюты {Code} в PDF", rate.Cur_Abbreviation);

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Document.Create(container => BuildExchangeRatePage(container, rate)).GeneratePdf(filePath);
                }, cancellationToken);

                _logger.LogInformation("Экспорт курса валюты {Code} завершен", rate.Cur_Abbreviation);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте курса валюты {Code} в PDF", rate.Cur_Abbreviation);
                throw;
            }
        }

        public async Task ExportAllRatesAsync(IEnumerable<Rate> rates, DateTime onDate, string filePath, CancellationToken cancellationToken = default)
        {
            var items = rates?.ToList() ?? new List<Rate>();

            _logger.LogInformation("Начало экспорта курсов валют на {Date} ({Count} записей) в PDF", onDate.ToString("dd.MM.yyyy"), items.Count);

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Document.Create(container => BuildAllRatesPage(container, items, onDate)).GeneratePdf(filePath);
                }, cancellationToken);

                _logger.LogInformation("Экспорт курсов валют на {Date} завершен", onDate.ToString("dd.MM.yyyy"));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте курсов валют на {Date} в PDF", onDate.ToString("dd.MM.yyyy"));
                throw;
            }
        }

        public async Task ExportDynamicsAsync(IEnumerable<RateShort> dynamics, string currencyLabel, DateTime startDate, DateTime endDate, string filePath, CancellationToken cancellationToken = default)
        {
            var items = dynamics?.ToList() ?? new List<RateShort>();

            _logger.LogInformation("Начало экспорта динамики курса {Label} за период ({Count} точек) в PDF", currencyLabel, items.Count);

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Document.Create(container => BuildDynamicsPage(container, items, currencyLabel, startDate, endDate)).GeneratePdf(filePath);
                }, cancellationToken);

                _logger.LogInformation("Экспорт динамики курса {Label} завершен", currencyLabel);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте динамики курса {Label} в PDF", currencyLabel);
                throw;
            }
        }

        public async Task ExportBelstatDataAsync(string title, string subtitle, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows, string filePath, CancellationToken cancellationToken = default)
        {
            var headerList = headers ?? Array.Empty<string>();
            var rowList = rows ?? Array.Empty<string[]>();

            _logger.LogInformation("Начало экспорта данных Белстата «{Title}» ({Count} строк) в PDF", title, rowList.Count);

            try
            {
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Document.Create(container => BuildBelstatPage(container, title, subtitle, headerList, rowList)).GeneratePdf(filePath);
                }, cancellationToken);

                _logger.LogInformation("Экспорт данных Белстата «{Title}» завершен", title);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте данных Белстата «{Title}» в PDF", title);
                throw;
            }
        }

        private static void BuildCompanyPage(IDocumentContainer container, CompanyRow? company, SearchHistory? entry)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(TextStyle.Default.FontFamily(DefaultFont).FontSize(BodyFontSize));

                // Шапка (логотип + заголовок) — только на первой странице; при разбивке на несколько
                // страниц на последующих страницах шапка не повторяется.
                page.Header().ShowOnce().Element(BuildHeader);

                page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
                {
                    column.Spacing(5);

                    if (entry != null)
                    {
                        column.Item().Background("#F0F8F8").Padding(10).Column(history =>
                        {
                            history.Spacing(3);
                            AddField(history, "Дата поиска", entry.SearchDate.ToString("dd.MM.yyyy HH:mm"));
                            AddField(history, "Тип", entry.SearchType == "Legal" ? "Юрлицо/ИП" : "Физлицо");
                            AddField(history, "УНП", entry.Unp);
                            AddField(history, "Наименование", entry.FullName);
                            AddField(history, "Параметры поиска", entry.SearchParams);

                            if (!string.IsNullOrWhiteSpace(entry.UpdateInfo))
                            {
                                history.Item().PaddingTop(4).MaxWidth(MaxFieldWidth).Text(text =>
                                {
                                    text.Span("Изменения: ").SemiBold().FontSize(BodyFontSize);
                                    text.Span(entry.UpdateInfo).Bold().FontColor(Colors.Orange.Darken2).FontSize(BodyFontSize);
                                });
                            }
                        });

                        column.Item().LineHorizontal(1, Unit.Point).LineColor("#1A8B8B");
                    }

                    if (company == null)
                    {
                        column.Item().Text("Данные не найдены в сохраненной записи.").FontSize(12);
                        return;
                    }

                    column.Item().Text("ИНФОРМАЦИЯ О КОНТРАГЕНТЕ").FontSize(13).Bold().FontColor("#1A5C5C");
                    column.Item().LineHorizontal(1, Unit.Point).LineColor("#1A8B8B");
                    column.Item().Element(c => BuildCompanyTable(c, company));

                    bool active = company.Status == "Действующий" || company.Status == "Активный";
                    column.Item().PaddingTop(10).Background(active ? "#E8F5E9" : "#FDECEA").Padding(10)
                        .Text(active ? "КОНТРАГЕНТ ДЕЙСТВУЕТ" : "КОНТРАГЕНТ НЕ ДЕЙСТВУЕТ - будьте осторожны!")
                        .FontSize(13)
                        .Bold()
                        .FontColor(active ? Colors.Green.Darken2 : Colors.Red.Darken2);

                    column.Item().Element(BuildSignature);
                });

                page.Footer().Element(BuildFooter);
            });
        }

        private static void BuildBatchPage(IDocumentContainer container, IReadOnlyList<BatchResultItem> results)
        {
            if (results.Count == 0)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(TextStyle.Default.FontFamily(DefaultFont).FontSize(BodyFontSize));

                    page.Header().Element(BuildHeader);

                    page.Content().PaddingVertical(1, Unit.Centimetre)
                        .AlignCenter().AlignMiddle().Text("Нет данных для экспорта")
                        .FontSize(13).Bold().FontColor("#1A5C5C");

                    page.Footer().Element(BuildFooter);
                });

                return;
            }

            foreach (var item in results)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(TextStyle.Default.FontFamily(DefaultFont).FontSize(BodyFontSize));

                    page.Header().Element(BuildHeader);

                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
                    {
                        column.Spacing(5);

                        column.Item().Text("Результат проверки контрагента")
                            .FontSize(16).Bold().FontColor("#1A5C5C");
                        column.Item().LineHorizontal(1, Unit.Point).LineColor("#1A8B8B");

                        if (!string.IsNullOrWhiteSpace(item.Error))
                            column.Item().Element(c => BuildBatchErrorBlock(c, item));
                        else
                            column.Item().Element(c => BuildBatchItemTable(c, item));

                        column.Item().Element(BuildSignature);
                    });

                    page.Footer().Element(BuildFooter);
                });
            }
        }

        private static void BuildBatchErrorBlock(IContainer container, BatchResultItem item)
        {
            container.Background("#FDECEA").Padding(10).Column(column =>
            {
                column.Spacing(3);
                column.Item().Text("ОШИБКА ПРИ ВЫПОЛНЕНИИ ЗАПРОСА")
                    .FontSize(13).Bold().FontColor(Colors.Red.Darken2);
                column.Item().LineHorizontal(1, Unit.Point).LineColor(Colors.Red.Darken2);

                if (!string.IsNullOrWhiteSpace(item.Query))
                    AddField(column, "Запрос", item.Query);
                AddField(column, "УНП", item.Unp);
                AddField(column, "Ошибка", item.Error);
            });
        }

        private static void BuildBatchItemTable(IContainer container, BatchResultItem item)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(150);
                    columns.RelativeColumn();
                });

                static IContainer CellStyleLabel(IContainer cell) =>
                    cell.Border(0.5f).Background("#F0F8F8").Padding(6)
                        .DefaultTextStyle(x => x.FontSize(10).Bold().FontColor("#1A5C5C"));

                static IContainer CellStyleValue(IContainer cell) =>
                    cell.Border(0.5f).Padding(6)
                        .DefaultTextStyle(x => x.FontSize(10));

                void AddRow(string label, string value)
                {
                    table.Cell().Element(CellStyleLabel).Text(label);
                    table.Cell().Element(CellStyleValue).Text(Value(value));
                }

                AddRow("УНП", item.Unp);
                AddRow("Наименование", item.Name);
                AddRow("Статус", item.Status);
                AddRow("Адрес", item.Address);
                AddRow("Инспекция МНС", item.Inspection);
                AddRow("Дата регистрации", DateValue(item.RegistrationDate));
                AddRow("Дата проверки", Value(item.CheckDate));
            });
        }

        private static void BuildDynamicsPage(IDocumentContainer container, List<RateShort> dynamics, string currencyLabel, DateTime startDate, DateTime endDate)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(TextStyle.Default.FontFamily(DefaultFont).FontSize(BodyFontSize));

                // Шапка (логотип + заголовок) — только на первой странице; на последующих страницах
                // отображается исключительно продолжение таблицы.
                page.Header().ShowOnce().Element(BuildCurrencyHeader);

                page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
                {
                    column.Spacing(8);

                    column.Item().Text($"Динамика курса: {currencyLabel}")
                        .FontSize(16).Bold().FontColor("#1A5C5C");
                    column.Item().LineHorizontal(1, Unit.Point).LineColor("#1A8B8B");
                    column.Item().Text($"Период: {startDate:dd.MM.yyyy} — {endDate:dd.MM.yyyy}").FontSize(10).FontColor("#666666");

                    if (dynamics.Count > 0)
                    {
                        decimal min = dynamics.Min(x => x.Cur_OfficialRate);
                        decimal max = dynamics.Max(x => x.Cur_OfficialRate);
                        decimal avg = dynamics.Average(x => x.Cur_OfficialRate);
                        column.Item()
                            .Text($"Минимум: {min:0.0000} BYN   Максимум: {max:0.0000} BYN   Среднее: {avg:0.0000} BYN")
                            .FontSize(10).FontColor("#666666");
                    }

                    column.Item().Element(c => BuildDynamicsTable(c, dynamics));

                    // Подпись «Проверил следователь» прижимается к низу последней страницы отчёта.
                    column.Item().Element(BuildSignature);
                });

                page.Footer().Element(BuildCurrencyFooter);
            });
        }

        private static void BuildDynamicsTable(IContainer container, List<RateShort> dynamics)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(120);
                });

                static IContainer HeaderCell(IContainer cell) =>
                    cell.Border(0.5f).Background("#1A8B8B").Padding(5)
                        .DefaultTextStyle(x => x.FontSize(10).Bold().FontColor(Colors.White));

                static IContainer ValueCell(IContainer cell) =>
                    cell.Border(0.5f).Padding(5)
                        .DefaultTextStyle(x => x.FontSize(10));

                table.Cell().Element(HeaderCell).Text("Дата");
                table.Cell().Element(HeaderCell).Text("Курс, BYN");

                foreach (var item in dynamics)
                {
                    table.Cell().Element(ValueCell).Text(item.Date.ToString("dd.MM.yyyy"));
                    table.Cell().Element(ValueCell).Text($"{item.Cur_OfficialRate:0.0000}");
                }
            });
        }

        private static void BuildExchangeRatePage(IDocumentContainer container, Rate rate)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(TextStyle.Default.FontFamily(DefaultFont).FontSize(BodyFontSize));

                // Шапка (логотип + заголовок) — только на первой странице; на последующих страницах
                // отображается исключительно продолжение таблицы.
                page.Header().ShowOnce().Element(BuildCurrencyHeader);

                page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
                {
                    column.Spacing(8);

                    column.Item().Text("Официальный курс валюты Национального банка Республики Беларусь")
                        .FontSize(16).Bold().FontColor("#1A5C5C");
                    column.Item().LineHorizontal(1, Unit.Point).LineColor("#1A8B8B");

                    column.Item().Element(c => BuildExchangeRateTable(c, rate));

                    // Подпись «Проверил следователь» прижимается к низу последней страницы отчёта.
                    column.Item().Element(BuildSignature);
                });

                page.Footer().Element(BuildCurrencyFooter);
            });
        }

        private static void BuildExchangeRateTable(IContainer container, Rate rate)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(150);
                    columns.RelativeColumn();
                });

                static IContainer CellStyleLabel(IContainer cell) =>
                    cell.Border(0.5f).Background("#F0F8F8").Padding(6)
                        .DefaultTextStyle(x => x.FontSize(10).Bold().FontColor("#1A5C5C"));

                static IContainer CellStyleValue(IContainer cell) =>
                    cell.Border(0.5f).Padding(6)
                        .DefaultTextStyle(x => x.FontSize(10));

                void AddRow(string label, string value)
                {
                    table.Cell().Element(CellStyleLabel).Text(label);
                    table.Cell().Element(CellStyleValue).Text(Value(value));
                }

                AddRow("Код валюты", rate.Cur_Abbreviation);
                AddRow("Наименование", rate.Cur_Name);
                AddRow("Дата", rate.Date.ToString("dd.MM.yyyy"));
                AddRow("Масштаб", rate.Cur_Scale.ToString());
                AddRow("Официальный курс (за масштаб)", $"{rate.Cur_OfficialRate:0.0000} BYN");
                AddRow("Курс за 1 единицу", $"{rate.UnitRate:0.0000} BYN");
            });
        }

        private static void BuildAllRatesPage(IDocumentContainer container, List<Rate> rates, DateTime onDate)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(TextStyle.Default.FontFamily(DefaultFont).FontSize(BodyFontSize));

                // Шапка (логотип + заголовок) — только на первой странице; на последующих страницах
                // отображается исключительно продолжение таблицы.
                page.Header().ShowOnce().Element(BuildCurrencyHeader);

                page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
                {
                    column.Spacing(8);

                    column.Item().Text($"Курсы валют НБ РБ на {onDate:dd.MM.yyyy}")
                        .FontSize(16).Bold().FontColor("#1A5C5C");
                    column.Item().LineHorizontal(1, Unit.Point).LineColor("#1A8B8B");
                    column.Item().Text($"Всего валют: {rates.Count}").FontSize(10).FontColor("#666666");

                    column.Item().Element(c => BuildRatesTable(c, rates));

                    // Подпись «Проверил следователь» прижимается к низу последней страницы отчёта.
                    column.Item().Element(BuildSignature);
                });

                page.Footer().Element(BuildCurrencyFooter);
            });
        }

        /// <summary>
        /// Универсальная страница отчёта по данным государственной статистики:
        /// заголовок, подзаголовок, счётчик строк и таблица с произвольным набором колонок.
        /// </summary>
        private static void BuildBelstatPage(IDocumentContainer container, string title, string subtitle, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(TextStyle.Default.FontFamily(DefaultFont).FontSize(BodyFontSize));

                page.Header().ShowOnce().Element(BuildBelstatHeader);

                page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
                {
                    column.Spacing(8);

                    column.Item().Text(title).FontSize(16).Bold().FontColor("#1A5C5C");
                    column.Item().LineHorizontal(1, Unit.Point).LineColor("#1A8B8B");

                    if (!string.IsNullOrWhiteSpace(subtitle))
                        column.Item().Text(subtitle).FontSize(10).FontColor("#666666");

                    column.Item().Text($"Строк в таблице: {rows.Count}").FontSize(10).FontColor("#666666");

                    if (rows.Count == 0)
                        column.Item().Text("Данные отсутствуют.").FontSize(11).Italic().FontColor("#666666");
                    else
                        column.Item().Element(c => BuildBelstatTable(c, headers, rows));

                    // Подпись «Проверил следователь» прижимается к низу последней страницы отчёта.
                    column.Item().Element(BuildSignature);
                });

                page.Footer().Element(BuildBelstatFooter);
            });
        }

        private static void BuildBelstatTable(IContainer container, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
        {
            var columnCount = Math.Max(headers.Count, rows.Max(row => row.Length));
            if (columnCount == 0)
                return;

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    for (var i = 0; i < columnCount; i++)
                        columns.RelativeColumn();
                });

                static IContainer HeaderCell(IContainer cell) =>
                    cell.Border(0.5f).Background("#1A8B8B").Padding(4)
                        .DefaultTextStyle(x => x.FontSize(8).Bold().FontColor(Colors.White));

                static IContainer ValueCell(IContainer cell) =>
                    cell.Border(0.5f).Padding(4)
                        .DefaultTextStyle(x => x.FontSize(8));

                for (var i = 0; i < columnCount; i++)
                    table.Cell().Element(HeaderCell).Text(i < headers.Count ? headers[i] : string.Empty);

                foreach (var row in rows)
                {
                    for (var i = 0; i < columnCount; i++)
                        table.Cell().Element(ValueCell).Text(i < row.Length ? row[i] : string.Empty);
                }
            });
        }

        /// <summary>Шапка отчёта по данным Белстата: тот же бланк СК, с подзаголовком про госстатистику.</summary>
        private static void BuildBelstatHeader(IContainer container) => BuildHeader(container, BelstatHeaderSubText);

        /// <summary>
        /// Подвал отчёта по данным Белстата: сведения об источнике и номер страницы на каждой
        /// странице. Подпись «Проверил следователь» выводится в теле отчёта (внизу последней
        /// страницы), поэтому в подвале она не дублируется.
        /// </summary>
        private static void BuildBelstatFooter(IContainer container)
        {
            container.PaddingTop(8).BorderTop(0.5f).BorderColor("#CCCCCC").Column(column =>
            {
                column.Item().PaddingTop(4).AlignCenter()
                    .Text($"Источник данных: {BelstatDataSource}. Отчет сгенерирован: {DateTimeHelper.NowBelarus:dd.MM.yyyy HH:mm}")
                    .FontSize(9).FontColor("#666666");

                column.Item().AlignCenter().Text(text =>
                {
                    text.Span("Страница ").FontSize(9).FontColor("#666666");
                    text.CurrentPageNumber().FontSize(9).FontColor("#666666");
                    text.Span(" из ").FontSize(9).FontColor("#666666");
                    text.TotalPages().FontSize(9).FontColor("#666666");
                });
            });
        }

        private static void BuildRatesTable(IContainer container, List<Rate> rates)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(70);
                    columns.RelativeColumn();
                    columns.ConstantColumn(60);
                    columns.ConstantColumn(90);
                });

                static IContainer HeaderCell(IContainer cell) =>
                    cell.Border(0.5f).Background("#1A8B8B").Padding(5)
                        .DefaultTextStyle(x => x.FontSize(10).Bold().FontColor(Colors.White));

                static IContainer ValueCell(IContainer cell) =>
                    cell.Border(0.5f).Padding(5)
                        .DefaultTextStyle(x => x.FontSize(10));

                table.Cell().Element(HeaderCell).Text("Код");
                table.Cell().Element(HeaderCell).Text("Наименование");
                table.Cell().Element(HeaderCell).Text("Масштаб");
                table.Cell().Element(HeaderCell).Text("Курс, BYN");

                foreach (var rate in rates)
                {
                    table.Cell().Element(ValueCell).Text(rate.Cur_Abbreviation);
                    table.Cell().Element(ValueCell).Text(rate.Cur_Name);
                    table.Cell().Element(ValueCell).Text(rate.Cur_Scale.ToString());
                    table.Cell().Element(ValueCell).Text($"{rate.Cur_OfficialRate:0.0000}");
                }
            });
        }

        private static void BuildHeader(IContainer container) => BuildHeader(container, HeaderSubText);

        /// <summary>
        /// Шапка отчётов по курсам валют: тот же бланк СК, но с подзаголовком про данные НБ РБ.
        /// </summary>
        private static void BuildCurrencyHeader(IContainer container) => BuildHeader(container, CurrencyHeaderSubText);

        private static void BuildHeader(IContainer container, string subText)
        {
            container.BorderBottom(2).BorderColor("#1A8B8B").PaddingBottom(8).Row(row =>
            {
                var logo = LogoBytes;

                if (logo != null)
                {
                    // FitWidth() и FitHeight() конфликтуют: каждая перезаписывает стратегию масштабирования,
                    // а в контейнере с фиксированной шириной и неограниченной высотой FitHeight приводит
                    // к ошибке компоновки. FitArea() подбирает масштаб по обеим осям без искажений.
                    row.ConstantItem(100).PaddingHorizontal(6).AlignCenter().AlignMiddle()
                        .Image(logo).FitArea();
                }
                else
                {
                    row.ConstantItem(90).PaddingHorizontal(6).PaddingVertical(2).Border(1f).BorderColor("#1A8B8B").AlignCenter().AlignMiddle()
                        .Text("SK").FontSize(22);
                }

                row.RelativeItem().PaddingLeft(10).AlignMiddle().Column(column =>
                {
                    column.Spacing(3);
                    column.Item().Text(HeaderText).FontSize(20).Bold().FontColor("#1A8B8B");
                    column.Item().Text(subText).FontSize(10).FontColor("#666666");
                });
            });
        }

        /// <summary>
        /// Блок подписи «Проверил следователь». Контейнер растягивается на всю оставшуюся высоту
        /// страницы (ExtendVertical) и прижимает содержимое к нижней кромке (AlignBottom), поэтому
        /// подпись всегда оказывается внизу последней страницы отчёта — над строкой «Страница N из M».
        /// Правое выравнивание задаётся у самих строк, чтобы не переопределять вертикальное выравнивание.
        /// </summary>
        private static void BuildSignature(IContainer container)
        {
            container.ExtendVertical().AlignBottom().PaddingBottom(4).Column(column =>
            {
                column.Spacing(4);
                column.Item().AlignRight().Text("Проверил следователь _______________").FontSize(11);
                column.Item().AlignRight().Text($"Дата: {DateTimeHelper.NowBelarus:dd.MM.yyyy}").FontSize(11);
            });
        }

        private static void BuildFooter(IContainer container) => BuildFooter(container, "grp.nalog.gov.by");

        private static void BuildFooter(IContainer container, string dataSource)
        {
            container.PaddingTop(8).BorderTop(0.5f).BorderColor("#CCCCCC").Column(column =>
            {
                column.Item().AlignCenter().Text($"Отчет сгенерирован: {DateTimeHelper.NowBelarus:dd.MM.yyyy HH:mm}").FontSize(9).FontColor("#666666");
                column.Item().AlignCenter().Text($"Источник данных: {dataSource}").FontSize(9).FontColor("#666666");
                column.Item().AlignCenter().Text(text =>
                {
                    text.Span("Страница ").FontSize(9).FontColor("#666666");
                    text.CurrentPageNumber().FontSize(9).FontColor("#666666");
                    text.Span(" из ").FontSize(9).FontColor("#666666");
                    text.TotalPages().FontSize(9).FontColor("#666666");
                });
            });
        }

        /// <summary>
        /// Подвал отчётов по курсам валют: номер страницы на каждой странице. Заголовок и логотип
        /// на последующих страницах не повторяются. Подпись «Проверил следователь» выводится
        /// в теле отчёта (внизу последней страницы), поэтому в подвале она не дублируется.
        /// </summary>
        private static void BuildCurrencyFooter(IContainer container)
        {
            container.PaddingTop(8).BorderTop(0.5f).BorderColor("#CCCCCC").Column(column =>
            {
                // Номер страницы — на каждой странице.
                column.Item().PaddingTop(4).AlignCenter().Text(text =>
                {
                    text.Span("Страница ").FontSize(9).FontColor("#666666");
                    text.CurrentPageNumber().FontSize(9).FontColor("#666666");
                    text.Span(" из ").FontSize(9).FontColor("#666666");
                    text.TotalPages().FontSize(9).FontColor("#666666");
                });
            });
        }

        private static void BuildCompanyTable(IContainer container, CompanyRow company)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(150);
                    columns.RelativeColumn();
                });

                static IContainer CellStyleLabel(IContainer cell) =>
                    cell.Border(0.5f).Background("#F0F8F8").Padding(6)
                        .DefaultTextStyle(x => x.FontSize(10).Bold().FontColor("#1A5C5C"));

                static IContainer CellStyleValue(IContainer cell) =>
                    cell.Border(0.5f).Padding(6)
                        .DefaultTextStyle(x => x.FontSize(10));

                void AddRow(string label, string value)
                {
                    table.Cell().Element(CellStyleLabel).Text(label);
                    table.Cell().Element(CellStyleValue).Text(Value(value));
                }

                AddRow("УНП", company.Unp);
                AddRow("Полное наименование", company.FullName);
                AddRow("Краткое наименование", company.ShortName);
                AddRow("Адрес", company.Address);
                AddRow("Дата регистрации", DateValue(company.RegistrationDate));
                AddRow("Инспекция МНС", InspectionText(company.InspectionName, company.InspectionCode));
                AddRow("Статус", StatusText(company.Status, company.StatusCode));
                AddRow("Дата изменения статуса", DateValue(company.StatusChangeDate));
                if (!string.IsNullOrEmpty(company.LiquidationDate))
                    AddRow("Дата ликвидации", DateValue(company.LiquidationDate));
            });
        }

        private static void AddField(ColumnDescriptor column, string label, string value)
        {
            column.Item().PaddingVertical(2).MaxWidth(MaxFieldWidth).Text(text =>
            {
                text.Span(label + ": ").SemiBold().FontSize(BodyFontSize);
                text.Span(Value(value)).FontSize(BodyFontSize);
            });
        }

        private static CompanyRow? TryParseCompany(string? resultData)
        {
            if (string.IsNullOrWhiteSpace(resultData)) return null;
            try
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<NalogResponse>(resultData)?.Row
                       ?? Newtonsoft.Json.JsonConvert.DeserializeObject<CompanyRow>(resultData);
            }
            catch
            {
                return null;
            }
        }

        private static string Value(string? value) =>
            string.IsNullOrWhiteSpace(value) ? NotSpecified : value;

        private static string DateValue(string? value)
        {
            string formatted = StringHelper.FormatDate(value ?? string.Empty);
            return string.IsNullOrEmpty(formatted) ? NotSpecified : formatted;
        }

        private static string InspectionText(string? name, string? code)
        {
            string value = Value(name);
            string codeValue = Value(code);
            return codeValue == NotSpecified ? value : $"{value} (код {codeValue})";
        }

        private static string StatusText(string? status, string? code)
        {
            string value = Value(status);
            string codeValue = Value(code);
            return codeValue == NotSpecified ? value : $"{value} (код {codeValue})";
        }

        private static string StatusColor(string? status) =>
            status switch
            {
                "Действующий" or "Активный" => Colors.Green.Darken2,
                "Ликвидирован" or "Исключен" => Colors.Red.Darken2,
                _ => Colors.Orange.Darken2
            };
    }
}
