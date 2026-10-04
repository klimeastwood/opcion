using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NalogParserWPF.Configuration;
using NalogParserWPF.Models;

namespace NalogParserWPF.Services
{
    public class NalogApiService : INalogApiService
    {
        public const string UnpNotFoundMessage = "УНП не найден в базе. Проверьте правильность ввода.";

        private const string CacheKeyPrefix = "api:";
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

        private readonly HttpClient _httpClient;
        private readonly ILogger<NalogApiService> _logger;
        private readonly ApiSettings _settings;
        private readonly ICacheService _cache;
        private readonly IHistoryRepository? _historyRepository;

        public NalogApiService(HttpClient httpClient, IOptions<ApiSettings> settings, ILogger<NalogApiService> logger, ICacheService cache, IHistoryRepository? historyRepository = null)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
            _cache = cache;
            _historyRepository = historyRepository;
        }

        public async Task<NalogResponse?> SearchLegalEntityAsync(LegalSearchParams parameters, CancellationToken cancellationToken = default)
        {
            string unp = parameters.Unp ?? string.Empty;

            string cacheKey = $"{CacheKeyPrefix}legal:unp:{unp}";
            if (_cache.TryGet<NalogResponse>(cacheKey, out var cached))
            {
                _logger.LogInformation("Возвращен результат из кэша для поиска юрлица (unp: {Unp})", unp);
                return cached;
            }

            try
            {
                string url = BuildDataUrl(unp);
                _logger.LogInformation("Запрос к API: {Url}", url);
                var response = await _httpClient.GetAsync(url, cancellationToken);
                await EnsureSuccessWithStatusAsync(response, cancellationToken);

                string jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                var data = JsonConvert.DeserializeObject<NalogResponse>(jsonResponse);

                await _cache.SetAsync(cacheKey, data, CacheExpiry, cancellationToken);

                _logger.LogInformation("Запрос к API успешно завершен для юрлица (unp: {Unp})", unp);
                return data;
            }
            catch (NalogApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return await TryGetFromCacheOrHistory(cacheKey, unp);
            }
            catch (NalogApiException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при запросе к API");
                throw new Exception($"Ошибка при поиске юрлица: {ex.Message}", ex);
            }
        }

        public async Task<NalogResponse?> SearchPhysicalPersonAsync(PhysicalSearchParams searchParams, CancellationToken cancellationToken = default)
        {
            string unp = searchParams.Unp ?? string.Empty;

            string cacheKey = $"{CacheKeyPrefix}physical:unp:{unp}";
            if (_cache.TryGet<NalogResponse>(cacheKey, out var cachedUnp))
            {
                _logger.LogInformation("Возвращен результат из кэша для поиска физлица (unp: {Unp})", unp);
                return cachedUnp;
            }

            try
            {
                var url = BuildDataUrl(unp);

                _logger.LogInformation("Запрос к API: {Url}", url);
                var response = await _httpClient.GetAsync(url, cancellationToken);
                await EnsureSuccessWithStatusAsync(response, cancellationToken);

                string jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                var data = JsonConvert.DeserializeObject<NalogResponse>(jsonResponse);

                await _cache.SetAsync(cacheKey, data, CacheExpiry, cancellationToken);

                _logger.LogInformation("Запрос к API успешно завершен для физлица (unp: {Unp})", unp);
                return data;
            }
            catch (NalogApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return await TryGetFromCacheOrHistory(cacheKey, unp);
            }
            catch (NalogApiException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при запросе к API");
                throw new Exception($"Ошибка при поиске физлица: {ex.Message}", ex);
            }
        }

        public async Task<NalogResponse?> GetCompanyInfoAsync(string unp, CancellationToken cancellationToken = default)
        {
            string cacheKey = $"{CacheKeyPrefix}{unp}";
            if (_cache.TryGet<NalogResponse>(cacheKey, out var cached))
            {
                _logger.LogInformation("Возвращен результат из кэша для УНП {Unp}", unp);
                return cached;
            }

            try
            {
                string url = BuildDataUrl(unp);
                _logger.LogInformation("Запрос к API: {Url}", url);
                var response = await _httpClient.GetAsync(url, cancellationToken);
                await EnsureSuccessWithStatusAsync(response, cancellationToken);

                string jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                var data = JsonConvert.DeserializeObject<NalogResponse>(jsonResponse);

                await _cache.SetAsync(cacheKey, data, CacheExpiry, cancellationToken);

                _logger.LogInformation("Запрос к API успешно завершен для УНП {Unp}", unp);
                return data;
            }
            catch (NalogApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return await TryGetFromCacheOrHistory(cacheKey, unp);
            }
            catch (NalogApiException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при запросе к API");
                throw new Exception($"Ошибка при получении информации: {ex.Message}", ex);
            }
        }

