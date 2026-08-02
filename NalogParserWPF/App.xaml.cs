using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NalogParserWPF.Configuration;
using NalogParserWPF.Database;
using NalogParserWPF.Services;
using NalogParserWPF.ViewModels;

namespace NalogParserWPF
{
    public partial class App : Application
    {
        private IHost? _host;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

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

                    services.AddHttpClient<INalogApiService, NalogApiService>((provider, client) =>
                    {
                        var settings = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiSettings>>().Value;
                        client.BaseAddress = new System.Uri(settings.BaseUrl);
                        client.Timeout = System.TimeSpan.FromSeconds(settings.TimeoutSeconds);
                    });

                    services.AddSingleton<IHistoryRepository, DatabaseHelper>();
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _host?.Dispose();
            base.OnExit(e);
        }
    }
}
