using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Newtonsoft.Json;
using NalogParserWPF.Helpers;
using NalogParserWPF.Models;
using NalogParserWPF.Services;

namespace NalogParserWPF.ViewModels
{
    public class HistoryDetailViewModel : ViewModelBase
    {
        public const string UnavailableMessage = "Информация временно недоступна. Подробнее на сайте grp.nalog.gov.by";

        private readonly IHistoryRepository _historyRepository;
        private readonly IResultFormatter _resultFormatter;
        private readonly IPdfExportService _pdfExportService;
        private readonly INotificationService _notifications;
        private readonly ILogger<HistoryDetailViewModel> _logger;

        private SearchHistory? _currentEntry;
        private string _formattedResult = string.Empty;
        private bool _hasCompanyData;
        private string _unp = string.Empty;
        private string _fullName = string.Empty;
        private string _shortName = string.Empty;
        private string _address = string.Empty;
        private string _registrationDate = string.Empty;
        private string _inspectionCode = string.Empty;
        private string _inspectionName = string.Empty;
        private string _statusCode = string.Empty;
        private string _status = string.Empty;
        private string _statusChangeDate = string.Empty;
        private string _liquidationDate = string.Empty;
        private string _updateInfo = string.Empty;

        public HistoryDetailViewModel(
            IHistoryRepository historyRepository,
            IResultFormatter resultFormatter,
            IPdfExportService pdfExportService,
            INotificationService notifications,
            ILogger<HistoryDetailViewModel> logger)
        {
            _historyRepository = historyRepository;
            _resultFormatter = resultFormatter;
            _pdfExportService = pdfExportService;
            _notifications = notifications;
            _logger = logger;

            LoadEntryCommand = new RelayCommand<int>(id => _ = LoadEntryAsync(id));
            ExportPdfCommand = new RelayCommand(_ => _ = ExportPdfAsync(), _ => CurrentEntry != null);
            CopyResultCommand = new RelayCommand(_ => CopyResult(), _ => !string.IsNullOrEmpty(FormattedResult));
        }

        public SearchHistory? CurrentEntry
        {
            get => _currentEntry;
            set
            {
                if (SetProperty(ref _currentEntry, value))
                    ExportPdfCommand.RaiseCanExecuteChanged();
            }
        }

        public string FormattedResult
        {
            get => _formattedResult;
            set => SetProperty(ref _formattedResult, value);
        }

        public bool HasCompanyData
        {
            get => _hasCompanyData;
            set => SetProperty(ref _hasCompanyData, value);
        }

        public string Unp { get => _unp; set => SetProperty(ref _unp, value); }
        public string FullName { get => _fullName; set => SetProperty(ref _fullName, value); }
        public string ShortName { get => _shortName; set => SetProperty(ref _shortName, value); }
        public string Address { get => _address; set => SetProperty(ref _address, value); }
        public string RegistrationDate { get => _registrationDate; set => SetProperty(ref _registrationDate, value); }
        public string InspectionCode { get => _inspectionCode; set => SetProperty(ref _inspectionCode, value); }
        public string InspectionName { get => _inspectionName; set => SetProperty(ref _inspectionName, value); }
        public string StatusCode { get => _statusCode; set => SetProperty(ref _statusCode, value); }
        public string Status { get => _status; set => SetProperty(ref _status, value); }
        public string StatusChangeDate { get => _statusChangeDate; set { if (SetProperty(ref _statusChangeDate, value)) OnPropertyChanged(nameof(HasStatusChangeDate)); } }
        public string LiquidationDate { get => _liquidationDate; set { if (SetProperty(ref _liquidationDate, value)) OnPropertyChanged(nameof(HasLiquidationDate)); } }
        public string UpdateInfo { get => _updateInfo; set { if (SetProperty(ref _updateInfo, value)) OnPropertyChanged(nameof(HasUpdateInfo)); } }

        public bool HasUpdateInfo => !string.IsNullOrEmpty(UpdateInfo);

        public bool HasStatusChangeDate => !string.IsNullOrEmpty(StatusChangeDate);
        public bool HasLiquidationDate => !string.IsNullOrEmpty(LiquidationDate);
        public string NamesHistoryMessage => UnavailableMessage;
        public string AddressesHistoryMessage => UnavailableMessage;
        public string ActivitiesHistoryMessage => UnavailableMessage;
        public string ContactsMessage => UnavailableMessage;

