using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NalogParserWPF.Services
{
    public class InMemoryCacheService : ICacheService, IDisposable
    {
        public static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(5);

        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
        private readonly ILogger<InMemoryCacheService> _logger;
        private readonly Timer _cleanupTimer;
        private bool _disposed;

        public InMemoryCacheService(ILogger<InMemoryCacheService> logger)
        {
            _logger = logger;
            _cleanupTimer = new Timer(_ => CleanupExpired(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Get<T>(key));
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Set(key, value, expiry);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _cache.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _cache.Clear();
            return Task.CompletedTask;
        }

        public bool TryGet<T>(string key, out T? value)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.IsExpired)
                {
                    _cache.TryRemove(key, out _);
                    value = default;
                    return false;
                }

                value = entry.Value is T t ? t : default;
                return true;
            }

            value = default;
            return false;
        }

        public void Set<T>(string key, T value, TimeSpan? expiry = null)
        {
            _cache[key] = new CacheEntry(value, DateTime.UtcNow.Add(expiry ?? DefaultExpiry));
        }

        private T? Get<T>(string key)
        {
            return TryGet<T>(key, out var value) ? value : default;
        }

        private void CleanupExpired()
        {
            try
            {
                var now = DateTime.UtcNow;
                int removed = 0;
                foreach (var pair in _cache)
                {
                    if (pair.Value.ExpiresAtUtc <= now && _cache.TryRemove(pair.Key, out _))
                        removed++;
                }

                if (removed > 0)
                    _logger.LogDebug("Очистка кэша: удалено просроченных записей {Count}", removed);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка при очистке просроченных записей кэша");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cleanupTimer.Dispose();
        }

        private sealed record CacheEntry(object? Value, DateTime ExpiresAtUtc)
        {
            public bool IsExpired => ExpiresAtUtc <= DateTime.UtcNow;
        }
    }
}
