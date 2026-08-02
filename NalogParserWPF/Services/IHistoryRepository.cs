using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NalogParserWPF.Models;

namespace NalogParserWPF.Services
{
    public interface IHistoryRepository
    {
        Task AddAsync(SearchHistory entry, CancellationToken cancellationToken = default);
        Task<List<SearchHistory>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<SearchHistory?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<List<SearchHistory>> GetByUnpAsync(string unp, CancellationToken cancellationToken = default);
        Task UpdateStatusAsync(int id, string newStatus, CancellationToken cancellationToken = default);
        Task DeleteAsync(int id, CancellationToken cancellationToken = default);
        Task ClearAllAsync(CancellationToken cancellationToken = default);
        Task<List<SearchHistory>> GetActiveUnpsAsync(CancellationToken cancellationToken = default);
    }
}
