using System;

namespace NalogParserWPF.Services
{
    public interface INotificationService
    {
        void ShowSuccess(string message, string title = "Успешно");
        void ShowError(string message, string title = "Ошибка");
        void ShowWarning(string message, string title = "Предупреждение");
        void ShowInfo(string message, string title = "Информация");
    }
}
