using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NalogParserWPF.Models;

namespace NalogParserWPF.Database
{
    /// <summary>
    /// Репозиторий истории запросов государственной статистики (таблица BelstatHistory).
    /// </summary>
    public interface IBelstatHistoryRepository
    {
        Task AddAsync(BelstatHistoryItem item, CancellationToken cancellationToken = default);
        Task<List<BelstatHistoryItem>> GetAllAsync(CancellationToken cancellationToken = default);
        Task ClearAsync(CancellationToken cancellationToken = default);
    }
}
