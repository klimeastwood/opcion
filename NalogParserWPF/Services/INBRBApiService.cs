using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NalogParserWPF.Models;

namespace NalogParserWPF.Services
{
    /// <summary>
    /// Клиент API Национального банка Республики Беларусь (api.nbrb.by/exrates).
    /// </summary>
    public interface INBRBApiService
    {
        /// <summary>Полный перечень валют, к которым устанавливается официальный курс.</summary>
        Task<List<Currency>> GetCurrenciesAsync(CancellationToken cancellationToken = default);

        /// <summary>Курс валюты по внутреннему коду (Cur_ID) на дату. null — если валюта/дата не найдена.</summary>
        Task<Rate?> GetRateAsync(int curId, DateTime? ondate = null, CancellationToken cancellationToken = default);

        /// <summary>Курс валюты по буквенному коду (ISO, например USD) на дату. null — если не найдено.</summary>
        Task<Rate?> GetRateByCodeAsync(string isoCode, DateTime? ondate = null, CancellationToken cancellationToken = default);

        /// <summary>Динамика курса валюты за период.</summary>
        Task<List<RateShort>> GetRateDynamicsAsync(int curId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        /// <summary>Курсы всех валют на дату (ежедневная периодичность).</summary>
        Task<List<Rate>> GetAllRatesAsync(DateTime? ondate = null, CancellationToken cancellationToken = default);
    }
}