        public RelayCommand<int> LoadEntryCommand { get; }
        public RelayCommand ExportPdfCommand { get; }
        public RelayCommand CopyResultCommand { get; }

        public void CopyResult()
        {
            if (string.IsNullOrEmpty(FormattedResult))
                return;

            try
            {
                Clipboard.SetText(FormattedResult);
                _logger.LogInformation("Результат записи скопирован в буфер обмена");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при копировании результата записи в буфер обмена");
                _notifications.ShowError($"Ошибка при копировании: {ex.Message}");
            }
        }

        public async Task LoadEntryAsync(int id)
        {
            try
            {
                var entry = await _historyRepository.GetByIdAsync(id);
                if (entry == null)
                {
                    CurrentEntry = null;
                    FormattedResult = "Запись не найдена.";
                    SetCompany(null);
                    return;
                }

                CurrentEntry = entry;
                UpdateInfo = entry.UpdateInfo ?? string.Empty;
                SetCompany(TryParseCompany(entry.ResultData));
                FormattedResult = _resultFormatter.FormatHistoryEntry(entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке записи #{Id}", id);
                FormattedResult = $"Ошибка при загрузке записи: {ex.Message}";
            }
        }

        public async Task ExportPdfAsync()
        {
            var entry = CurrentEntry;
            if (entry == null)
            {
                _notifications.ShowInfo("Нет данных для экспорта.");
                return;
            }

            var company = TryParseCompany(entry.ResultData);
            if (company == null)
            {
                _notifications.ShowInfo("Нет данных для экспорта.");
                return;
            }

            string fileName = $"История_{entry.Unp ?? "Запись"}";
            if (!string.IsNullOrEmpty(company.ShortName))
                fileName += $"_{company.ShortName}";
            else if (!string.IsNullOrEmpty(company.FullName))
                fileName += $"_{company.FullName}";
            fileName += $"_{DateTimeHelper.NowBelarus:yyyy-MM-dd}";

            foreach (char c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');

            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF файлы (*.pdf)|*.pdf",
                FileName = fileName,
                DefaultExt = "pdf"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                _logger.LogInformation("Начало экспорта записи #{Id} в PDF", entry.Id);
                await _pdfExportService.ExportHistoryEntryAsync(entry, saveDialog.FileName);
                _logger.LogInformation("Экспорт записи #{Id} в PDF успешно завершен", entry.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при экспорте записи #{Id} в PDF", entry.Id);
                _notifications.ShowError($"Ошибка при экспорте: {ex.Message}");
            }
        }

        private void SetCompany(CompanyRow? company)
        {
            if (company == null)
            {
                Unp = FullName = ShortName = Address = RegistrationDate = string.Empty;
                InspectionCode = InspectionName = StatusCode = Status = string.Empty;
                StatusChangeDate = LiquidationDate = string.Empty;
                UpdateInfo = string.Empty;
                HasCompanyData = false;
                return;
            }

            Unp = company.Unp ?? string.Empty;
            FullName = company.FullName ?? string.Empty;
            ShortName = company.ShortName ?? string.Empty;
            Address = company.Address ?? string.Empty;
            RegistrationDate = company.RegistrationDate ?? string.Empty;
            InspectionCode = company.InspectionCode ?? string.Empty;
            InspectionName = company.InspectionName ?? string.Empty;
            StatusCode = company.StatusCode ?? string.Empty;
            Status = company.Status ?? string.Empty;
            StatusChangeDate = company.StatusChangeDate ?? string.Empty;
            LiquidationDate = company.LiquidationDate ?? string.Empty;
            HasCompanyData = true;
        }

        private static CompanyRow? TryParseCompany(string? resultData)
        {
            if (string.IsNullOrWhiteSpace(resultData)) return null;
            try
            {
                return JsonConvert.DeserializeObject<NalogResponse>(resultData)?.Row
                       ?? JsonConvert.DeserializeObject<CompanyRow>(resultData);
            }
            catch
            {
                return null;
            }
        }
    }
}
