using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NalogParserWPF.Helpers;
using NalogParserWPF.Models;

namespace NalogParserWPF.Services
{
    /// <summary>
    /// Клиент публичного API Белстата (SDMX-JSON).
    /// Справочники (рубрики, каталог показателей) кэшируются на час, значения — на 5 минут.
    /// Ошибочные ответы API (400/404/500) и сетевые сбои возвращают null/пустой список,
    /// отмена пользователем (<see cref="OperationCanceledException"/>) пробрасывается наружу.
    /// </summary>
    public class BelstatApiService : IBelstatApiService
    {
        private const string ValuesFormat = "SDMX-JSON";
        private const string UnspecifiedIndex = "-1";
        private const string IndicatorDimensionId = "indicator";
        private const string IndicatorDimensionName = "Показатель";
        private const string ValueDimensionId = "value_index";
        private const string ValuesFileSuffix = "_sdmx_values.json";

        /// <summary>Справочник структуры данных показателя: единственный источник русских подписей измерений и значений.</summary>
        private const string DataStructureUrlPrefix = "sdmx-api/indicator/datastructure/SDMX-JSON/";

        /// <summary>Префикс идентификаторов классификаторов значений измерений, например CL_razrez_594.</summary>
        private const string CodelistIdPrefix = "CL_";

        /// <summary>Сколько справочников показателей подгружать для отчёта по рубрике (ответ рубрики может содержать десятки показателей).</summary>
        private const int MaxRubricLocalizationRequests = 3;

        private const string RubricTreeCacheKey = "belstat:rubricTree";
        private const string RubricIndicatorsCacheKeyPrefix = "belstat:rubricIndicators:";
        private const string CatalogCacheKey = "belstat:catalog";
        private const string ValuesCacheKeyPrefix = "belstat:values:";
        private const string LocalizationCacheKey = "belstat:localization";

        private const int MaxSearchResults = 200;
        private const int MaxYears = 25;
        private const int MinYear = 1990;

        private static readonly TimeSpan MetadataCacheExpiry = TimeSpan.FromHours(1);
        private static readonly TimeSpan ValuesCacheExpiry = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan LocalizationCacheExpiry = TimeSpan.FromHours(6);

        private readonly HttpClient _httpClient;
        private readonly ICacheService _cache;
        private readonly ILogger<BelstatApiService> _logger;

        public BelstatApiService(HttpClient httpClient, ICacheService cache, ILogger<BelstatApiService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<BelstatRubric>> GetRubricTreeAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGet<List<BelstatRubric>>(RubricTreeCacheKey, out var cached) && cached is { Count: > 0 })
                return cached;

            _logger.LogInformation("Запрос к API Белстата: дерево рубрик");

            var response = await GetJsonAsync<BelstatRubricTreeResponse>("rubric/rubricTreeNodeGet/0", cancellationToken).ConfigureAwait(false);
            var rubrics = FlattenRubrics(response?.Item);

            if (rubrics.Count == 0)
            {
                _logger.LogWarning("API Белстата не вернул дерево рубрик");
                return rubrics;
            }

            _cache.Set(RubricTreeCacheKey, rubrics, MetadataCacheExpiry);
            return rubrics;
        }

