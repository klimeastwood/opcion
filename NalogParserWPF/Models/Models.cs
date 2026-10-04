using System;
using Newtonsoft.Json;

namespace NalogParserWPF.Models
{
    // Модель ответа от API
    public class NalogResponse
    {
        [JsonProperty("row", DefaultValueHandling = DefaultValueHandling.Populate)]
        public CompanyRow Row { get; set; } = new();
    }

    public class CompanyRow
    {
        [JsonProperty("vunp")]
        public string Unp { get; set; } = string.Empty;

        [JsonProperty("vnaimp")]
        public string FullName { get; set; } = string.Empty;

        [JsonProperty("vnaimk")]
        public string ShortName { get; set; } = string.Empty;

        [JsonProperty("vpadres")]
        public string Address { get; set; } = string.Empty;

        [JsonProperty("dreg")]
        public string RegistrationDate { get; set; } = string.Empty;

        [JsonProperty("nmns")]
        public string InspectionCode { get; set; } = string.Empty;

        [JsonProperty("vmns")]
        public string InspectionName { get; set; } = string.Empty;

        [JsonProperty("ckodsost")]
        public string StatusCode { get; set; } = string.Empty;

        [JsonProperty("vkods")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("dlikv")]
        public string StatusChangeDate { get; set; } = string.Empty;

        [JsonProperty("vlikv")]
        public string LiquidationDate { get; set; } = string.Empty;
    }

    // Модель для хранения в БД
    public class SearchHistory
    {
        public int Id { get; set; }
        public DateTime SearchDate { get; set; }
        public string SearchType { get; set; } = string.Empty; // "Legal" или "Physical"
        public string SearchParams { get; set; } = string.Empty; // JSON с параметрами поиска
        public string ResultData { get; set; } = string.Empty; // JSON с результатом
        public string Unp { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string PreviousStatus { get; set; } = string.Empty;
        public DateTime? LastChecked { get; set; } // Добавляем поле
        public string? UpdateInfo { get; set; } // Изменения, которые произошли при автопроверке
    }

    // Параметры поиска для юрлиц
    public class LegalSearchParams
    {
        public string? Unp { get; set; }
    }

    // Параметры поиска для физлиц
    public class PhysicalSearchParams
    {
        public string? Unp { get; set; }
    }
}