using System;
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
        public DateTime Timestamp { get; set; }
        public string FullData { get; set; } = string.Empty;

        public static HistoryItemViewModel CreateFromModel(SearchHistory model)
        {
            return new HistoryItemViewModel
            {
                Id = model.Id,
                Unp = model.Unp ?? string.Empty,
                Name = model.FullName ?? string.Empty,
                Type = model.SearchType ?? string.Empty,
                Status = model.Status ?? string.Empty,
                Timestamp = model.SearchDate,
                FullData = model.ResultData ?? string.Empty
            };
        }
    }
}