        public async Task<List<BelstatIndicator>> GetIndicatorsByRubricAsync(string rubricCode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rubricCode))
                return new List<BelstatIndicator>();

            var cacheKey = RubricIndicatorsCacheKeyPrefix + rubricCode;
            if (_cache.TryGet<List<BelstatIndicator>>(cacheKey, out var cached) && cached is { Count: > 0 })
                return cached;

            _logger.LogInformation("Запрос к API Белстата: показатели рубрики {Rubric}", rubricCode);

            var response = await GetJsonAsync<BelstatItemsResponse>(
                $"rubric/rubricIndicatorsGet/{Uri.EscapeDataString(rubricCode)}", cancellationToken).ConfigureAwait(false);

            var indicators = MapIndicators(response?.Items, rubricCode);
            if (indicators.Count > 0)
                _cache.Set(cacheKey, indicators, MetadataCacheExpiry);

            return indicators;
        }

        public async Task<List<BelstatIndicator>> SearchIndicatorsAsync(string query, CancellationToken cancellationToken = default)
        {
            var catalog = await GetCatalogAsync(cancellationToken).ConfigureAwait(false);
            if (catalog.Count == 0)
                return new List<BelstatIndicator>();

            var needle = (query ?? string.Empty).Trim();
            var matched = string.IsNullOrEmpty(needle)
                ? catalog
                : catalog.Where(i =>
                    Contains(i.Name, needle) ||
                    Contains(i.Code, needle) ||
                    Contains(i.KeyWords, needle)).ToList();

            if (matched.Count > MaxSearchResults)
                matched = matched.Take(MaxSearchResults).ToList();

            return matched;
        }


        public async Task<BelstatDataResponse?> GetIndicatorDataAsync(string indicatorCode, int[] years, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(indicatorCode))
            {
                _logger.LogWarning("Не указан код показателя Белстата");
                return null;
            }

            var period = NormalizeYears(years);
            if (period.Count == 0)
            {
                _logger.LogWarning("Не указаны годы запроса значений показателя {Code}", indicatorCode);
                return null;
            }

            var cacheKey = $"{ValuesCacheKeyPrefix}indicator:{indicatorCode}:{string.Join(",", period)}";
            if (_cache.TryGet<BelstatDataResponse>(cacheKey, out var cached) && cached != null)
                return cached;

            _logger.LogInformation("Запрос к API Белстата: значения показателя {Code} за {Years}", indicatorCode, string.Join(",", period));

            var url = $"sdmx-api/indicator/values/{ValuesFormat}/{Uri.EscapeDataString(indicatorCode)}";

            // Значения (подписи на английском) и справочник структуры данных (русские подписи)
            // запрашиваются параллельно: справочник нужен только для локализации.
            var documentTask = PostJsonAsync<BelstatDataResponse>(url, new { years = period }, cancellationToken);
            var localizationTask = EnsureIndicatorLocalizationAsync(indicatorCode, cancellationToken);

            var document = await documentTask.ConfigureAwait(false);
            if (document == null)
                return null;

            await localizationTask.ConfigureAwait(false);
            ApplyLocalization(document);

            var response = BuildCanonicalResponse(
                new List<(string Label, BelstatDataResponse Document)> { (indicatorCode, document) },
                period.ToArray(),
                withIndicatorColumn: false);

            if (response == null)
            {
                _logger.LogWarning("Показатель {Code} не содержит наблюдений за {Years}", indicatorCode, string.Join(",", period));
                return null;
            }

            _cache.Set(cacheKey, response, ValuesCacheExpiry);
            return response;
        }

        public async Task<BelstatDataResponse?> GetRubricDataAsync(string rubricCode, int[] years, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rubricCode))
            {
                _logger.LogWarning("Не указан код рубрики Белстата");
                return null;
            }

            var period = NormalizeYears(years);
            if (period.Count == 0)
            {
                _logger.LogWarning("Не указаны годы запроса значений рубрики {Code}", rubricCode);
                return null;
            }

            var cacheKey = $"{ValuesCacheKeyPrefix}rubric:{rubricCode}:{string.Join(",", period)}";
            if (_cache.TryGet<BelstatDataResponse>(cacheKey, out var cached) && cached != null)
                return cached;

            _logger.LogInformation("Запрос к API Белстата: значения показателей рубрики {Code} за {Years}", rubricCode, string.Join(",", period));

            var url = $"sdmx-api/rubric/indicators/values/{ValuesFormat}/{Uri.EscapeDataString(rubricCode)}";
            var bytes = await PostRawAsync(url, new { years = period }, cancellationToken).ConfigureAwait(false);
            if (bytes == null || bytes.Length == 0)
                return null;

            var documents = ExtractDocuments(bytes, rubricCode);
            if (documents.Count == 0)
            {
                _logger.LogWarning("Рубрика {Code} не содержит разобранных наборов данных", rubricCode);
                return null;
            }

            // Русские названия измерений берём из справочников показателей рубрики
            // (для первых нескольких показателей: ответ рубрики может содержать десятки наборов данных).
            foreach (var code in ExtractIndicatorCodes(bytes, documents).Take(MaxRubricLocalizationRequests))
                await EnsureIndicatorLocalizationAsync(code, cancellationToken).ConfigureAwait(false);

            foreach (var (_, document) in documents)
                ApplyLocalization(document);

            var response = BuildCanonicalResponse(documents, period.ToArray(), withIndicatorColumn: true);
            if (response == null)
            {
                _logger.LogWarning("Рубрика {Code} не содержит наблюдений за {Years}", rubricCode, string.Join(",", period));
                return null;
            }

            _cache.Set(cacheKey, response, ValuesCacheExpiry);
            return response;
        }

        /// <summary>Каталог показателей целиком (POST indicator/indicatorSearch с пустым телом), кэш — 1 час.</summary>
        private async Task<List<BelstatIndicator>> GetCatalogAsync(CancellationToken cancellationToken)
        {
            if (_cache.TryGet<List<BelstatIndicator>>(CatalogCacheKey, out var cached) && cached is { Count: > 0 })
                return cached;

            _logger.LogInformation("Запрос к API Белстата: каталог показателей");

            var response = await PostJsonAsync<BelstatItemsResponse>("indicator/indicatorSearch", new { }, cancellationToken).ConfigureAwait(false);
            var catalog = MapIndicators(response?.Items, null);

            if (catalog.Count > 0)
                _cache.Set(CatalogCacheKey, catalog, MetadataCacheExpiry);

            return catalog;
        }

        private static List<BelstatIndicator> MapIndicators(IEnumerable<BelstatIndicatorItem>? items, string? rubricCode)
        {
            var result = new List<BelstatIndicator>();

            foreach (var item in items ?? Enumerable.Empty<BelstatIndicatorItem>())
            {
                if (string.IsNullOrWhiteSpace(item.Code))
                    continue;

                result.Add(new BelstatIndicator
                {
                    Code = item.Code,
                    Name = string.IsNullOrWhiteSpace(item.Name) ? item.Code : item.Name,
                    RubricCode = rubricCode ?? string.Empty,
                    KeyWords = item.KeyWords
                });
            }

            return result;
        }

        private async Task<T?> GetJsonAsync<T>(string relativeUrl, CancellationToken cancellationToken) where T : class
        {
            try
            {
                using var response = await _httpClient.GetAsync(relativeUrl, cancellationToken).ConfigureAwait(false);
                return await ReadJsonResponseAsync<T>(response, "GET", relativeUrl, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError("Таймаут запроса к API Белстата: GET {Url}", relativeUrl);
                return null;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Сетевая ошибка при запросе к API Белстата: GET {Url}", relativeUrl);
                return null;
            }
        }

        private async Task<T?> PostJsonAsync<T>(string relativeUrl, object? payload, CancellationToken cancellationToken) where T : class
        {
            try
            {
                using var content = CreateJsonContent(payload);
                using var response = await _httpClient.PostAsync(relativeUrl, content, cancellationToken).ConfigureAwait(false);
                return await ReadJsonResponseAsync<T>(response, "POST", relativeUrl, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError("Таймаут запроса к API Белстата: POST {Url}", relativeUrl);
                return null;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Сетевая ошибка при запросе к API Белстата: POST {Url}", relativeUrl);
                return null;
            }
        }

        /// <summary>POST-запрос, возвращающий тело ответа «как есть» (JSON или ZIP-архив рубрики).</summary>
        private async Task<byte[]?> PostRawAsync(string relativeUrl, object? payload, CancellationToken cancellationToken)
        {
            try
            {
                using var content = CreateJsonContent(payload);
                using var response = await _httpClient.PostAsync(relativeUrl, content, cancellationToken).ConfigureAwait(false);

                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("API Белстата вернул {Status} на POST {Url}: {Body}",
                        (int)response.StatusCode, relativeUrl, Truncate(Decode(bytes), 400));
                    return null;
                }

                if (bytes.Length == 0)
                {
                    _logger.LogWarning("Пустой ответ API Белстата: POST {Url}", relativeUrl);
                    return null;
                }

                return bytes;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError("Таймаут запроса к API Белстата: POST {Url}", relativeUrl);
                return null;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Сетевая ошибка при запросе к API Белстата: POST {Url}", relativeUrl);
                return null;
            }
        }

        private async Task<T?> ReadJsonResponseAsync<T>(HttpResponseMessage response, string method, string url, CancellationToken cancellationToken) where T : class
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("API Белстата вернул {Status} на {Method} {Url}: {Body}",
                    (int)response.StatusCode, method, url, Truncate(content, 400));
                return null;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Пустой ответ API Белстата: {Method} {Url}", method, url);
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(content);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Не удалось разобрать JSON от API Белстата: {Method} {Url}", method, url);
                return null;
            }
        }

        private static StringContent CreateJsonContent(object? payload)
        {
            var json = payload == null ? "{}" : JsonConvert.SerializeObject(payload);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        /// <summary>
        /// Разбирает тело ответа рубрики: ZIP с файлами «{code}_sdmx_values.json» либо одиночный SDMX-JSON.
        /// Подписи документов обогащаются названиями показателей из кэшированного каталога.
        /// </summary>
        private List<(string Label, BelstatDataResponse Document)> ExtractDocuments(byte[] bytes, string rubricCode)
        {
            var result = new List<(string Label, BelstatDataResponse Document)>();
            var names = GetKnownIndicatorNames();

            if (!IsZipArchive(bytes))
            {
                var single = DeserializeDocument(bytes, rubricCode);
                if (single != null)
                    result.Add((BuildLabel(rubricCode, names), single));
                return result;
            }

            try
            {
                using var stream = new MemoryStream(bytes, writable: false);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

                foreach (var entry in archive.Entries)
                {
                    if (entry.Length == 0 || string.IsNullOrEmpty(entry.Name))
                        continue;

                    if (!entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug("Элемент архива рубрики {Rubric} пропущен: {Entry}", rubricCode, entry.FullName);
                        continue;
                    }

                    if (entry.Name.Contains("datastructure", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var code = ExtractIndicatorCode(entry.Name);
                    using var entryStream = entry.Open();
                    using var buffer = new MemoryStream();
                    entryStream.CopyTo(buffer);

                    var document = DeserializeDocument(buffer.ToArray(), $"{rubricCode}/{entry.FullName}");
                    if (document == null)
                        continue;

                    result.Add((BuildLabel(code, names), document));
                }
            }
            catch (InvalidDataException ex)
            {
                _logger.LogError(ex, "Не удалось распаковать архив ответа рубрики {Rubric}", rubricCode);
            }

            return result;
        }

        private BelstatDataResponse? DeserializeDocument(byte[] bytes, string source)
        {
            try
            {
                var json = Decode(bytes);
                return string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<BelstatDataResponse>(json);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Не удалось разобрать SDMX-JSON из {Source}", source);
                return null;
            }
        }

        private Dictionary<string, string> GetKnownIndicatorNames()
        {
            if (!_cache.TryGet<List<BelstatIndicator>>(CatalogCacheKey, out var catalog) || catalog == null)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var indicator in catalog)
            {
                if (!string.IsNullOrWhiteSpace(indicator.Code))
                    names[indicator.Code] = indicator.Name;
            }

            return names;
        }

        private static string ExtractIndicatorCode(string entryName)
        {
            var name = entryName;
            var slash = name.LastIndexOf('/');
            if (slash >= 0)
                name = name[(slash + 1)..];

            if (name.EndsWith(ValuesFileSuffix, StringComparison.OrdinalIgnoreCase))
                name = name[..^ValuesFileSuffix.Length];
            else if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                name = name[..^".json".Length];

            return name;
        }

        private static string BuildLabel(string code, Dictionary<string, string> names) =>
            names.TryGetValue(code, out var name) && !string.IsNullOrWhiteSpace(name) && !string.Equals(name, code, StringComparison.Ordinal)
                ? $"{code} — {name}"
                : code;

        private static bool IsZipArchive(byte[] bytes) =>
            bytes.Length >= 4 &&
            bytes[0] == 0x50 && bytes[1] == 0x4B &&
            (bytes[2] == 0x03 || bytes[2] == 0x05 || bytes[2] == 0x07);

        private static string Decode(byte[] bytes)
        {
            if (bytes.Length == 0)
                return string.Empty;

            // Отсекаем BOM, если он есть: ответы Белстата отдаются в UTF-8.
            var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            return Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
        }


        /// <summary>
        /// Разворачивает дерево рубрик в плоский список (дети корня) с уровнями вложенности
        /// и иерархическими номерами («1», «1.2», «1.2.3»): номер показывается в ComboBox
        /// и помогает ориентироваться в многоуровневом каталоге.
        /// </summary>
        private static List<BelstatRubric> FlattenRubrics(BelstatRubric? root)
        {
            var result = new List<BelstatRubric>();
            if (root?.Children == null)
                return result;

            for (var index = 0; index < root.Children.Count; index++)
            {
                var number = (index + 1).ToString(CultureInfo.InvariantCulture);
                AppendRubric(root.Children[index], root.Code, 0, number, result);
            }

            return result;
        }

        private static void AppendRubric(BelstatRubric rubric, string? parentCode, int depth, string number,
            List<BelstatRubric> target)
        {
            rubric.ParentCode = parentCode;
            rubric.Depth = depth;
            rubric.Number = number;
            target.Add(rubric);

            var children = rubric.Children ?? new List<BelstatRubric>();
            for (var index = 0; index < children.Count; index++)
                AppendRubric(children[index], rubric.Code, depth + 1, $"{number}.{index + 1}", target);
        }

        /// <summary>Годы запроса: уникальные, по убыванию, в допустимых границах (не старше 1990, максимум 25 лет).</summary>
        private List<int> NormalizeYears(int[]? years)
        {
            var result = new List<int>();
            if (years == null)
                return result;

            var upperBound = DateTime.Now.Year + 1;

            foreach (var year in years.Where(y => y >= MinYear && y <= upperBound).Distinct().OrderByDescending(y => y))
            {
                if (result.Count >= MaxYears)
                {
                    _logger.LogWarning("Количество запрашиваемых лет ограничено значением {MaxYears}", MaxYears);
                    break;
                }

                result.Add(year);
            }

            return result;
        }

        private static bool Contains(string? source, string needle) =>
            !string.IsNullOrWhiteSpace(source) && source.Contains(needle, StringComparison.OrdinalIgnoreCase);

        private static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Length <= maxLength ? value : value[..maxLength] + "...";
        }

        // =====================================================================================
        // Локализация подписей. Эндпоинт значений Белстата всегда отдаёт подписи измерений и их
        // значений на английском, поэтому русские названия берутся из справочника структуры данных
        // (GET sdmx-api/indicator/datastructure/SDMX-JSON/{code}):
        //   • названия измерений — из схемы понятий (conceptSchemes → concepts → names.ru);
        //   • расшифровки значений — из классификаторов (codelists → codes → names.ru),
        //     на которые ссылается измерение (localRepresentation.enumeration, например CL_razrez_594).
        // Если справочник недоступен, применяется резервный словарь BelstatLocalizationHelper.
        // =====================================================================================

        /// <summary>
        /// Накопительный словарь русских подписей, собранный из ответов справочника структуры данных
        /// разных показателей. Хранится в кэше <see cref="LocalizationCacheExpiry"/> (6 часов).
        /// </summary>
        private sealed class BelstatLocalization
        {
            /// <summary>Идентификатор понятия → русское название измерения (например, razrez_594).</summary>
            public Dictionary<string, string> Concepts { get; } = new(StringComparer.OrdinalIgnoreCase);

            /// <summary>Идентификатор классификатора → (идентификатор кода → русская расшифровка).</summary>
            public Dictionary<string, Dictionary<string, string>> Codelists { get; } = new(StringComparer.OrdinalIgnoreCase);

            /// <summary>Идентификатор измерения → идентификатор понятия (из справочника структуры).</summary>
            public Dictionary<string, string> DimensionConcepts { get; } = new(StringComparer.OrdinalIgnoreCase);

            /// <summary>Идентификатор измерения → идентификатор классификатора значений.</summary>
            public Dictionary<string, string> DimensionCodelists { get; } = new(StringComparer.OrdinalIgnoreCase);

            /// <summary>Коды показателей, для которых справочник уже запрашивался (повторно не запрашиваем).</summary>
            public HashSet<string> RequestedIndicators { get; } = new(StringComparer.OrdinalIgnoreCase);

            /// <summary>Объект-замок: словари заполняются асинхронными запросами и читаются при построении отчёта.</summary>
            public object SyncRoot { get; } = new();
        }

        /// <summary>Накопительный словарь русских подписей: берётся из кэша либо создаётся пустым.</summary>
        private BelstatLocalization GetLocalization()
        {
            if (_cache.TryGet<BelstatLocalization>(LocalizationCacheKey, out var cached) && cached != null)
                return cached;

            var localization = new BelstatLocalization();
            _cache.Set(LocalizationCacheKey, localization, LocalizationCacheExpiry);
            return localization;
        }

        /// <summary>
        /// Догружает справочник структуры данных показателя (не более одного раза на показатель)
        /// и пополняет русские названия измерений и значений. Справочник нужен только для локализации,
        /// поэтому его недоступность не прерывает запрос значений: в худшем случае подписи останутся
        /// английскими либо будут заменены резервным словарём <see cref="BelstatLocalizationHelper"/>.
        /// </summary>
        private async Task EnsureIndicatorLocalizationAsync(string indicatorCode, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(indicatorCode))
                return;

            var localization = GetLocalization();

            lock (localization.SyncRoot)
            {
                if (!localization.RequestedIndicators.Add(indicatorCode))
                    return;
            }

            try
            {
                var reference = await GetJsonAsync<BelstatStructureReferenceResponse>(
                    $"{DataStructureUrlPrefix}{Uri.EscapeDataString(indicatorCode)}", cancellationToken).ConfigureAwait(false);

                if (reference == null)
                {
                    _logger.LogWarning(
                        "Справочник структуры показателя {Code} недоступен: русские подписи берутся из резервного словаря",
                        indicatorCode);
                    return;
                }

                int concepts, codelists;
                lock (localization.SyncRoot)
                {
                    MergeLocalization(localization, reference);
                    concepts = localization.Concepts.Count;
                    codelists = localization.Codelists.Count;
                }

                _logger.LogInformation(
                    "Справочник показателя {Code} обработан: понятий всего {Concepts}, классификаторов всего {Codelists}",
                    indicatorCode, concepts, codelists);
            }
            catch (OperationCanceledException)
            {
                // Отмена пользователем: справочник не критичен, об отмене сообщит основной запрос значений.
            }
        }

        /// <summary>
        /// Переносит русские подписи из ответа справочника структуры данных в накопительный словарь:
        /// схемы понятий дают названия измерений, классификаторы — расшифровки значений.
        /// </summary>
        private static void MergeLocalization(BelstatLocalization localization, BelstatStructureReferenceResponse reference)
        {
            var data = reference.Data;
            if (data == null)
                return;

            foreach (var scheme in data.ConceptSchemes ?? new List<BelstatConceptScheme>())
            {
                foreach (var concept in scheme.Concepts ?? new List<BelstatConcept>())
                {
                    var name = TryGetLocalizedName(concept.Names);
                    var conceptId = GetLocalId(concept.Id);
                    if (name != null && conceptId.Length > 0)
                        localization.Concepts[conceptId] = name;
                }
            }

            foreach (var codelist in data.Codelists ?? new List<BelstatReferenceCodelist>())
            {
                var codelistId = GetLocalId(codelist.Id);
                if (codelistId.Length == 0)
                    continue;

                if (!localization.Codelists.TryGetValue(codelistId, out var codes))
                {
                    codes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    localization.Codelists[codelistId] = codes;
                }

                foreach (var code in codelist.Codes ?? new List<BelstatReferenceCode>())
                {
                    var name = TryGetLocalizedName(code.Names);
                    var codeId = code.Id.Trim();
                    if (name != null && codeId.Length > 0)
                        codes[codeId] = name;
                }
            }

            foreach (var structure in data.DataStructures ?? new List<BelstatDataStructureReference>())
            {
                var dimensions = structure.DataStructureComponents?.DimensionList?.Dimensions;
                if (dimensions == null)
                    continue;

                foreach (var dimension in dimensions)
                {
                    var dimensionId = dimension.Id.Trim();
                    if (dimensionId.Length == 0)
                        continue;

                    var conceptId = ExtractReferenceId(dimension.ConceptIdentity);
                    if (!string.IsNullOrWhiteSpace(conceptId))
                        localization.DimensionConcepts[dimensionId] = conceptId!;

                    var codelistId = ExtractReferenceId(dimension.LocalRepresentation?.Enumeration);
                    if (!string.IsNullOrWhiteSpace(codelistId))
                        localization.DimensionCodelists[dimensionId] = codelistId!;
                }
            }
        }

        /// <summary>Русское название из набора локализованных имён (при отсутствии русского берётся английское).</summary>
        private static string? TryGetLocalizedName(BelstatLocalizedNames? names)
        {
            var name = names?.Preferred?.Trim();
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        /// <summary>
        /// Локальный идентификатор из ссылки-URN справочника SDMX:
        /// urn:sdmx:org.sdmx.infomodel.codelist.Codelist=BELSTAT:CL_razrez_594(1.0) → CL_razrez_594;
        /// urn:sdmx:org.sdmx.infomodel.conceptscheme.Concept=BELSTAT:CUBE(1.0).razrez_594 → razrez_594.
        /// Обычный идентификатор (razrez_594, CL_razrez_594) возвращается без изменений.
        /// </summary>
        private static string? ExtractReferenceId(string? reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                return null;

            var value = reference.Trim();

            // Отсекаем префикс вида urn:sdmx:...=BELSTAT: — остаётся локальная часть идентификатора.
            var colon = value.LastIndexOf(':');
            if (colon >= 0 && colon < value.Length - 1)
                value = value[(colon + 1)..];

            // Отсекаем версию справочника: CONCEPTS(1.0) → CONCEPTS.
            var version = value.LastIndexOf('(');
            if (version > 0 && value.EndsWith(")", StringComparison.Ordinal))
                value = value[..version];

            // Для понятий локальная часть отделяется точкой: CONCEPTS.razrez_594 → razrez_594.
            var dot = value.LastIndexOf('.');
            if (dot >= 0 && dot < value.Length - 1)
                value = value[(dot + 1)..];

            return value.Length == 0 ? null : value;
        }

        /// <summary>Локальный идентификатор понятия или классификатора (из URN либо из обычного идентификатора).</summary>
        private static string GetLocalId(string? id)
        {
            var local = ExtractReferenceId(id);
            return local == null ? string.Empty : local.Trim();
        }

        /// <summary>
        /// Заменяет английские подписи измерений и их значений на русские: сначала по накопительному
        /// словарю справочника структуры данных, затем — по резервному словарю
        /// <see cref="BelstatLocalizationHelper"/>. Незнакомые подписи остаются без изменений.
        /// </summary>
        private void ApplyLocalization(BelstatDataResponse? document)
        {
            if (document?.Structure?.Dimensions == null)
                return;

            var localization = GetLocalization();

            lock (localization.SyncRoot)
            {
                foreach (var dimension in EnumerateDimensions(document))
                    ApplyLocalizationToDimension(localization, dimension);
            }
        }

        /// <summary>Все измерения документа: наборов данных, серийные и наблюдений.</summary>
        private static IEnumerable<BelstatDimension> EnumerateDimensions(BelstatDataResponse document)
        {
            var sets = document.Structure?.Dimensions;
            if (sets == null)
                yield break;

            foreach (var dimension in sets.Dataset)
                yield return dimension;
            foreach (var dimension in sets.Series)
                yield return dimension;
            foreach (var dimension in sets.Observation)
                yield return dimension;
        }

        private static void ApplyLocalizationToDimension(BelstatLocalization localization, BelstatDimension dimension)
        {
            var dimensionId = dimension.Id.Trim();

            // Название измерения: понятие справочника (names.ru схемы понятий) → резервный словарь.
            if (localization.DimensionConcepts.TryGetValue(dimensionId, out var conceptId) &&
                localization.Concepts.TryGetValue(conceptId, out var conceptName) &&
                !string.IsNullOrWhiteSpace(conceptName))
            {
                dimension.Name = conceptName;
            }
            else
            {
                dimension.Name = BelstatLocalizationHelper.TranslateDimensionName(dimension.Name);
            }

            // Классификатор значений измерения: ссылка из справочника, при её отсутствии —
            // классификатор с тем же именем, что и измерение (CL_<измерение>), если он уже загружен.
            if (!localization.DimensionCodelists.TryGetValue(dimensionId, out var codelistId) ||
                !localization.Codelists.ContainsKey(codelistId))
            {
                codelistId = CodelistIdPrefix + dimensionId;
            }

            var codelist = localization.Codelists.TryGetValue(codelistId, out var codes) ? codes : null;

            foreach (var value in dimension.Values)
            {
                if (codelist != null && codelist.TryGetValue(value.Id, out var localizedName) &&
                    !string.IsNullOrWhiteSpace(localizedName))
                {
                    value.Name = localizedName;
                }
                else
                {
                    value.Name = BelstatLocalizationHelper.TranslateValueName(value.Name);
                }
            }
        }

        /// <summary>
        /// Коды показателей, вошедших в ответ рубрики: берутся из подписей документов («{код} — {название}»).
        /// Для одиночного SDMX-JSON код показателя неизвестен — локализация выполняется резервным словарём.
        /// </summary>
        private static IEnumerable<string> ExtractIndicatorCodes(
            byte[] bytes, IReadOnlyList<(string Label, BelstatDataResponse Document)> documents)
        {
            if (!IsZipArchive(bytes))
                yield break;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (label, _) in documents)
            {
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                var separator = label.IndexOf("—", StringComparison.Ordinal);
                var code = (separator > 0 ? label[..separator] : label).Trim();
                if (code.Length > 0 && seen.Add(code))
                    yield return code;
            }
        }

        private sealed class ParsedDocument
        {
            public string Label { get; set; } = string.Empty;
            public List<BelstatDimension> Dimensions { get; } = new();
            public List<ParsedObservation> Observations { get; } = new();
        }

        private sealed class ParsedObservation
        {
            public string[] Parts { get; set; } = Array.Empty<string>();
            public string[] Values { get; set; } = Array.Empty<string>();
        }

        /// <summary>
        /// Приводит один или несколько SDMX-JSON-ответов к единому «плоскому» виду:
        /// измерения наблюдений объединяются по идентификатору, а наблюдения переиндексируются.
        /// Измерения и хвостовые части ключа, которые нигде не определены (значение «-1»),
        /// в результирующую таблицу не попадают.
        /// </summary>
        private static BelstatDataResponse? BuildCanonicalResponse(
            IReadOnlyList<(string Label, BelstatDataResponse Document)> documents,
            int[]? years,
            bool withIndicatorColumn)
        {
            var parsed = ParseDocuments(documents);
            if (parsed.Count == 0 || parsed.TrueForAll(d => d.Observations.Count == 0))
                return null;

            var unionDimensions = new List<BelstatDimension>();
            var unionPositions = new Dictionary<string, int>(StringComparer.Ordinal);
            var unionValuePositions = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

            foreach (var document in parsed)
            {
                foreach (var dimension in document.Dimensions)
                    MergeDimension(dimension, unionDimensions, unionPositions, unionValuePositions);
            }

            // Какие объявленные измерения действительно присутствуют в данных.
            var declaredKeep = new bool[unionDimensions.Count];
            foreach (var document in parsed)
            {
                for (var i = 0; i < document.Dimensions.Count; i++)
                {
                    var unionPosition = unionPositions[document.Dimensions[i].Id];
                    if (declaredKeep[unionPosition])
                        continue;

                    foreach (var observation in document.Observations)
                    {
                        if (i < observation.Parts.Length &&
                            IsDefinedIndex(observation.Parts[i], document.Dimensions[i].Values.Count))
                        {
                            declaredKeep[unionPosition] = true;
                            break;
                        }
                    }
                }
            }

            // Хвостовые части ключа, которым не соответствует объявленное измерение
            // (в ответах Белстата это чаще всего подавленное измерение периода).
            var extraCount = parsed.Max(MaxExtraParts);
            var extraDimensions = new Dictionary<int, BelstatDimension>();
            var extraValuePositions = new Dictionary<int, Dictionary<string, int>>();

            for (var k = 0; k < extraCount; k++)
            {
                var dimension = new BelstatDimension
                {
                    Id = $"extra_{k}",
                    Name = extraCount == 1 ? "Период" : $"Период {k + 1}"
                };
                var valuePositions = new Dictionary<string, int>(StringComparer.Ordinal);

                foreach (var document in parsed)
                {
                    foreach (var observation in document.Observations)
                    {
                        var partIndex = document.Dimensions.Count + k;
                        if (partIndex >= observation.Parts.Length)
                            continue;

                        var raw = observation.Parts[partIndex];
                        if (valuePositions.ContainsKey(raw))
                            continue;

                        valuePositions[raw] = dimension.Values.Count;
                        dimension.Values.Add(new BelstatDimensionValue
                        {
                            Id = raw,
                            Name = string.Equals(raw, UnspecifiedIndex, StringComparison.Ordinal) ? "не указан" : raw
                        });
                    }
                }

                if (dimension.Values.Count == 0)
                    continue;

                if (dimension.Values.Count == 1 && string.Equals(dimension.Values[0].Id, UnspecifiedIndex, StringComparison.Ordinal))
                    continue;

                extraDimensions[k] = dimension;
                extraValuePositions[k] = valuePositions;
            }

            // Если в одном наблюдении несколько значений, добавляем измерение «Значение»
            // (при совпадении количества значений с запрошенными годами — подписываем годами).
            var maxValues = parsed.SelectMany(d => d.Observations).Select(o => o.Values.Length).DefaultIfEmpty(0).Max();
            BelstatDimension? valueDimension = null;
            if (maxValues > 1)
            {
                var yearsMatch = years != null && years.Length == maxValues;
                valueDimension = new BelstatDimension
                {
                    Id = ValueDimensionId,
                    Name = yearsMatch ? "Год" : "Значение №"
                };

                for (var i = 0; i < maxValues; i++)
                {
                    valueDimension.Values.Add(new BelstatDimensionValue
                    {
                        Id = i.ToString(CultureInfo.InvariantCulture),
                        Name = yearsMatch ? years![i].ToString(CultureInfo.InvariantCulture) : (i + 1).ToString(CultureInfo.InvariantCulture)
                    });
                }
            }

            // Итоговый набор колонок таблицы.
            var finalDimensions = new List<BelstatDimension>();
            var finalPositions = new Dictionary<BelstatDimension, int>();

            void AddDimension(BelstatDimension dimension)
            {
                finalPositions[dimension] = finalDimensions.Count;
                finalDimensions.Add(dimension);
            }

            if (withIndicatorColumn)
            {
                var indicatorDimension = new BelstatDimension { Id = IndicatorDimensionId, Name = IndicatorDimensionName };
                for (var i = 0; i < parsed.Count; i++)
                {
                    indicatorDimension.Values.Add(new BelstatDimensionValue
                    {
                        Id = i.ToString(CultureInfo.InvariantCulture),
                        Name = parsed[i].Label
                    });
                }

                AddDimension(indicatorDimension);
            }

            for (var i = 0; i < unionDimensions.Count; i++)
            {
                if (declaredKeep[i])
                    AddDimension(unionDimensions[i]);
            }

            foreach (var pair in extraDimensions.OrderBy(p => p.Key))
                AddDimension(pair.Value);

            if (valueDimension != null)
                AddDimension(valueDimension);

            var indicatorSlot = withIndicatorColumn ? 0 : -1;
            var valueSlot = valueDimension != null ? finalPositions[valueDimension] : -1;
            var observations = new Dictionary<string, string[]>(StringComparer.Ordinal);

            for (var d = 0; d < parsed.Count; d++)
            {
                var document = parsed[d];

                var dimensionSlots = new int[document.Dimensions.Count];
                for (var i = 0; i < document.Dimensions.Count; i++)
                {
                    var unionDimension = unionDimensions[unionPositions[document.Dimensions[i].Id]];
                    dimensionSlots[i] = finalPositions.TryGetValue(unionDimension, out var slot) ? slot : -1;
                }

                var extraSlots = new int[extraCount];
                for (var k = 0; k < extraCount; k++)
                {
                    extraSlots[k] = extraDimensions.TryGetValue(k, out var extraDimension) &&
                                    finalPositions.TryGetValue(extraDimension, out var slot)
                        ? slot
                        : -1;
                }

                foreach (var observation in document.Observations)
                {
                    var keyParts = new string[finalDimensions.Count];
                    for (var i = 0; i < keyParts.Length; i++)
                        keyParts[i] = UnspecifiedIndex;

                    if (indicatorSlot >= 0)
                        keyParts[indicatorSlot] = d.ToString(CultureInfo.InvariantCulture);

                    for (var i = 0; i < document.Dimensions.Count; i++)
                    {
                        if (dimensionSlots[i] < 0)
                            continue;

                        keyParts[dimensionSlots[i]] = ResolveObservationPart(
                            i < observation.Parts.Length ? observation.Parts[i] : UnspecifiedIndex,
                            document.Dimensions[i],
                            unionDimensions[unionPositions[document.Dimensions[i].Id]],
                            unionValuePositions);
                    }

                    for (var k = 0; k < extraCount; k++)
                    {
                        if (extraSlots[k] < 0)
                            continue;

                        var partIndex = document.Dimensions.Count + k;
                        var raw = partIndex < observation.Parts.Length ? observation.Parts[partIndex] : UnspecifiedIndex;
                        keyParts[extraSlots[k]] = extraValuePositions[k].TryGetValue(raw, out var position)
                            ? position.ToString(CultureInfo.InvariantCulture)
                            : UnspecifiedIndex;
                    }

                    var availableValues = observation.Values ?? Array.Empty<string>();
                    var valueCount = valueDimension == null
                        ? Math.Max(availableValues.Length, 1)
                        : Math.Min(availableValues.Length, valueDimension.Values.Count);

                    for (var v = 0; v < valueCount; v++)
                    {
                        if (valueSlot >= 0)
                            keyParts[valueSlot] = v.ToString(CultureInfo.InvariantCulture);

                        var key = string.Join(":", keyParts);
                        if (!observations.ContainsKey(key))
                            observations[key] = new[] { v < availableValues.Length ? availableValues[v] : string.Empty };
                    }
                }
            }

            if (observations.Count == 0)
                return null;

            return new BelstatDataResponse
            {
                Header = documents.Count > 0 ? documents[0].Document?.Header : null,
                Structure = new BelstatStructure
                {
                    Dimensions = new BelstatDimensions { Observation = finalDimensions }
                },
                DataSets = new List<BelstatDataSet>
                {
                    new BelstatDataSet { Action = "Information", Observations = observations }
                }
            };
        }

        private static List<ParsedDocument> ParseDocuments(IReadOnlyList<(string Label, BelstatDataResponse Document)> documents)
        {
            var parsed = new List<ParsedDocument>();

            foreach (var (label, document) in documents)
            {
                if (document == null)
                    continue;

                var item = new ParsedDocument { Label = label };
                item.Dimensions.AddRange(document.Structure?.Dimensions?.Observation ?? new List<BelstatDimension>());

                foreach (var dataSet in document.DataSets ?? new List<BelstatDataSet>())
                {
                    foreach (var pair in dataSet.Observations)
                    {
                        item.Observations.Add(new ParsedObservation
                        {
                            Parts = pair.Key.Split(':'),
                            Values = pair.Value ?? Array.Empty<string>()
                        });
                    }
                }

                parsed.Add(item);
            }

            return parsed;
        }

        private static int MaxExtraParts(ParsedDocument document) =>
            document.Observations.Count == 0
                ? 0
                : document.Observations.Max(o => Math.Max(0, o.Parts.Length - document.Dimensions.Count));

        private static bool IsDefinedIndex(string raw, int valueCount) =>
            int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) &&
            index >= 0 &&
            index < valueCount;

        private static BelstatDimension MergeDimension(
            BelstatDimension dimension,
            List<BelstatDimension> unionDimensions,
            Dictionary<string, int> unionPositions,
            Dictionary<string, Dictionary<string, int>> unionValuePositions)
        {
            if (!unionPositions.TryGetValue(dimension.Id, out var position))
            {
                position = unionDimensions.Count;
                unionPositions[dimension.Id] = position;
                unionDimensions.Add(new BelstatDimension { Id = dimension.Id, Name = dimension.Name });
                unionValuePositions[dimension.Id] = new Dictionary<string, int>(StringComparer.Ordinal);
            }

            var unionDimension = unionDimensions[position];
            var valuePositions = unionValuePositions[unionDimension.Id];

            foreach (var value in dimension.Values)
            {
                var valueId = ResolveValueId(value);

                if (valuePositions.TryGetValue(valueId, out var existing))
                {
                    if (string.IsNullOrWhiteSpace(unionDimension.Values[existing].Name) && !string.IsNullOrWhiteSpace(value.Name))
                        unionDimension.Values[existing].Name = value.Name;

                    continue;
                }

                valuePositions[valueId] = unionDimension.Values.Count;
                unionDimension.Values.Add(new BelstatDimensionValue { Id = valueId, Name = value.Name });
            }

            return unionDimension;
        }

        private static string ResolveValueId(BelstatDimensionValue value) =>
            string.IsNullOrWhiteSpace(value.Id) ? value.Name ?? string.Empty : value.Id;

        private static string ResolveObservationPart(
            string raw,
            BelstatDimension documentDimension,
            BelstatDimension unionDimension,
            Dictionary<string, Dictionary<string, int>> unionValuePositions)
        {
            if (documentDimension.Values.Count == 0 ||
                !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ||
                index < 0 ||
                index >= documentDimension.Values.Count)
            {
                return UnspecifiedIndex;
            }

            var valueId = ResolveValueId(documentDimension.Values[index]);

            if (unionValuePositions.TryGetValue(unionDimension.Id, out var positions) &&
                positions.TryGetValue(valueId, out var position))
            {
                return position.ToString(CultureInfo.InvariantCulture);
            }

            return UnspecifiedIndex;
        }
    }
}

