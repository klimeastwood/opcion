using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace NalogParserWPF.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(ILogger<NotificationService> logger)
        {
            _logger = logger;
        }

        public void ShowSuccess(string message, string title = "Успешно")
        {
            _logger.LogInformation("Уведомление (успех): {Message}", message);
            Show(() => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information));
        }

        public void ShowError(string message, string title = "Ошибка")
        {
            _logger.LogError("Уведомление (ошибка): {Message}", message);
            Show(() => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error));
        }

        public void ShowWarning(string message, string title = "Предупреждение")
        {
            _logger.LogWarning("Уведомление (предупреждение): {Message}", message);
            Show(() => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning));
        }

        public void ShowInfo(string message, string title = "Информация")
        {
            _logger.LogInformation("Уведомление (информация): {Message}", message);
            Show(() => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information));
        }

        private static void Show(Action show)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                show();
                return;
            }

            dispatcher.Invoke(DispatcherPriority.Normal, new Action(() => show()));
        }
    }
}
