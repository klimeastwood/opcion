using System;
using Newtonsoft.Json;

namespace NalogParserWPF.Models
{
    // Модель ответа от API
    public class NalogResponse
    {
        [JsonProperty("row")]
        public CompanyRow Row { get; set; }
    }

    public class CompanyRow
    {
        [JsonProperty("vunp")]
        public string Unp { get; set; }

        [JsonProperty("vnaimp")]
        public string FullName { get; set; }

        [JsonProperty("vnaimk")]
        public string ShortName { get; set; }

        [JsonProperty("vpadres")]
        public string Address { get; set; }

        [JsonProperty("dreg")]
        public string RegistrationDate { get; set; }

        [JsonProperty("nmns")]
        public string InspectorateCode { get; set; }

        [JsonProperty("vmns")]
        public string InspectorateName { get; set; }

        [JsonProperty("ckodsost")]
        public string StatusCode { get; set; }

        [JsonProperty("vkods")]
        public string StatusText { get; set; }

        [JsonProperty("dlikv")]
        public string LiquidationDate { get; set; }

        [JsonProperty("vlikv")]
        public string LiquidationReason { get; set; }

        // Дополнительные поля для физлиц
        [JsonProperty("vname")]
        public string FirstName { get; set; }

        [JsonProperty("vpatr")]
        public string Patronymic { get; set; }

        [JsonProperty("dbirth")]
        public string BirthDate { get; set; }

        [JsonProperty("docnum")]
        public string DocumentNumber { get; set; }

        [JsonProperty("idnum")]
        public string IdNumber { get; set; }
    }

    // Модель для хранения в БД
    public class SearchHistory
    {
        public int Id { get; set; }
        public DateTime SearchDate { get; set; }
        public string SearchType { get; set; } // "Legal" или "Physical"
        public string SearchParams { get; set; } // JSON с параметрами поиска
        public string ResultData { get; set; } // JSON с результатом
        public string Unp { get; set; }
        public string FullName { get; set; }
        public string Status { get; set; }
        public bool IsActive { get; set; }
        public string PreviousStatus { get; set; }
        public DateTime? LastChecked { get; set; } // Добавляем поле
    }

    // Модель для отображения в истории
    public class HistoryItem
    {
        public int Id { get; set; }
        public string SearchDate { get; set; }
        public string Type { get; set; }
        public string SearchParams { get; set; }
        public string FullName { get; set; }
        public string Unp { get; set; }
        public string Status { get; set; }
        public string StatusColor { get; set; }
        public bool IsActive { get; set; }
        public string PreviousStatus { get; set; }
    }

    // Параметры поиска для юрлиц
    public class LegalSearchParams
    {
        public string Unp { get; set; }
        public string Name { get; set; }
        public bool UseWildcard { get; set; }
    }

    // Параметры поиска для физлиц
    public class PhysicalSearchParams
    {
        public string Unp { get; set; }
        public string Fio { get; set; }
        public string Passport { get; set; }
        public string IdNumber { get; set; }
        public bool UseWildcard { get; set; }
    }
}