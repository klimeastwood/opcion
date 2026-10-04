using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NalogParserWPF.Helpers;
using NalogParserWPF.Services;

namespace NalogParserWPF.ViewModels
{
    /// <summary>
    /// Источник данных (API), доступный для выбора в окне «Настройки».
    /// </summary>
    public sealed class ApiOption
    {
        public ApiOption(string key, string sectionName, string title, string url, bool isImplemented)
        {
            Key = key;
            SectionName = sectionName;
            Title = title;
            Url = url;
            IsImplemented = isImplemented;
        }

        /// <summary>Ключ источника данных: он же значение настройки SelectedApi.</summary>
        public string Key { get; }

        /// <summary>Секция appsettings.json, в которой хранится адрес источника.</summary>
        public string SectionName { get; }

        /// <summary>Название источника для выпадающего списка.</summary>
        public string Title { get; }

        /// <summary>Базовый адрес API.</summary>
        public string Url { get; }

        /// <summary>Реализован ли клиент этого API в приложении.</summary>
        public bool IsImplemented { get; }

        /// <summary>
        /// Подпись в списке. Для источников, клиент которых ещё не реализован,
        /// добавляется пометка — чтобы выбор не выглядел как рабочая функция.
        /// </summary>
        public string DisplayTitle => IsImplemented ? Title : Title + " — в разработке";

        /// <summary>
        /// Текстовое представление используется системами доступности (UI Automation),
        /// поэтому в списке озвучивается та же подпись, что видит пользователь.
        /// </summary>
        public override string ToString() => DisplayTitle;
    }

    public class SettingsViewModel : ViewModelBase
    {
        /// <summary>Ключ источника Налоговой службы: поиск по УНП работает только через него.</summary>
        private const string NalogApiKey = "Nalog";

        /// <summary>Ключ источника НБ РБ: через него приходят курсы валют.</summary>
        private const string NbrbApiKey = "Nbrb";

        /// <summary>Ключ источника по умолчанию.</summary>
        private const string DefaultApiKey = NalogApiKey;

        /// <summary>
        /// Каталог источников данных: подпись, адрес и секция appsettings.json.
        /// Адреса — единственный источник правды для поля «Адрес API» в настройках.
        /// </summary>
        private static readonly IReadOnlyList<ApiOption> ApiOptions = new[]
        {
            new ApiOption(NalogApiKey, "ApiSettings", "Налоговая служба (grp.nalog.gov.by)",
                "http://grp.nalog.gov.by/api/grp-public/data", true),
            new ApiOption(NbrbApiKey, "NbrbSettings", "Национальный банк (api.nbrb.by)",
                "https://api.nbrb.by/exrates/", true),
            new ApiOption("Belstat", "BelstatSettings", "Белстат (dataportal.belstat.gov.by)",
                "https://dataportal.belstat.gov.by/osids-public-api", true)
        };

        private readonly IConfiguration _configuration;
        private readonly ILogger<SettingsViewModel> _logger;
        private readonly INotificationService _notifications;

        private int _timeoutSeconds;
        private int _retryCount;
        private string _theme = "Light";
        private string _selectedApi = DefaultApiKey;

        public SettingsViewModel(
            IConfiguration configuration,
            ILogger<SettingsViewModel> logger,
            INotificationService notifications)
        {
            _configuration = configuration;
            _logger = logger;
            _notifications = notifications;

            LoadFromConfiguration();

            SaveCommand = new RelayCommand(_ => Save());
            ResetCommand = new RelayCommand(_ => LoadFromConfiguration());
            CancelCommand = new RelayCommand(_ => OnCloseRequested());
        }

        /// <summary>Доступные источники данных для выпадающего списка в настройках.</summary>
        public IReadOnlyList<ApiOption> AvailableApis => ApiOptions;

        /// <summary>
        /// Адрес выбранного источника данных. В окне настроек поле только для
        /// чтения: адрес меняется автоматически при смене источника API.
        /// </summary>
        public string ApiUrl => GetApiOption(SelectedApi).Url;

        public int TimeoutSeconds
        {
            get => _timeoutSeconds;
            set => SetProperty(ref _timeoutSeconds, value);
        }

        public int RetryCount
        {
            get => _retryCount;
            set => SetProperty(ref _retryCount, value);
        }

        public string Theme
        {
            get => _theme;
            set => SetProperty(ref _theme, value);
        }

