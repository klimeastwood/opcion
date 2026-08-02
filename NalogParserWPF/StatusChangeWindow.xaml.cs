using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace NalogParserWPF
{
    public partial class StatusChangeWindow : Window
    {
        public StatusChangeWindow(ObservableCollection<StatusChangeItem> changes)
        {
            InitializeComponent();
            dgChanges.ItemsSource = changes;

            if (changes.Count == 0)
            {
                dgChanges.Visibility = Visibility.Collapsed;
                // Можно показать сообщение "Изменений не обнаружено"
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }
    }

    // Модель для отображения изменений
    public class StatusChangeItem : INotifyPropertyChanged
    {
        public string Unp { get; set; }
        public string FullName { get; set; }
        public string OldStatus { get; set; }
        public string NewStatus { get; set; }
        public string CheckDate { get; set; }

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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}