        private static bool IsUnpNotFoundError(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return false;

            return (responseBody.Contains("УНП", StringComparison.OrdinalIgnoreCase)
                    && (responseBody.Contains("не найден", StringComparison.OrdinalIgnoreCase)
                        || responseBody.Contains("отсутств", StringComparison.OrdinalIgnoreCase)
                        || responseBody.Contains("не существует", StringComparison.OrdinalIgnoreCase)))
                   || responseBody.Contains("данные по унп", StringComparison.OrdinalIgnoreCase);
        }

        private string BuildDataUrl(string unp)
        {
            var query = new List<string> { "charset=UTF-8", "type=json" };

            if (!string.IsNullOrWhiteSpace(unp))
                query.Add($"unp={Uri.EscapeDataString(unp)}");

            return $"{BaseEndpoint}/data?{string.Join("&", query)}";
        }

        // BaseUrl из конфигурации содержит полный путь эндпоинта, поэтому выделяем корень, чтобы избежать дублирования "/data"
        private string BaseEndpoint
        {
            get
            {
                string baseUrl = (_settings.BaseUrl ?? string.Empty).TrimEnd('/');
                const string dataSuffix = "/data";
                return baseUrl.EndsWith(dataSuffix, StringComparison.OrdinalIgnoreCase)
                    ? baseUrl[..^dataSuffix.Length]
                    : baseUrl;
            }
        }

        // Падение с 404: пробуем вернуть ранее закэшированный ответ либо сохраненный в истории результат
        private async Task<NalogResponse?> TryGetFromCacheOrHistory(string cacheKey, string unp)
        {
            if (_cache.TryGet<NalogResponse>(cacheKey, out var cached))
            {
                _logger.LogWarning("API вернул 404 для УНП {Unp}, но в кэше есть данные — возвращены данные из кэша", unp);
                return cached;
            }

            return await GetFromHistory(unp);
        }

        private async Task<NalogResponse?> GetFromHistory(string unp)
        {
            try
            {
                if (_historyRepository == null)
                    return null;

                var entries = await _historyRepository.GetByUnpAsync(unp);
                var entry = entries.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.ResultData));
                if (entry == null)
                    return null;

                var data = JsonConvert.DeserializeObject<NalogResponse>(entry.ResultData)
                           ?? JsonConvert.DeserializeObject<NalogResponse>(
                               $"{{\"row\":{entry.ResultData}}}");

                if (data?.Row != null)
                    await _cache.SetAsync($"{CacheKeyPrefix}{unp}", data, CacheExpiry);

                _logger.LogWarning("API вернул 404 для УНП {Unp}, но в истории есть данные — возвращен результат из истории", unp);
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка при извлечении данных из истории для УНП {Unp}", unp);
                return null;
            }
        }

        private async Task EnsureSuccessWithStatusAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
                return;

            string responseBody = string.Empty;
            try
            {
                responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch
            {
                // тело ответа недоступно — продолжаем с пустой строкой
            }

            if (response.StatusCode == HttpStatusCode.BadRequest && IsUnpNotFoundError(responseBody))
            {
                _logger.LogWarning(
                    "HTTP 400 при запросе {Url}: УНП не найден в базе. Оригинальная ошибка: {ResponseBody}",
                    response.RequestMessage?.RequestUri, responseBody);
                throw new NalogApiException(HttpStatusCode.BadRequest, UnpNotFoundMessage, responseBody);
            }

            string message = response.StatusCode switch
            {
                HttpStatusCode.BadRequest => $"Невалидный запрос к API (400): {responseBody}",
                HttpStatusCode.NotFound => "Контрагент не найден (404)",
                (HttpStatusCode)429 => "Превышен лимит запросов к API (429). Попробуйте позже.",
                HttpStatusCode.InternalServerError => "Ошибка на сервере API (500)",
                _ => $"Ошибка HTTP {(int)response.StatusCode}"
            };

            _logger.LogError("HTTP {StatusCode} при запросе {Url}: {Message}",
                (int)response.StatusCode, response.RequestMessage?.RequestUri, message);

            throw new NalogApiException(response.StatusCode, message, responseBody);
        }
    }
}
