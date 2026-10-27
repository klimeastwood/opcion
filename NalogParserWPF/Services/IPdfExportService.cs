using System;
using System.Threading;
using System.Threading.Tasks;
using NalogParserWPF.Models;

namespace NalogParserWPF.Services
{
    public interface IPdfExportService
    {
        Task ExportCompanyInfoAsync(CompanyRow company, string filePath, CancellationToken cancellationToken = default);
        Task ExportHistoryEntryAsync(SearchHistory entry, string filePath, CancellationToken cancellationToken = default);
        Task ExportBatchResultsAsync(IEnumerable<BatchResultItem> results, string filePath, CancellationToken cancellationToken = default);

        /// <summary>Экспорт официального курса одной валюты в PDF.</summary>
        Task ExportExchangeRateAsync(Rate rate, string filePath, CancellationToken cancellationToken = default);

        /// <summary>Экспорт курсов всех валют на дату в PDF.</summary>
        Task ExportAllRatesAsync(IEnumerable<Rate> rates, DateTime onDate, string filePath, CancellationToken cancellationToken = default);

        /// <summary>Экспорт динамики курса валюты за период в PDF.</summary>
        Task ExportDynamicsAsync(IEnumerable<RateShort> dynamics, string currencyLabel, DateTime startDate, DateTime endDate, string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Экспорт таблицы данных государственной статистики (Белстат) в PDF.
        /// Таблица универсальная: набор колонок и строк формирует вызывающий код.
        /// <paramref name="filterNote"/> — необязательная пометка о том, что выгружены не все строки
        /// результата, а отобранные фильтром (пустая строка/отсутствие — пометка не выводится).
        /// </summary>
        Task ExportBelstatDataAsync(string title, string subtitle, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows, string filePath, string? filterNote = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Экспорт интерпретации статистического ряда (Белстат) в PDF: сводные показатели,
        /// разбор по годам, примечания статистического анализа и рекомендации для следователя.
        /// </summary>
        Task ExportInterpretationAsync(InterpretationResult result, string filePath, CancellationToken cancellationToken = default);
    }
}
