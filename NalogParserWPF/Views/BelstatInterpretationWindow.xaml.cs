using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NalogParserWPF.Helpers;
using NalogParserWPF.ViewModels;

namespace NalogParserWPF.Views
{
    /// <summary>
    /// Отдельное окно «📊 Интерпретация статистических данных»: разбор ряда показателя
    /// вкладки «Госстатистика» (простая и расширенная интерпретация) для подготовки
    /// представлений об устранении причин и условий, способствовавших совершению
    /// преступлений. Окно создаётся контейнером DI, владелец передаёт ему таблицу
    /// результата вкладки — исходные данные не изменяются.
    /// </summary>
    public partial class BelstatInterpretationWindow : Window
    {
        public BelstatInterpretationWindow(BelstatInterpretationViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // Сортировка по клику на заголовок колонки и фильтр по тексту поля «🔎 Фильтр:»
            // над таблицами обеих вкладок — простой и расширенной интерпретации.
            AttachTableFeatures(SimplePointsGrid, SimpleFilterBox);
            AttachTableFeatures(AdvancedPointsGrid, AdvancedFilterBox);
        }

        /// <summary>
        /// Подключает к таблице сортировку по клику на заголовок колонки и фильтрацию
        /// по тексту поля «🔎 Фильтр:» над этой таблицей.
        /// </summary>
        private static void AttachTableFeatures(DataGrid grid, TextBox filterBox)
        {
            DataGridSortHelper.AttachSorting(grid);
            DataGridFilterHelper.AttachFilter(grid, filterBox);
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
        /// то же поведение, что и в таблице результата вкладки.
        /// </summary>
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataGridHelper.CopyCellContent(sender, e);
        }
    }
}
