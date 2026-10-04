using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NalogParserWPF.Helpers;
using NalogParserWPF.Models;

namespace NalogParserWPF.Services
{
    /// <summary>
    /// Реализация клиента API НБ РБ (https://api.nbrb.by/exrates).
    /// Ответы кэшируются на 5 минут; 404 трактуется как «валюта/курс не найден» (возвращается null).
    /// </summary>
    public class NBRBApiService : INBRBApiService
    {
        private const string CacheKeyPrefix = "nbrb:";
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

        private readonly HttpClient _httpClient;
        private readonly ILogger<NBRBApiService> _logger;
        private readonly ICacheService _cache;

        public NBRBApiService(HttpClient httpClient, ILogger<NBRBApiService> logger, ICacheService cache)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cache = cache;
        }

        public async Task<List<Currency>> GetCurrenciesAsync(CancellationToken cancellationToken = default)
        {
            const string cacheKey = CacheKeyPrefix + "currencies";
            if (_cache.TryGet<List<Currency>>(cacheKey, out var cached) && cached != null)
            {
                _logger.LogInformation("Список валют НБ РБ возвращен из кэша ({Count})", cached.Count);
                return cached;
            }

            try
            {
                _logger.LogInformation("Запрос к API НБ РБ: currencies");
                var currencies = await GetAsync<List<Currency>>("currencies", cancellationToken) ?? new List<Currency>();

                await _cache.SetAsync(cacheKey, currencies, CacheExpiry, cancellationToken);
                _logger.LogInformation("Получено валют НБ РБ: {Count}", currencies.Count);
                return currencies;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка валют НБ РБ");
                throw;
            }
        }

        public async Task<Rate?> GetRateAsync(int curId, DateTime? ondate = null, CancellationToken cancellationToken = default)
        {
            var date = ondate?.Date ?? DateTimeHelper.TodayBelarus;
            string cacheKey = $"{CacheKeyPrefix}rate:id:{curId}:{date:yyyy-MM-dd}";

            if (_cache.TryGet<Rate>(cacheKey, out var cached))
            {
                _logger.LogInformation("Курс НБ РБ (curId: {CurId}) возвращен из кэша", curId);
                return cached;
            }

            string url = $"rates/{curId}?ondate={date:yyyy-MM-dd}";
            _logger.LogInformation("Запрос к API НБ РБ: {Url}", url);

            var rate = await GetAsync<Rate>(url, cancellationToken);
            if (rate == null)
            {
                _logger.LogWarning("Курс валюты {CurId} на {Date} не найден (404)", curId, date.ToString("yyyy-MM-dd"));
                return null;
            }

            await _cache.SetAsync(cacheKey, rate, CacheExpiry, cancellationToken);
            return rate;
        }

        public async Task<Rate?> GetRateByCodeAsync(string isoCode, DateTime? ondate = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(isoCode))
                return null;

            var date = ondate?.Date ?? DateTimeHelper.TodayBelarus;
            string code = isoCode.Trim().ToUpperInvariant();
            string cacheKey = $"{CacheKeyPrefix}rate:code:{code}:{date:yyyy-MM-dd}";

            if (_cache.TryGet<Rate>(cacheKey, out var cached))
            {
                _logger.LogInformation("Курс НБ РБ (code: {Code}) возвращен из кэша", code);
                return cached;
            }

            // parammode=2 — поиск валюты по буквенному коду (ISO, например USD).
            string url = $"rates/{code}?ondate={date:yyyy-MM-dd}&parammode=2";
            _logger.LogInformation("Запрос к API НБ РБ: {Url}", url);

            var rate = await GetAsync<Rate>(url, cancellationToken);
            if (rate == null)
            {
                _logger.LogWarning("Курс валюты {Code} на {Date} не найден (404)", code, date.ToString("yyyy-MM-dd"));
                return null;
            }

            await _cache.SetAsync(cacheKey, rate, CacheExpiry, cancellationToken);
            return rate;
        }

        public async Task<List<Rate>> GetAllRatesAsync(DateTime? ondate = null, CancellationToken cancellationToken = default)
        {
            var date = ondate?.Date ?? DateTimeHelper.TodayBelarus;
            string cacheKey = $"{CacheKeyPrefix}rates:all:{date:yyyy-MM-dd}";

            if (_cache.TryGet<List<Rate>>(cacheKey, out var cached) && cached != null)
            {
                _logger.LogInformation("Курсы всех валют на {Date} возвращены из кэша ({Count})", date.ToString("yyyy-MM-dd"), cached.Count);
                return cached;
            }

            string url = $"rates?ondate={date:yyyy-MM-dd}&periodicity=0";
            _logger.LogInformation("Запрос к API НБ РБ: {Url}", url);

            var rates = await GetAsync<List<Rate>>(url, cancellationToken) ?? new List<Rate>();
            await _cache.SetAsync(cacheKey, rates, CacheExpiry, cancellationToken);

            _logger.LogInformation("Получено курсов на {Date}: {Count}", date.ToString("yyyy-MM-dd"), rates.Count);
            return rates;
        }

        public async Task<List<RateShort>> GetRateDynamicsAsync(int curId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            string cacheKey = $"{CacheKeyPrefix}dynamics:{curId}:{startDate:yyyy-MM-dd}:{endDate:yyyy-MM-dd}";

            if (_cache.TryGet<List<RateShort>>(cacheKey, out var cached) && cached != null)
            {
                _logger.LogInformation("Динамика курса {CurId} возвращена из кэша ({Count})", curId, cached.Count);
                return cached;
            }

            string url = $"rates/dynamics/{curId}?startdate={startDate:yyyy-MM-dd}&enddate={endDate:yyyy-MM-dd}";
            _logger.LogInformation("Запрос к API НБ РБ: {Url}", url);

            var dynamics = await GetAsync<List<RateShort>>(url, cancellationToken) ?? new List<RateShort>();
            await _cache.SetAsync(cacheKey, dynamics, CacheExpiry, cancellationToken);

            _logger.LogInformation("Получено точек динамики курса {CurId}: {Count}", curId, dynamics.Count);
            return dynamics;
        }

        /// <summary>
        /// Выполняет GET-запрос и десериализует JSON. 404 возвращает null (без исключения).
        /// </summary>
        private async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken) where T : class
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("API НБ РБ вернул 404 для {Url}", url);
                return null;
            }

            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
