using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using NalogParserWPF.Helpers;
using NalogParserWPF.ViewModels;

namespace NalogParserWPF.Views
{
    /// <summary>
    /// Отдельное окно с таблицей результата вкладки «Госстатистика».
    /// Показывает тот же результат, что и таблица на вкладке (общий экземпляр
    /// <see cref="BelstatViewModel"/>), но без ограничений по ширине колонки вкладки:
    /// удобно для широких таблиц Белстата (много измерений) и экспорта.
    /// </summary>
    public partial class BelstatDataWindow : Window
    {
        public BelstatDataWindow(BelstatViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        /// <summary>Закрытие окна кнопкой «Закрыть».</summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        /// <summary>Закрытие окна клавишей Esc (как в остальных окнах приложения).</summary>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }

        /// <summary>
        /// Копирование значения ячейки в буфер обмена по двойному клику —
        /// то же поведение, что и в таблице на вкладке.
        /// </summary>
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataGridHelper.CopyCellContent(sender, e);
        }

        /// <summary>
        /// Таблица госстатистики строится из DataTable, колонки которой названы значениями
        /// измерений ответа SDMX-JSON. WPF формирует путь привязки из имени колонки, и символы
        /// вроде точки (например, «г. Минск») такую привязку ломают — значение ячейки молча
        /// не отображается. Поэтому путь переводится в индексную форму «[имя колонки]»,
        /// которая корректно работает для DataRowView с любыми именами; заголовком колонки
        /// остаётся имя из DataTable (оно уже уникально и содержит расшифровку измерения).
        /// </summary>
        private void BelstatDataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.Column is DataGridBoundColumn boundColumn)
            {
                boundColumn.Binding = new Binding($"[{e.PropertyName}]")
                {
                    Mode = BindingMode.OneWay
                };
                boundColumn.MinWidth = 90;
            }

            e.Column.Header = e.PropertyName;
        }
    }
}
