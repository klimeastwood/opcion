using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NalogParserWPF.Helpers;
using NalogParserWPF.Models;
using NalogParserWPF.Services;

namespace NalogParserWPF.ViewModels
{
    public class StatusChangeViewModel : ViewModelBase
    {
        private readonly IHistoryRepository _historyRepository;
        private readonly INotificationService _notifications;
        private readonly ILogger<StatusChangeViewModel> _logger;

        private bool _isLoading;
        private string _statusMessage = "Готов к проверке";

        public StatusChangeViewModel(
            IHistoryRepository historyRepository,
            INotificationService notifications,
            ILogger<StatusChangeViewModel> logger)
        {
            _historyRepository = historyRepository;
            _notifications = notifications;
            _logger = logger;

            Items = new ObservableCollection<StatusChangeItem>();
            CheckStatusesCommand = new RelayCommand(_ => _ = CheckStatusesAsync(), _ => !IsLoading);
        }

        public ObservableCollection<StatusChangeItem> Items { get; }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                    CheckStatusesCommand.RaiseCanExecuteChanged();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public RelayCommand CheckStatusesCommand { get; }

        public async Task CheckStatusesAsync()
        {
            if (IsLoading) return;

            try
            {
                IsLoading = true;
                StatusMessage = "Загрузка активных УНП...";

                var activeUnps = await _historyRepository.GetActiveUnpsAsync();

                Items.Clear();
                if (activeUnps.Count == 0)
                {
                    StatusMessage = "Активные записи не найдены";
                    return;
                }

                // Группируем по УНП, берём последнюю запись каждого
                var latestByUnp = activeUnps
                    .GroupBy(x => x.Unp)
                    .Select(g => g.OrderByDescending(x => x.SearchDate).First())
                    .ToList();

                foreach (var entry in latestByUnp)
                {
                    var item = new StatusChangeItem
                    {
                        Unp = entry.Unp,
                        FullName = entry.FullName,
                        OldStatus = entry.PreviousStatus ?? entry.Status,
                        NewStatus = entry.Status,
                        CheckDate = entry.LastChecked?.ToString("dd.MM.yyyy HH:mm") ?? entry.SearchDate.ToString("dd.MM.yyyy HH:mm")
                    };
                    Items.Add(item);
                }

                StatusMessage = Items.Count > 0
                    ? $"Найдено изменений: {Items.Count}"
                    : "Изменения не обнаружены";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке статусов");
                StatusMessage = $"Ошибка: {ex.Message}";
                _notifications.ShowError($"Ошибка при проверке статусов: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
