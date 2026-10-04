using System;
using System.Text;
using Microsoft.Extensions.Logging;
using NalogParserWPF.Helpers;
using NalogParserWPF.Models;

namespace NalogParserWPF.Services
{
    public class ResultFormatter : IResultFormatter
    {
        private const string NotSpecified = "Не указано";

        private readonly ILogger<ResultFormatter> _logger;

        public ResultFormatter(ILogger<ResultFormatter> logger)
        {
            _logger = logger;
        }

        public string FormatCompanyInfo(CompanyRow company)
        {
            if (company == null)
                throw new ArgumentNullException(nameof(company));

            _logger.LogInformation("Форматирование подробной информации о компании {Unp}", company.Unp);

            var sb = new StringBuilder();
            sb.AppendLine("ИНФОРМАЦИЯ О КОНТРАГЕНТЕ");
            sb.AppendLine(new string('─', 50));
            sb.AppendLine();
            AppendCompanyFields(sb, company);
            sb.AppendLine();
            sb.AppendLine(new string('─', 50));
            sb.AppendLine(IsActive(company.Status) ? "КОНТРАГЕНТ ДЕЙСТВУЕТ" : "ВНИМАНИЕ! КОНТРАГЕНТ НЕ ДЕЙСТВУЕТ — будьте осторожны!");
            return sb.ToString();
        }

        public string FormatHistoryEntry(SearchHistory entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            _logger.LogInformation("Форматирование записи истории #{Id}", entry.Id);

            var sb = new StringBuilder();
            sb.AppendLine("ИНФОРМАЦИЯ О КОНТРАГЕНТЕ");
            sb.AppendLine(new string('─', 50));
            sb.AppendLine();
            sb.AppendLine($"Дата поиска: {entry.SearchDate:dd.MM.yyyy HH:mm}");
            sb.AppendLine($"Тип: {(entry.SearchType == "Legal" ? "Юрлицо/ИП" : "Физлицо")}");
            sb.AppendLine($"УНП: {Value(entry.Unp)}");
            sb.AppendLine($"Наименование: {Value(entry.FullName)}");
            sb.AppendLine($"Параметры поиска: {Value(entry.SearchParams)}");
            sb.AppendLine();

            var company = TryParseCompany(entry.ResultData);
            if (company == null)
            {
                sb.AppendLine("Данные не найдены в сохраненной записи.");
                return sb.ToString();
            }

            AppendCompanyFields(sb, company);
            sb.AppendLine();
            sb.AppendLine(new string('─', 50));
            sb.AppendLine(IsActive(company.Status) ? "КОНТРАГЕНТ ДЕЙСТВУЕТ" : "ВНИМАНИЕ! КОНТРАГЕНТ НЕ ДЕЙСТВУЕТ — будьте осторожны!");
            sb.AppendLine();
            sb.AppendLine("Информация временно недоступна. Подробнее на сайте grp.nalog.gov.by");
            return sb.ToString();
        }

        private static void AppendCompanyFields(StringBuilder sb, CompanyRow company)
        {
            sb.AppendLine($"УНП: {Value(company.Unp)}");
            sb.AppendLine($"Полное наименование: {Value(company.FullName)}");
            sb.AppendLine($"Краткое наименование: {Value(company.ShortName)}");
            sb.AppendLine($"Адрес: {Value(company.Address)}");
            sb.AppendLine($"Дата регистрации: {DateValue(company.RegistrationDate)}");
            sb.AppendLine($"Инспекция МНС: {InspectionText(company.InspectionName, company.InspectionCode)}");
            sb.AppendLine($"Статус: {StatusText(company.Status, company.StatusCode)}");
            sb.AppendLine($"Дата изменения статуса: {DateValue(company.StatusChangeDate)}");
            if (!string.IsNullOrEmpty(company.LiquidationDate))
                sb.AppendLine($"Дата ликвидации: {DateValue(company.LiquidationDate)}");
        }

        private static bool IsActive(string? status) =>
            status == "Действующий" || status == "Активный";

        private static string Value(string? value) =>
            string.IsNullOrWhiteSpace(value) ? NotSpecified : value;

        private static string DateValue(string? value)
        {
            string formatted = StringHelper.FormatDate(value ?? string.Empty);
            return string.IsNullOrEmpty(formatted) ? NotSpecified : formatted;
        }

        private static string InspectionText(string? name, string? code)
        {
            string value = Value(name);
            string codeValue = Value(code);
            return codeValue == NotSpecified ? value : $"{value} (код {codeValue})";
        }

        private static string StatusText(string? status, string? code)
        {
            string value = Value(status);
            string codeValue = Value(code);
            return codeValue == NotSpecified ? value : $"{value} (код {codeValue})";
        }

        private static CompanyRow? TryParseCompany(string? resultData)
        {
            if (string.IsNullOrWhiteSpace(resultData)) return null;
            try
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<NalogResponse>(resultData)?.Row
                       ?? Newtonsoft.Json.JsonConvert.DeserializeObject<CompanyRow>(resultData);
            }
            catch
            {
                return null;
            }
        }
    }
}
