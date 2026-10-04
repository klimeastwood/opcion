using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NalogParserWPF.Models;

namespace NalogParserWPF.Database
{
    /// <summary>
    /// Репозиторий истории запросов курсов валют (таблица ExchangeHistory).
    /// </summary>
    public interface IExchangeHistoryRepository
    {
        Task AddAsync(ExchangeHistoryItem item, CancellationToken cancellationToken = default);
        Task<List<ExchangeHistoryItem>> GetAllAsync(CancellationToken cancellationToken = default);
        Task ClearAsync(CancellationToken cancellationToken = default);
    }
}
