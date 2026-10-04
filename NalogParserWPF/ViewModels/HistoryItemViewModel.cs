using System;
using NalogParserWPF.Helpers;
using NalogParserWPF.Models;

namespace NalogParserWPF.ViewModels
{
    public class HistoryItemViewModel : ViewModelBase
    {
        public int Id { get; set; }
        public string Unp { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? UpdateInfo { get; set; }
        public bool HasUpdateInfo => !string.IsNullOrWhiteSpace(UpdateInfo);
        public DateTime Timestamp { get; set; }
        public string FullData { get; set; } = string.Empty;

        /// <summary>
        /// Русская подпись типа записи («Legal» → «Юридическое лицо»).
        /// Используется в колонке «Тип» и в подсказках таблицы истории.
        /// </summary>
        public string TypeText => Converters.SearchTypeToRussianConverter.ToRussianText(Type);

        /// <summary>Расшифровка статуса для ToolTip плашки статуса (см. StatusHelper.GetStatusHint).</summary>
        public string StatusHint => StatusHelper.GetStatusHint(Status);

        public static HistoryItemViewModel CreateFromModel(SearchHistory model)
        {
            return new HistoryItemViewModel
            {
                Id = model.Id,
                Unp = model.Unp ?? string.Empty,
                Name = model.FullName ?? string.Empty,
                Type = model.SearchType ?? string.Empty,
                Status = model.Status ?? string.Empty,
                UpdateInfo = model.UpdateInfo,
                Timestamp = model.SearchDate,
                FullData = model.ResultData ?? string.Empty
            };
        }
    }
}

