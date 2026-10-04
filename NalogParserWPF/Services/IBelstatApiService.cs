using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NalogParserWPF.Models;

namespace NalogParserWPF.Services
{
    /// <summary>
    /// Клиент публичного API Белстата (https://dataportal.belstat.gov.by/osids-public-api).
    /// </summary>
    public interface IBelstatApiService
    {
        /// <summary>Дерево тематических рубрик (rubric/rubricTreeNodeGet/0).</summary>
        Task<List<BelstatRubric>> GetRubricTreeAsync(CancellationToken cancellationToken = default);

        /// <summary>Показатели выбранной рубрики (rubric/rubricIndicatorsGet/{code}).</summary>
        Task<List<BelstatIndicator>> GetIndicatorsByRubricAsync(string rubricCode, CancellationToken cancellationToken = default);

        /// <summary>Поиск показателей по названию/ключевым словам (indicator/indicatorSearch).</summary>
        Task<List<BelstatIndicator>> SearchIndicatorsAsync(string query, CancellationToken cancellationToken = default);

        /// <summary>Значения показателя за указанные годы (sdmx-api/indicator/values/SDMX-JSON/{code}).</summary>
        Task<BelstatDataResponse?> GetIndicatorDataAsync(string indicatorCode, int[] years, CancellationToken cancellationToken = default);

        /// <summary>Значения всех показателей рубрики (sdmx-api/rubric/indicators/values/SDMX-JSON/{code}, ZIP).</summary>
        Task<BelstatDataResponse?> GetRubricDataAsync(string rubricCode, int[] years, CancellationToken cancellationToken = default);
    }
}
