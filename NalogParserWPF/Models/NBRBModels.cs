using System;
using Newtonsoft.Json;

namespace NalogParserWPF.Models
{
    /// <summary>
    /// Валюта из справочника НБ РБ (метод /exrates/currencies).
    /// </summary>
    public class Currency
    {
        [JsonProperty("Cur_ID")]
        public int Cur_ID { get; set; }

        [JsonProperty("Cur_ParentID")]
        public int Cur_ParentID { get; set; }

        [JsonProperty("Cur_Code")]
        public string Cur_Code { get; set; } = string.Empty;

        [JsonProperty("Cur_Abbreviation")]
        public string Cur_Abbreviation { get; set; } = string.Empty;

        [JsonProperty("Cur_Name")]
        public string Cur_Name { get; set; } = string.Empty;

        [JsonProperty("Cur_Name_Bel")]
        public string Cur_Name_Bel { get; set; } = string.Empty;

        [JsonProperty("Cur_Name_Eng")]
        public string Cur_Name_Eng { get; set; } = string.Empty;

        [JsonProperty("Cur_QuotName")]
        public string Cur_QuotName { get; set; } = string.Empty;

        [JsonProperty("Cur_QuotName_Bel")]
        public string Cur_QuotName_Bel { get; set; } = string.Empty;

        [JsonProperty("Cur_QuotName_Eng")]
        public string Cur_QuotName_Eng { get; set; } = string.Empty;

        [JsonProperty("Cur_NameMulti")]
        public string Cur_NameMulti { get; set; } = string.Empty;

        [JsonProperty("Cur_Name_BelMulti")]
        public string Cur_Name_BelMulti { get; set; } = string.Empty;

        [JsonProperty("Cur_Name_EngMulti")]
        public string Cur_Name_EngMulti { get; set; } = string.Empty;

        [JsonProperty("Cur_Scale")]
        public int Cur_Scale { get; set; }

        [JsonProperty("Cur_Periodicity")]
        public int Cur_Periodicity { get; set; }

        [JsonProperty("Cur_DateStart")]
        public DateTime Cur_DateStart { get; set; }

        [JsonProperty("Cur_DateEnd")]
        public DateTime Cur_DateEnd { get; set; }

        /// <summary>
        /// Отображаемое имя для ComboBox: «USD — Доллар США».
        /// </summary>
        [JsonIgnore]
        public string DisplayName => $"{Cur_Abbreviation} — {Cur_Name}";
    }

    /// <summary>
    /// Официальный курс белорусского рубля к иностранной валюте на дату.
    /// </summary>
    public class Rate
    {
        [JsonProperty("Cur_ID")]
        public int Cur_ID { get; set; }

        [JsonProperty("Cur_Code")]
        public string Cur_Code { get; set; } = string.Empty;

        [JsonProperty("Date")]
        public DateTime Date { get; set; }

        [JsonProperty("Cur_Abbreviation")]
        public string Cur_Abbreviation { get; set; } = string.Empty;

        [JsonProperty("Cur_Scale")]
        public int Cur_Scale { get; set; }

        [JsonProperty("Cur_Name")]
        public string Cur_Name { get; set; } = string.Empty;

        [JsonProperty("Cur_OfficialRate")]
        public decimal Cur_OfficialRate { get; set; }

        /// <summary>
        /// Курс за одну единицу валюты (Cur_OfficialRate / Cur_Scale).
        /// </summary>
        [JsonIgnore]
        public decimal UnitRate => Cur_Scale > 0 ? Cur_OfficialRate / Cur_Scale : Cur_OfficialRate;
    }

    /// <summary>
    /// Краткая запись курса (метод /exrates/rates/dynamics).
    /// </summary>
    public class RateShort
    {
        [JsonProperty("Cur_ID")]
        public int Cur_ID { get; set; }

        [JsonProperty("Date")]
        public DateTime Date { get; set; }

        [JsonProperty("Cur_OfficialRate")]
        public decimal Cur_OfficialRate { get; set; }
    }

    /// <summary>
    /// Запись истории запросов курсов валют.
    /// </summary>
    public class ExchangeHistoryItem
    {
        public int Id { get; set; }

        public string CurrencyCode { get; set; } = string.Empty;

        public string CurrencyName { get; set; } = string.Empty;

        public DateTime Date { get; set; }

        public decimal Rate { get; set; }

        public int Scale { get; set; }

        public DateTime Timestamp { get; set; }
    }
}
