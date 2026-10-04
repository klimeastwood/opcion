using System.Windows;
using System.Windows.Input;
using NalogParserWPF.ViewModels;

namespace NalogParserWPF
{
    public partial class BatchSearchWindow : Window
    {
        public BatchSearchWindow(BatchSearchViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseRequested += (_, _) => Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void BatchSearchView_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}
