using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NalogParserWPF.Configuration;
using NalogParserWPF.Database;
using NalogParserWPF.Helpers;
using NalogParserWPF.Services;
using NalogParserWPF.ViewModels;
using NalogParserWPF.Views;

namespace NalogParserWPF
{
    public partial class App : Application
    {
        private IHost? _host;
        private ILogger<App>? _logger;

        public static IServiceProvider? ServiceProvider { get; private set; }

        public static void ApplyTheme(string theme)
        {
            var resources = Application.Current?.Resources;
            if (resources == null) return;

            bool isDark = string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase);

            // Палитра СК: васильковый (AccentBrush) + жёлтый акцент (SecondaryBrush).
            Set("WindowBackgroundBrush", 0xFFF0F4FC, 0xFF0F1B33);

            Set("PanelBackgroundBrush", 0xFFF7FAFF, 0xFF16243F);

            Set("TextPrimaryBrush", 0xFF0D1B3E, 0xFFE8EDF7);

            Set("TextAccentBrush", 0xFF1A3A8F, 0xFF9DB4F0);

            Set("AccentBrush", 0xFF1A3A8F, 0xFF4A6FD4);

            // Мягкий акцент: фон наведения в списках (пункты выпадающих списков).
            // Выбранный пункт залит AccentBrush, поэтому наведение должно быть
            // заметно светлее/темнее него, иначе оба пункта выглядят выбранными.
            Set("LightAccentBrush", 0xFFDCE6FA, 0xFF324A7D);

            Set("TabBackgroundBrush", 0xFFDCE6FA, 0xFF1B2A47);

            // Остальные цвета темы. Значения для светлой темы совпадают с палитрой в App.xaml,
            // поэтому внешний вид не меняется, а тёмная тема получает согласованные оттенки.
            void Set(string key, uint lightArgb, uint darkArgb)
            {
                uint argb = isDark ? darkArgb : lightArgb;
                resources[key] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(
                    (byte)(argb >> 24),
                    (byte)(argb >> 16),
                    (byte)(argb >> 8),
                    (byte)argb));
            }

