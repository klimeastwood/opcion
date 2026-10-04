using System.Windows;
using System.Windows.Input;
using NalogParserWPF.Helpers;
using NalogParserWPF.ViewModels;

namespace NalogParserWPF
{
    public partial class StatusChangeWindow : Window
    {
        public StatusChangeWindow(StatusChangeViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseRequested += (_, _) => Close();
        }

        private void ChangesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataGridHelper.CopyCellContent(sender, e);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
