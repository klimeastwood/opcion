using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using NalogParserWPF.Helpers;
using NalogParserWPF.Models;
using NalogParserWPF.Services;

namespace NalogParserWPF.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly INalogApiService _apiService;
        private readonly IHistoryRepository _historyRepository;
        private readonly ILogger<MainViewModel> _logger;
        private CancellationTokenSource? _cancellationTokenSource;

        private string _searchType = "Legal";
        private string _searchQuery = string.Empty;
        private bool _isSearching;
        private string _statusMessage = "Готов к поиску";
        private NalogResponse? _currentResult;

        public MainViewModel(
            INalogApiService apiService,
            IHistoryRepository historyRepository,
            ILogger<MainViewModel> logger)
        {
            _apiService = apiService;
            _historyRepository = historyRepository;
            _logger = logger;

            HistoryItems = new ObservableCollection<SearchHistory>();
            SearchCommand = new RelayCommand(_ => ExecuteSearchAsync(), _ => !IsSearching && !string.IsNullOrWhiteSpace(SearchQuery));
            ClearHistoryCommand = new RelayCommand(_ => ClearHistoryAsync());
            ViewHistoryCommand = new RelayCommand<SearchHistory>(item => ViewHistory(item));
            RepeatSearchCommand = new RelayCommand<SearchHistory>(item => RepeatSearchAsync(item));
            CancelSearchCommand = new RelayCommand(_ => CancelSearch());

            _ = LoadHistoryAsync();
        }

        public string SearchType
        {
            get => _searchType;
            set => SetProperty(ref _searchType, value);
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                    SearchCommand.RaiseCanExecuteChanged();
            }
        }

        public bool IsSearching
        {
            get => _isSearching;
            set
            {
                if (SetProperty(ref _isSearching, value))
                    SearchCommand.RaiseCanExecuteChanged();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public NalogResponse? CurrentResult
        {
            get => _currentResult;
            set => SetProperty(ref _currentResult, value);
        }

        public ObservableCollection<SearchHistory> HistoryItems { get; }

        public RelayCommand SearchCommand { get; }
        public RelayCommand LoadHistoryCommand { get; }
        public RelayCommand ClearHistoryCommand { get; }
        public RelayCommand<SearchHistory> ViewHistoryCommand { get; }
        public RelayCommand<SearchHistory> RepeatSearchCommand { get; }
        public RelayCommand CancelSearchCommand { get; }

        private async Task ExecuteSearchAsync()
        {
            if (IsSearching) return;

            try
            {
                IsSearching = true;
                StatusMessage = "Выполняется поиск...";
                _cancellationTokenSource = new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;

                NalogResponse? result = null;

                if (SearchType == "Legal")
                {
                    var parameters = new LegalSearchParams
                    {
                        Unp = SearchQuery,
                        Name = SearchQuery
                    };
                    result = await _apiService.SearchLegalEntityAsync(parameters, token);
                }
                else
                {
                    var parameters = new PhysicalSearchParams
                    {
                        Unp = SearchQuery,
                        Fio = SearchQuery
                    };
                    result = await _apiService.SearchPhysicalPersonAsync(parameters, token);
                }

                if (result?.Row != null)
                {
                    CurrentResult = result;
                    StatusMessage = $"Найдено: {result.Row.FullName ?? SearchQuery}";

                    // Сохраняем в историю
                    var historyEntry = new SearchHistory
                    {
                        SearchType = SearchType,
                        SearchParams = SearchQuery,
                        ResultData = Newtonsoft.Json.JsonConvert.SerializeObject(result),
                        Unp = SearchQuery,
                        FullName = result.Row.FullName ?? SearchQuery,
                        Status = "Активный",
                        IsActive = true
                    };
                    await _historyRepository.AddAsync(historyEntry, token);
                    await LoadHistoryAsync();
                }
                else
                {
                    StatusMessage = "Ничего не найдено";
                    CurrentResult = null;
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Поиск отменен";
                _logger.LogInformation("Поиск отменен пользователем");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка: {ex.Message}";
                _logger.LogError(ex, "Ошибка при выполнении поиска");
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSearching = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private async Task LoadHistoryAsync()
        {
            try
            {
                var history = await _historyRepository.GetAllAsync();
                HistoryItems.Clear();
                foreach (var item in history)
                {
                    HistoryItems.Add(item);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке истории");
            }
        }

        private async void ClearHistoryAsync()
        {
            if (MessageBox.Show("Очистить всю историю?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    await _historyRepository.ClearAllAsync();
                    HistoryItems.Clear();
                    StatusMessage = "История очищена";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при очистке истории");
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ViewHistory(SearchHistory item)
        {
            if (item == null) return;
            // TODO: Открыть HistoryDetailWindow с ViewModel
            StatusMessage = $"Просмотр записи #{item.Id}";
        }

        private async void RepeatSearchAsync(SearchHistory item)
        {
            if (item == null) return;
            SearchQuery = item.Unp ?? string.Empty;
            SearchType = item.SearchType;
            await ExecuteSearchAsync();
        }

        private void CancelSearch()
        {
            _cancellationTokenSource?.Cancel();
            StatusMessage = "Отмена поиска...";
        }
    }
}