            Set("CardBackgroundBrush", 0xFFFFFFFF, 0xFF1B2A47);
            Set("InputBackgroundBrush", 0xFFFFFFFF, 0xFF22334F);
            // Выпадающие списки (ComboBox) и поля даты: значения из правки
            // доступности тёмной темы — серый фон и светлый текст.
            Set("ComboBoxBackgroundBrush", 0xFFFFFFFF, 0xFF3F3F3F);
            Set("ComboBoxForegroundBrush", 0xFF0D1B3E, 0xFFE0E0E0);
            Set("ComboBoxBorderBrush", 0xFFC9D4EA, 0xFF555555);
            Set("TextSecondaryBrush", 0xFF4A5578, 0xFFAEB9D6);
            Set("TextMutedBrush", 0xFF8A93AD, 0xFF7E89A8);
            Set("BorderBrush", 0xFFC9D4EA, 0xFF34456A);
            Set("GridLineBrush", 0xFFDCE4F2, 0xFF2C3B5C);
            Set("HeaderBackgroundBrush", 0xFFE1E9F8, 0xFF24365A);
            Set("AltRowBackgroundBrush", 0xFFF5F8FF, 0xFF1A2949);
            Set("TabHoverBrush", 0xFFC6D6F5, 0xFF2A3E66);
            Set("TabHoverForegroundBrush", 0xFF12276B, 0xFFE8EDF7);
            Set("DividerBrush", 0xFFB9C7E4, 0xFF34456A);
            Set("ButtonBackgroundBrush", 0xFF1A3A8F, 0xFF4A6FD4);
            Set("ButtonTextBrush", 0xFFFFFFFF, 0xFFFFFFFF);
            Set("OnAccentBrush", 0xFFFFFFFF, 0xFFFFFFFF);
            Set("SecondaryBrush", 0xFFF5C518, 0xFFF5C518);
            Set("OnSecondaryBrush", 0xFF0D1B3E, 0xFF0D1B3E);
            Set("WarningBackgroundBrush", 0xFFFFF9E6, 0xFF4A3F1E);
            Set("WarningBorderBrush", 0xFFF5C518, 0xFFF5C518);
            Set("WarningTextBrush", 0xFF8A6D00, 0xFFF0D060);
            Set("HighlightBackgroundBrush", 0xFFFFF3C4, 0xFF6B5A1E);
            Set("DangerSoftBrush", 0xFFC97B7B, 0xFFC97B7B);
            Set("DangerBrush", 0xFFC0392B, 0xFFE06B5E);
            Set("ExportButtonBrush", 0xFFB87333, 0xFFB87333);
            Set("ExcelButtonBrush", 0xFF217346, 0xFF2E9E63);
            Set("SecondaryButtonBrush", 0xFF2E4FA8, 0xFF3A5BC0);
            Set("NeutralButtonBrush", 0xFF5A6480, 0xFF4A5578);
            Set("OverlayDisabledBrush", 0x14000000, 0x1AFFFFFF);
            Set("OverlayPressedBrush", 0x33000000, 0x33FFFFFF);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureLogging((context, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddDebug();
                    logging.AddConfiguration(context.Configuration.GetSection("Logging"));
                })
                .ConfigureServices((context, services) =>
                {
                    services.Configure<ApiSettings>(context.Configuration.GetSection("ApiSettings"));
                    services.Configure<DatabaseSettings>(context.Configuration.GetSection("DatabaseSettings"));
                    services.Configure<NbrbSettings>(context.Configuration.GetSection("NbrbSettings"));
                    services.Configure<BelstatSettings>(context.Configuration.GetSection("BelstatSettings"));

                    services.AddTransient<HttpLoggingMessageHandler>();
                    services.AddTransient<HttpRetryDelegatingHandler>(provider =>
                    {
                        var settings = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiSettings>>().Value;
                        return new HttpRetryDelegatingHandler(settings.RetryCount);
                    });

                    services.AddHttpClient<INalogApiService, NalogApiService>((provider, client) =>
                    {
                        var settings = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiSettings>>().Value;
                        client.BaseAddress = new System.Uri(settings.BaseUrl);
                        client.Timeout = System.TimeSpan.FromSeconds(settings.TimeoutSeconds <= 0 ? 30 : settings.TimeoutSeconds);
                    })
                    .AddHttpMessageHandler<HttpRetryDelegatingHandler>()
                    .AddHttpMessageHandler<HttpLoggingMessageHandler>();

                    services.AddHttpClient<INBRBApiService, NBRBApiService>((provider, client) =>
                    {
                        var settings = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<NbrbSettings>>().Value;
                        client.BaseAddress = new System.Uri(settings.BaseUrl);
                        client.Timeout = System.TimeSpan.FromSeconds(settings.TimeoutSeconds <= 0 ? 30 : settings.TimeoutSeconds);
                    })
                    .AddHttpMessageHandler<HttpRetryDelegatingHandler>()
                    .AddHttpMessageHandler<HttpLoggingMessageHandler>();

                    services.AddHttpClient<IBelstatApiService, BelstatApiService>((provider, client) =>
                    {
                        var settings = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BelstatSettings>>().Value;
                        client.BaseAddress = new System.Uri(settings.BaseUrl);
                        client.Timeout = System.TimeSpan.FromSeconds(settings.TimeoutSeconds <= 0 ? 60 : settings.TimeoutSeconds);
                        client.DefaultRequestHeaders.Add("Accept-Language", string.IsNullOrWhiteSpace(settings.Language) ? "ru" : settings.Language);
                    })
                    .AddHttpMessageHandler<HttpRetryDelegatingHandler>()
                    .AddHttpMessageHandler<HttpLoggingMessageHandler>();

                    services.AddSingleton<IHistoryRepository, DatabaseHelper>();
                    services.AddSingleton<IExchangeHistoryRepository, ExchangeHistoryRepository>();
                    services.AddSingleton<IBelstatHistoryRepository, BelstatHistoryRepository>();
                    services.AddSingleton<ICacheService, InMemoryCacheService>();
                    services.AddTransient<INotificationService, NotificationService>();

                    services.AddTransient<IResultFormatter, ResultFormatter>();
                    services.AddTransient<IPdfExportService, PdfExportService>();

                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<MainWindow>();

                    services.AddTransient<HistoryDetailViewModel>();
                    services.AddTransient<BatchSearchViewModel>();
                    services.AddTransient<StatusChangeViewModel>();
                    services.AddSingleton<ExchangeRateViewModel>();
                    services.AddSingleton<BelstatViewModel>();
                    services.AddTransient<SettingsViewModel>();
                    services.AddTransient<HistoryDetailWindow>();
                    services.AddTransient<BatchSearchWindow>();
                    services.AddTransient<StatusChangeWindow>();
                    services.AddTransient<Views.SettingsWindow>();
                    services.AddTransient<Views.AboutWindow>();
                    services.AddTransient<Views.BelstatDataWindow>();
                })
                .Build();

            ServiceProvider = _host.Services;

            _logger = _host.Services.GetRequiredService<ILogger<App>>();

            var configuration = _host.Services.GetRequiredService<IConfiguration>();
            ApplyTheme(configuration["Theme"] ?? "Light");

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            DispatcherUnhandledException -= OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;

            _host?.Dispose();
            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            _logger?.LogError(e.Exception, "Необработанное исключение в потоке UI");
            ShowFatalError(e.Exception);
            e.Handled = true;
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            _logger?.LogError(e.Exception, "Необработанное исключение в фоновой задаче (Task)");
            e.SetObserved();
        }

        private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            _logger?.LogError(exception, "Критическое необработанное исключение AppDomain");
        }

        private void ShowFatalError(Exception ex)
        {
            try
            {
                MessageBox.Show(
                    $"Произошла критическая ошибка:\n\n{ex.Message}\n\n" +
                    "Подробности записаны в журнал приложения.",
                    "Критическая ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch
            {
                // Если не удалось показать диалог, приложение продолжает работу с логом.
            }
        }
    }
}
