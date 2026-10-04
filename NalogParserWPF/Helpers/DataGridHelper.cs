using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using NalogParserWPF.Services;

namespace NalogParserWPF.Helpers
{
    /// <summary>
    /// Копирует содержимое ячейки DataGrid в буфер обмена по двойному клику.
    /// Поддерживает связанные колонки (DataGridTextColumn и т.п.) и шаблонные с текстом.
    /// </summary>
    public static class DataGridHelper
    {
        public static void CopyCellContent(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGrid dataGrid) return;

            DependencyObject? current = e.OriginalSource as DependencyObject;
            TextBlock? clickedText = null;
            bool overButton = false;
            while (current != null && current is not DataGridCell)
            {
                if (current is Button)
                    overButton = true;
                if (current is TextBlock textBlock)
                    clickedText = textBlock;
                current = VisualTreeHelper.GetParent(current);
            }

            // Клик вне ячейки (заголовки, пустое место) или по кнопке не копирует текст.
            if (current is not DataGridCell cell || overButton)
                return;

            string? text = GetCellText(cell, clickedText);
            if (string.IsNullOrEmpty(text))
            {
                // Пустое значение имеет смысл только в связанных текстовых колонках.
                if (cell.Column is DataGridBoundColumn)
                    Notify()?.ShowInfo("Ячейка пуста.");
                return;
            }

            try
            {
                Clipboard.SetText(text);
                Notify()?.ShowSuccess($"Скопировано: {text}");
            }
            catch (Exception ex)
            {
                Notify()?.ShowError($"Не удалось скопировать текст: {ex.Message}");
            }
        }

        private static string? GetCellText(DataGridCell cell, TextBlock? clickedText)
        {
            // Текстовые колонки содержат отформатированный TextBlock (с учётом StringFormat).
            if (cell.Content is TextBlock contentBlock)
                return contentBlock.Text;
            if (cell.Content is string content)
                return content;

            // Текст под курсором в шаблонной колонке.
            if (clickedText != null)
                return clickedText.Text;

            // Связанная колонка: читаем значение из DataContext по пути привязки.
            if (cell.Column is DataGridBoundColumn bound
                && bound.Binding is Binding binding
                && cell.DataContext != null)
            {
                var property = cell.DataContext.GetType().GetProperty(binding.Path.Path);
                return property?.GetValue(cell.DataContext)?.ToString();
            }

            // Шаблонная колонка без кнопок: ищем текст в содержимом ячейки.
            return HasButton(cell) ? null : FindVisualChild<TextBlock>(cell)?.Text;
        }

        private static bool HasButton(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Button)
                    return true;
                if (HasButton(child))
                    return true;
            }

            return false;
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                    return match;

                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                    return descendant;
            }

            return null;
        }

        private static INotificationService? Notify() =>
            App.ServiceProvider?.GetService<INotificationService>();
    }
}
