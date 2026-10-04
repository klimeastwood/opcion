using System.ComponentModel;

namespace NalogParserWPF.Models
{
    // Модель строки результатов группового поиска
    public class BatchResultItem : INotifyPropertyChanged
    {
        public int Index { get; set; }
        public string Query { get; set; } = string.Empty;
        public string Unp { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Inspection { get; set; } = string.Empty;
        public string RegistrationDate { get; set; } = string.Empty;
        public string CheckDate { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public string? RawData { get; set; }

        // Обратная совместимость с экспортом в PDF (BuildBatchPage)
        public string FullName => Name;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
