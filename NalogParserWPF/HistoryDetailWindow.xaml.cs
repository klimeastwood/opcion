using System.Windows;
using System.Windows.Input;
using NalogParserWPF.ViewModels;

namespace NalogParserWPF
{
    public partial class HistoryDetailWindow : Window
    {
        public HistoryDetailWindow(HistoryDetailViewModel viewModel)
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
    }
}
