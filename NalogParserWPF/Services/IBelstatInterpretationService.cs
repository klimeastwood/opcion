using System.Data;
using System.Threading;
using System.Threading.Tasks;
using NalogParserWPF.Models;

namespace NalogParserWPF.Services
{
    /// <summary>
    /// Интерпретация статистических данных Белстата для подготовки представлений
    /// об устранении причин и условий, способствовавших совершению преступлений.
    /// Работает поверх уже загруженной таблицы результата (DataTable) и не изменяет исходные данные.
    /// </summary>
    public interface IBelstatInterpretationService
    {
        /// <summary>
        /// Простая интерпретация: среднее значение, отклонение каждого года от среднего (в %)
        /// и пометка «в пределах нормы / выше-ниже среднего / аномалия».
        /// </summary>
        InterpretationResult AnalyzeSimple(DataTable data, string indicatorName);

        /// <summary>
        /// Расширенная интерпретация: дополнительно тренд (линейная регрессия),
        /// стандартное отклонение, скачки «год к году» и устойчивые изменения,
        /// аномалии по правилу «более 2σ» и детальные рекомендации по типу показателя.
        /// </summary>
        InterpretationResult AnalyzeAdvanced(DataTable data, string indicatorName);

        /// <summary>
        /// Фоновый вариант простой интерпретации: тяжёлый расчёт выполняется вне UI-потока
        /// (через <see cref="Task.Run(System.Action)"/>), чтобы окно не замерзало на 50 000+ строках.
        /// </summary>
        Task<InterpretationResult> AnalyzeSimpleAsync(DataTable data, string indicatorName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Фоновый вариант расширенной интерпретации: тяжёлый расчёт выполняется вне UI-потока.
        /// </summary>
        Task<InterpretationResult> AnalyzeAdvancedAsync(DataTable data, string indicatorName, CancellationToken cancellationToken = default);
    }
}
