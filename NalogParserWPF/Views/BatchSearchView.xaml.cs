using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using NalogParserWPF.Models;
using NalogParserWPF.Services;
using NalogParserWPF.ViewModels;

namespace NalogParserWPF.Views
{
    /// <summary>
    /// Содержимое массового (группового) поиска по УНП.
    /// Используется и как вкладка главного окна, и как содержимое окна <see cref="BatchSearchWindow"/>,
    /// поэтому разметка и обработчики существуют в единственном экземпляре.
    /// </summary>
    public partial class BatchSearchView : UserControl
    {
        public BatchSearchView()
        {
            InitializeComponent();
        }

        private static INotificationService? Notifications =>
            App.ServiceProvider?.GetService<INotificationService>();

        private void View_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private async void View_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;

            string filePath = files[0];
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension != ".txt" && extension != ".csv" && extension != ".xlsx")
            {
                MessageBox.Show("Поддерживаются только файлы .txt, .csv и .xlsx.",
                    "Неподдерживаемый формат", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DataContext is BatchSearchViewModel viewModel)
            {
                await viewModel.LoadFileAsync(filePath);
            }
        }

        private void DgBatchResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGrid) return;

            DependencyObject? current = e.OriginalSource as DependencyObject;
            while (current != null && current is not DataGridCell)
            {
                current = VisualTreeHelper.GetParent(current);
            }

            if (current is not DataGridCell cell
                || cell.Column is not DataGridBoundColumn column
                || column.Binding is not Binding binding
                || cell.DataContext is not BatchResultItem item)
            {
                return;
            }

            var property = typeof(BatchResultItem).GetProperty(binding.Path.Path);
            string text = property?.GetValue(item)?.ToString() ?? string.Empty;

            if (string.IsNullOrEmpty(text))
            {
                Notifications?.ShowInfo("Ячейка пуста.");
                return;
            }

            try
            {
                Clipboard.SetText(text);
                Notifications?.ShowSuccess($"Скопировано: {text}");
            }
            catch (Exception ex)
            {
                Notifications?.ShowError($"Не удалось скопировать текст: {ex.Message}");
            }
        }
    }
}
