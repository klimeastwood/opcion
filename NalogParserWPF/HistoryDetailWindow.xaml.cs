using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Newtonsoft.Json;
using NalogParserWPF.Models;

namespace NalogParserWPF
{
    public partial class HistoryDetailWindow : Window
    {
        private CompanyRow _currentCompany;
        private string _searchParams;
        private string _searchDate;

        public HistoryDetailWindow(HistoryItem historyItem, string resultData)
        {
            InitializeComponent();

            _searchDate = historyItem.SearchDate;
            _searchParams = historyItem.SearchParams;

            // Заполняем информацию о записи
            tbDate.Text = historyItem.SearchDate;
            tbType.Text = historyItem.Type == "Legal" ? "Юрлицо/ИП" : "Физлицо";

            // Статус с цветом
            tbStatus.Text = historyItem.Status;
            if (historyItem.Status == "Действующий" || historyItem.Status == "Активный")
            {
                tbStatus.Foreground = Brushes.Green;
            }
            else if (historyItem.Status == "Ликвидирован" || historyItem.Status == "Исключен" || historyItem.Status == "Недействующий")
            {
                tbStatus.Foreground = Brushes.Red;
            }
            else
            {
                tbStatus.Foreground = Brushes.Orange;
            }

            tbParams.Text = historyItem.SearchParams;

            // Парсим результат
            if (!string.IsNullOrEmpty(resultData))
            {
                try
                {
                    _currentCompany = JsonConvert.DeserializeObject<CompanyRow>(resultData);
                    if (_currentCompany != null)
                    {
                        DisplayCompanyInfo(_currentCompany);
                    }
                    else
                    {
                        txtResult.Text = "❌ Данные не найдены в сохраненной записи.";
                    }
                }
                catch
                {
                    txtResult.Text = "❌ Ошибка при разборе сохраненных данных.\n\n" + resultData;
                }
            }
            else
            {
                txtResult.Text = "❌ Данные отсутствуют.";
            }
        }

        private void DisplayCompanyInfo(CompanyRow company)
        {
            txtResult.Clear();

            txtResult.AppendText("📋 ИНФОРМАЦИЯ О КОНТРАГЕНТЕ\n");
            txtResult.AppendText(new string('─', 50) + "\n\n");

            txtResult.AppendText($"УНП: {company.Unp}\n");
            txtResult.AppendText($"Полное наименование: {company.FullName}\n");
            txtResult.AppendText($"Краткое наименование: {company.ShortName}\n");
            txtResult.AppendText($"Адрес: {company.Address}\n");
            txtResult.AppendText($"Дата регистрации: {company.RegistrationDate}\n");
            txtResult.AppendText($"Инспекция МНС: {company.InspectorateName} (код {company.InspectorateCode})\n");

            // Статус
            txtResult.AppendText($"Статус: ");
            if (company.StatusText == "Действующий")
            {
                txtResult.Foreground = Brushes.Green;
                txtResult.AppendText($"{company.StatusText}");
                txtResult.Foreground = Brushes.Black;
                txtResult.AppendText($" (код {company.StatusCode})\n");
            }
            else
            {
                txtResult.Foreground = Brushes.Red;
                txtResult.AppendText($"{company.StatusText}");
                txtResult.Foreground = Brushes.Black;
                txtResult.AppendText($" (код {company.StatusCode})\n");
            }

            if (!string.IsNullOrEmpty(company.LiquidationDate))
            {
                txtResult.AppendText($"Дата ликвидации: {company.LiquidationDate}\n");
                txtResult.AppendText($"Причина: {company.LiquidationReason}\n");
            }

            // Дополнительные поля для физлиц
            if (!string.IsNullOrEmpty(company.BirthDate))
                txtResult.AppendText($"Дата рождения: {company.BirthDate}\n");
            if (!string.IsNullOrEmpty(company.DocumentNumber))
                txtResult.AppendText($"Документ: {company.DocumentNumber}\n");
            if (!string.IsNullOrEmpty(company.IdNumber))
                txtResult.AppendText($"Идентификационный номер: {company.IdNumber}\n");

            txtResult.AppendText("\n" + new string('─', 50) + "\n");

            if (company.StatusText == "Действующий")
            {
                txtResult.Foreground = Brushes.Green;
                txtResult.AppendText("\n✅ КОНТРАГЕНТ ДЕЙСТВУЕТ");
                txtResult.Foreground = Brushes.Black;
            }
            else
            {
                txtResult.Foreground = Brushes.Red;
                txtResult.AppendText("\n⚠️ КОНТРАГЕНТ НЕ ДЕЙСТВУЕТ — будьте осторожны!");
                txtResult.Foreground = Brushes.Black;
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

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtResult.Text)) return;
            Clipboard.SetText(txtResult.Text);
            MessageBox.Show("Данные скопированы в буфер обмена!", "Успех",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_currentCompany == null)
            {
                MessageBox.Show("Нет данных для экспорта.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Формируем имя файла
            string fileName = $"История_{_currentCompany.Unp}";
            if (!string.IsNullOrEmpty(_currentCompany.ShortName))
                fileName += $"_{_currentCompany.ShortName}";
            else if (!string.IsNullOrEmpty(_currentCompany.FullName))
                fileName += $"_{_currentCompany.FullName}";
            fileName += $"_{DateTime.Now:yyyy-MM-dd}";

            // Очищаем от недопустимых символов
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF файлы (*.pdf)|*.pdf",
                FileName = fileName,
                DefaultExt = "pdf"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    btnExportPdf.IsEnabled = false;
                    btnExportPdf.Content = "⏳ Экспорт...";

                    ExportToPdf(saveDialog.FileName, _currentCompany);

                    MessageBox.Show($"PDF успешно сохранен!\n{saveDialog.FileName}", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    btnExportPdf.IsEnabled = true;
                    btnExportPdf.Content = "📄 Экспорт в PDF";
                }
            }
        }

