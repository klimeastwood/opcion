using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Newtonsoft.Json;
using NalogParserWPF.Database;
using NalogParserWPF.Models;
using NalogParserWPF.Services;

namespace NalogParserWPF
{
    public partial class BatchSearchWindow : Window
    {
        private readonly NalogApiService _apiService;
        private readonly DatabaseHelper _dbHelper;
        private ObservableCollection<BatchResultItem> _results;
        private bool _isSearching = false;

        public BatchSearchWindow()
        {
            InitializeComponent();
            _apiService = new NalogApiService();
            _dbHelper = new DatabaseHelper();
            _results = new ObservableCollection<BatchResultItem>();
            dgBatchResults.ItemsSource = _results;
            UpdateResultsCount();
        }

        private async void BtnBatchSearch_Click(object sender, RoutedEventArgs e)
        {
            if (_isSearching) return;

            string input = txtBatchInput.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                MessageBox.Show("Введите УНП для поиска (по одному на строку).", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Разбираем строки
            var lines = input.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => x.Trim())
                            .Where(x => !string.IsNullOrEmpty(x))
                            .ToList();

            if (lines.Count == 0)
            {
                MessageBox.Show("Не найдено ни одного УНП для поиска.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Подтверждение
            var confirmResult = MessageBox.Show($"Найдено {lines.Count} УНП для поиска.\n\nПродолжить?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes) return;

            // Очищаем предыдущие результаты
            _results.Clear();
            btnBatchSearch.IsEnabled = false;
            btnBatchSearch.Content = "⏳ Поиск...";
            btnExportBatch.IsEnabled = false;
            _isSearching = true;
            lblStatus.Text = "";
            lblStatus.Foreground = Brushes.Black;

            try
            {
                int total = lines.Count;
                int successCount = 0;
                int notFoundCount = 0;
                int errorCount = 0;

                for (int i = 0; i < total; i++)
                {
                    string unp = lines[i];
                    lblProgress.Text = $"Обработка {i + 1} из {total}...";

                    try
                    {
                        var company = await _apiService.GetCompanyInfo(unp);

                        var item = new BatchResultItem
                        {
                            Index = i + 1,
                            Unp = unp,
                            FullName = company?.FullName ?? "Не найдено",
                            Status = company?.StatusText ?? "Не найден",
                            CheckDate = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
                            RawData = company != null ? JsonConvert.SerializeObject(company) : null
                        };

                        // Определяем цвет статуса
                        if (company != null)
                        {
                            if (company.StatusText == "Действующий")
                            {
                                item.StatusColor = "Green";
                                successCount++;
                            }
                            else
                            {
                                item.StatusColor = "Red";
                                successCount++;
                            }

                            // Сохраняем в историю
                            SaveToHistory(unp, company);
                        }
                        else
                        {
                            item.StatusColor = "Gray";
                            notFoundCount++;
                        }

                        _results.Add(item);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _results.Add(new BatchResultItem
                        {
                            Index = i + 1,
                            Unp = unp,
                            FullName = "Ошибка",
                            Status = "Ошибка запроса",
                            StatusColor = "Red",
                            CheckDate = DateTime.Now.ToString("dd.MM.yyyy HH:mm")
                        });
                    }

                    // Обновляем счетчики
                    UpdateResultsCount();
                    dgBatchResults.ScrollIntoView(dgBatchResults.Items[dgBatchResults.Items.Count - 1]);
                }

                // Итоговый статус
                lblStatus.Text = $"✅ Завершено: найдено {successCount}, не найдено {notFoundCount}, ошибок {errorCount}";
                lblStatus.Foreground = successCount > 0 ? Brushes.Green : Brushes.Red;
                btnExportBatch.IsEnabled = _results.Count > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выполнении поиска: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnBatchSearch.IsEnabled = true;
                btnBatchSearch.Content = "🔍 Начать поиск";
                _isSearching = false;
                lblProgress.Text = _results.Count > 0 ? $"Найдено {_results.Count} записей" : "Готов к работе";
            }
        }

        private void SaveToHistory(string unp, CompanyRow company)
        {
            try
            {
                var history = new SearchHistory
                {
                    SearchType = "Legal",
                    SearchParams = $"Групповой поиск: {unp}",
                    ResultData = JsonConvert.SerializeObject(company),
                    Unp = company?.Unp,
                    FullName = company?.FullName,
                    Status = company?.StatusText,
                    IsActive = company?.StatusText == "Действующий"
                };

                _dbHelper.AddHistory(history);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения в историю: {ex.Message}");
            }
        }

        private void UpdateResultsCount()
        {
            lblResultsCount.Text = _results.Count > 0 ? $"Всего: {_results.Count}" : "";
        }

        private void BtnLoadFile_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                Title = "Выберите файл со списком УНП"
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    string content = File.ReadAllText(openDialog.FileName, Encoding.UTF8);
                    txtBatchInput.Text = content;
                    lblProgress.Text = $"Загружен файл: {Path.GetFileName(openDialog.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке файла: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnClearBatch_Click(object sender, RoutedEventArgs e)
        {
            txtBatchInput.Clear();
            _results.Clear();
            UpdateResultsCount();
            btnExportBatch.IsEnabled = false;
            lblProgress.Text = "Готов к работе";
            lblStatus.Text = "";
        }

        private void BtnExportBatch_Click(object sender, RoutedEventArgs e)
        {
            if (_results.Count == 0)
            {
                MessageBox.Show("Нет данных для экспорта.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV файлы (*.csv)|*.csv|Текстовые файлы (*.txt)|*.txt",
                FileName = $"Групповой_поиск_{DateTime.Now:yyyy-MM-dd_HH-mm}",
                DefaultExt = "csv"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    ExportToCsv(saveDialog.FileName);
                    MessageBox.Show($"Файл успешно сохранен!\n{saveDialog.FileName}", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportToCsv(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("\"№\",\"УНП\",\"Наименование\",\"Статус\",\"Дата проверки\"");

            foreach (var item in _results)
            {
                sb.AppendLine($"\"{item.Index}\",\"{item.Unp}\",\"{item.FullName}\",\"{item.Status}\",\"{item.CheckDate}\"");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private void DgBatchResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgBatchResults.SelectedItem is BatchResultItem item)
            {
                if (!string.IsNullOrEmpty(item.RawData))
                {
                    try
                    {
                        var company = JsonConvert.DeserializeObject<CompanyRow>(item.RawData);
                        if (company != null)
                        {
                            // Создаем HistoryItem для отображения
                            var historyItem = new HistoryItem
                            {
                                Id = 0,
                                SearchDate = item.CheckDate,
                                Type = "Юрлицо/ИП",
                                SearchParams = $"УНП: {item.Unp}",
                                Unp = item.Unp,
                                FullName = item.FullName,
                                Status = item.Status,
                                StatusColor = item.StatusColor,
                                IsActive = item.Status == "Действующий"
                            };

                            var detailWindow = new HistoryDetailWindow(historyItem, item.RawData);
                            detailWindow.ShowDialog();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при открытии деталей: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Данные для этой записи недоступны.", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
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

    // Модель для отображения в таблице группового поиска
    public class BatchResultItem : INotifyPropertyChanged
    {
        public int Index { get; set; }
        public string Unp { get; set; }
        public string FullName { get; set; }
        public string Status { get; set; }
        public string StatusColor { get; set; }
        public string CheckDate { get; set; }
        public string RawData { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}