        /// <summary>
        /// Выбранный источник данных. Ключ хранится в appsettings.json в секции "SelectedApi".
        /// Возможные значения: "Nalog" (grp.nalog.gov.by), "Nbrb" (api.nbrb.by), "Belstat" (dataportal.belstat.gov.by).
        /// При смене значения автоматически пересчитывается <see cref="ApiUrl"/>.
        /// </summary>
        public string SelectedApi
        {
            get => _selectedApi;
            set
            {
                if (SetProperty(ref _selectedApi, NormalizeApiKey(value)))
                    OnPropertyChanged(nameof(ApiUrl));
            }
        }

        public RelayCommand SaveCommand { get; }
        public RelayCommand ResetCommand { get; }
        public RelayCommand CancelCommand { get; }

        /// <summary>Описание источника данных по его ключу (неизвестный ключ → источник по умолчанию).</summary>
        private static ApiOption GetApiOption(string? key)
        {
            return ApiOptions.FirstOrDefault(option =>
                       string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))
                   ?? ApiOptions.First(option => option.Key == DefaultApiKey);
        }

        /// <summary>Приводит ключ источника к известному значению из каталога.</summary>
        private static string NormalizeApiKey(string? key)
        {
            return GetApiOption(key).Key;
        }

        public void LoadFromConfiguration()
        {
            TimeoutSeconds = int.TryParse(_configuration["ApiSettings:TimeoutSeconds"], out var timeout) && timeout > 0
                ? timeout
                : 30;
            RetryCount = int.TryParse(_configuration["ApiSettings:RetryCount"], out var retries) && retries > 0
                ? retries
                : 3;
            Theme = _configuration["Theme"] ?? "Light";
            SelectedApi = _configuration["SelectedApi"] ?? DefaultApiKey;
        }

        private void Save()
        {
            if (TimeoutSeconds <= 0)
                TimeoutSeconds = 30;

            if (RetryCount <= 0)
                RetryCount = 3;

            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                var root = File.Exists(path)
                    ? JObject.Parse(File.ReadAllText(path))
                    : new JObject();

                // Таймаут и количество попыток относятся к API Налоговой службы.
                var apiSettings = root["ApiSettings"] as JObject ?? new JObject();
                if (string.IsNullOrWhiteSpace((string?)apiSettings["BaseUrl"]))
                {
                    // Поиск по УНП работает только через API Налоговой службы,
                    // поэтому адрес этой секции всегда должен быть заполнен.
                    apiSettings["BaseUrl"] = GetApiOption(NalogApiKey).Url;
                }
                apiSettings["TimeoutSeconds"] = TimeoutSeconds;
                apiSettings["RetryCount"] = RetryCount;
                root["ApiSettings"] = apiSettings;

                var nbrbSettings = root["NbrbSettings"] as JObject ?? new JObject();
                if (string.IsNullOrWhiteSpace((string?)nbrbSettings["BaseUrl"]))
                    nbrbSettings["BaseUrl"] = GetApiOption(NbrbApiKey).Url;
                if (nbrbSettings["TimeoutSeconds"] == null)
                    nbrbSettings["TimeoutSeconds"] = TimeoutSeconds;
                root["NbrbSettings"] = nbrbSettings;

                // Адрес выбранного источника записывается в его собственную секцию,
                // чтобы при следующем запуске приложение работало с выбранным API.
                var selectedApi = GetApiOption(SelectedApi);
                var selectedSettings = root[selectedApi.SectionName] as JObject ?? new JObject();
                selectedSettings["BaseUrl"] = selectedApi.Url;
                root[selectedApi.SectionName] = selectedSettings;

                root["Theme"] = Theme;
                root["SelectedApi"] = SelectedApi;

                File.WriteAllText(path, root.ToString(Formatting.Indented));

                App.ApplyTheme(Theme);

                _notifications.ShowSuccess("Настройки сохранены. Изменения применятся при следующем поиске.");
                _logger.LogInformation(
                    "Настройки сохранены: Api={Api}, Url={Url}, Timeout={Timeout}s, Retry={Retry}, Theme={Theme}",
                    SelectedApi, ApiUrl, TimeoutSeconds, RetryCount, Theme);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении настроек");
                _notifications.ShowError($"Ошибка при сохранении настроек: {ex.Message}");
            }
        }
    }
}
