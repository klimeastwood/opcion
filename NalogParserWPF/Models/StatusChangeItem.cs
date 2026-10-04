using System.ComponentModel;

namespace NalogParserWPF.Models
{
    // Модель для отображения изменений
    public class StatusChangeItem : INotifyPropertyChanged
    {
        public string Unp { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string OldStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public string CheckDate { get; set; } = string.Empty;

        public string OldStatusColor
        {
            get
            {
                return OldStatus == "Действующий" ? "Green" : "Red";
            }
        }

        public string NewStatusColor
        {
            get
            {
                return NewStatus == "Действующий" ? "Green" : "Red";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