        private void ExportToPdf(string filePath, CompanyRow company)
        {
            string html = $@"
            <html>
            <head><meta charset='UTF-8'>
            <style>
                body {{ font-family: Arial, sans-serif; padding: 40px; max-width: 800px; margin: 0 auto; }}
                h1 {{ color: #008080; border-bottom: 2px solid #008080; padding-bottom: 10px; }}
                .info {{ font-size: 14px; line-height: 1.8; }}
                .label {{ font-weight: bold; color: #008080; }}
                .active {{ color: green; font-weight: bold; }}
                .inactive {{ color: red; font-weight: bold; }}
                .footer {{ margin-top: 30px; padding-top: 10px; border-top: 1px solid #ccc; color: #666; font-size: 12px; }}
                .result {{ margin-top: 20px; padding: 15px; background: #f0f8f8; border-left: 4px solid #008080; }}
                .history-info {{ background: #f5f5f5; padding: 10px; border-radius: 5px; margin-bottom: 20px; font-size: 13px; }}
            </style>
            </head>
            <body>
                <h1>📋 Информация о контрагенте</h1>
                <div class='history-info'>
                    <p><strong>Дата поиска:</strong> {_searchDate}</p>
                    <p><strong>Параметры поиска:</strong> {_searchParams}</p>
                </div>
                <div class='info'>
                    <p><span class='label'>УНП:</span> {company.Unp}</p>
                    <p><span class='label'>Полное наименование:</span> {company.FullName}</p>
                    <p><span class='label'>Краткое наименование:</span> {company.ShortName}</p>
                    <p><span class='label'>Адрес:</span> {company.Address}</p>
                    <p><span class='label'>Дата регистрации:</span> {company.RegistrationDate}</p>
                    <p><span class='label'>Инспекция МНС:</span> {company.InspectorateName} (код {company.InspectorateCode})</p>
                    <p><span class='label'>Статус:</span> <span class='{(company.StatusText == "Действующий" ? "active" : "inactive")}'>{company.StatusText}</span> (код {company.StatusCode})</p>
                    {(string.IsNullOrEmpty(company.LiquidationDate) ? "" : $"<p><span class='label'>Дата ликвидации:</span> {company.LiquidationDate}</p><p><span class='label'>Причина:</span> {company.LiquidationReason}</p>")}
                    {(string.IsNullOrEmpty(company.BirthDate) ? "" : $"<p><span class='label'>Дата рождения:</span> {company.BirthDate}</p>")}
                    {(string.IsNullOrEmpty(company.DocumentNumber) ? "" : $"<p><span class='label'>Документ:</span> {company.DocumentNumber}</p>")}
                    {(string.IsNullOrEmpty(company.IdNumber) ? "" : $"<p><span class='label'>Идентификационный номер:</span> {company.IdNumber}</p>")}
                </div>
                <div class='result'>
                    {(company.StatusText == "Действующий" ? "✅ КОМПАНИЯ ДЕЙСТВУЕТ — можно работать" : "⚠️ КОМПАНИЯ НЕ ДЕЙСТВУЕТ — будьте осторожны!")}
                </div>
                <div class='footer'>
                    Отчет сгенерирован: {DateTime.Now:dd.MM.yyyy HH:mm}<br/>
                    Источник данных: grp.nalog.gov.by
                </div>
            </body>
            </html>";

            string tempFile = Path.GetTempFileName() + ".html";
            File.WriteAllText(tempFile, html, Encoding.UTF8);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = tempFile,
                UseShellExecute = true
            });
        }
    }
}