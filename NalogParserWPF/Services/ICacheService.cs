using System;
using System.Threading;
using System.Threading.Tasks;

namespace NalogParserWPF.Services
{
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
        Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);
        Task ClearAsync(CancellationToken cancellationToken = default);
        bool TryGet<T>(string key, out T? value);
        void Set<T>(string key, T value, TimeSpan? expiry = null);
    }
}
