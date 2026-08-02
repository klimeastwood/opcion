using System;
using System.Net.Http;
using System.Text.RegularExpressions;
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
        private readonly HttpClient _httpClient;
        private readonly ILogger<NalogApiService> _logger;
        private readonly ApiSettings _settings;

        public NalogApiService(HttpClient httpClient, IOptions<ApiSettings> settings, ILogger<NalogApiService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
        }

        // Backward-compatible shim для существующих окон (new NalogApiService()); удалить после Шага 3.
        public NalogApiService()
            : this(new HttpClient { Timeout = TimeSpan.FromSeconds(30) },
                   Options.Create(new ApiSettings { BaseUrl = "http://grp.nalog.gov.by/api/grp-public/data" }),
                   Microsoft.Extensions.Logging.Abstractions.NullLogger<NalogApiService>.Instance)
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "WPF-Client");
        }

        // Обратная совместимость со старыми методами (без Async). Удалить после Шага 3.
        public Task<CompanyRow> SearchLegalEntity(string unp, string name = null, bool useWildcard = false)
        {
            return GetCompanyInfo(unp);
        }

        public Task<CompanyRow> GetCompanyInfo(string unp)
        {
            return GetCompanyInfoAsync(unp).ContinueWith(t => t.Result?.Row);
        }

        public Task<CompanyRow> SearchPhysicalPerson(PhysicalSearchParams searchParams)
        {
            return SearchPhysicalPersonAsync(searchParams).ContinueWith(t => t.Result?.Row);
        }

        public async Task<NalogResponse?> SearchLegalEntityAsync(LegalSearchParams parameters, CancellationToken cancellationToken = default)
        {
            try
            {
                string url = $"{_settings.BaseUrl}?unp={parameters.Unp}&charset=UTF-8&type=json";

                if (!string.IsNullOrEmpty(parameters.Name) && parameters.UseWildcard)
                {
                    url += $"&name={Uri.EscapeDataString(parameters.Name)}";
                }

                _logger.LogInformation($"Запрос к API: {url}");
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                var data = JsonConvert.DeserializeObject<NalogResponse>(jsonResponse);

                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при запросе к API");
                throw new Exception($"Ошибка при поиске юрлица: {ex.Message}", ex);
            }
        }

        public async Task<NalogResponse?> SearchPhysicalPersonAsync(PhysicalSearchParams searchParams, CancellationToken cancellationToken = default)
        {
            try
            {
                var url = $"{_settings.BaseUrl}?charset=UTF-8&type=json";

                if (!string.IsNullOrEmpty(searchParams.Unp))
                    url += $"&unp={searchParams.Unp}";

                if (!string.IsNullOrEmpty(searchParams.Fio))
                    url += $"&fio={Uri.EscapeDataString(searchParams.Fio)}";

                if (!string.IsNullOrEmpty(searchParams.Passport))
                    url += $"&passport={Uri.EscapeDataString(searchParams.Passport)}";

                if (!string.IsNullOrEmpty(searchParams.IdNumber))
                    url += $"&id_number={Uri.EscapeDataString(searchParams.IdNumber)}";

                // Для частичного поиска
                if (searchParams.UseWildcard)
                {
                    url += "&wildcard=true";
                }

                _logger.LogInformation($"Запрос к API: {url}");
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                var data = JsonConvert.DeserializeObject<NalogResponse>(jsonResponse);

                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при запросе к API");
                throw new Exception($"Ошибка при поиске физлица: {ex.Message}", ex);
            }
        }

        public async Task<NalogResponse?> GetCompanyInfoAsync(string unp, CancellationToken cancellationToken = default)
        {
            try
            {
                string url = $"{_settings.BaseUrl}?unp={unp}&charset=UTF-8&type=json";

                _logger.LogInformation($"Запрос к API: {url}");
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                var data = JsonConvert.DeserializeObject<NalogResponse>(jsonResponse);

                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при запросе к API");
                throw new Exception($"Ошибка при получении информации: {ex.Message}", ex);
            }
        }

        // Преобразование wildcard паттерна в регулярное выражение
        public string WildcardToRegex(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return pattern;

            // Экранируем спецсимволы regex
            string regex = Regex.Escape(pattern);
            // Заменяем * на .* (любое количество символов)
            regex = regex.Replace("\\*", ".*");
            // Заменяем ? на . (один любой символ)
            regex = regex.Replace("\\?", ".");
            // Добавляем начало и конец строки
            return $"^{regex}$";
        }
    }
}
