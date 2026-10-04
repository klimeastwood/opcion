using System;
using System.Collections.Generic;

namespace NalogParserWPF.Helpers
{
    /// <summary>
    /// Резервный (статический) русский словарь для данных государственной статистики Белстата.
    /// Основной источник русских подписей — справочник структуры данных
    /// (GET sdmx-api/indicator/datastructure/SDMX-JSON/{code}, см. Services/BelstatApiService):
    /// русские названия измерений берутся из схемы понятий, расшифровки значений — из классификаторов CL_*.
    /// Этот словарь применяется только тогда, когда справочник недоступен (нет сети, изменился ответ API)
    /// или когда в нём нет нужной подписи.
    /// </summary>
    public static class BelstatLocalizationHelper
    {
        /// <summary>Английские названия измерений → русские (резерв, если справочник недоступен).</summary>
        private static readonly Dictionary<string, string> DimensionNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Territory of the Republic of Belarus"] = "Территория Республики Беларусь",
            ["Type of area"] = "Тип местности",
            ["Unit of measurement"] = "Единица измерения",
            ["Units of measurement"] = "Единицы измерения",
            ["Indicator"] = "Показатель",
            ["Indicator name"] = "Наименование показателя",
            ["Value"] = "Значение",
            ["Period"] = "Период",
            ["Year"] = "Год",
            ["Month"] = "Месяц",
            ["Date"] = "Дата",
            ["Sex"] = "Пол",
            ["Age"] = "Возраст",
            ["Age group"] = "Возрастная группа",
            ["Region"] = "Область",
            ["District"] = "Район",
            ["City"] = "Город",
            ["Urban and rural area"] = "Городская и сельская местность",
            ["Type of ownership"] = "Форма собственности",
            ["Economic activity"] = "Вид экономической деятельности",
            ["Kind of economic activity"] = "Вид экономической деятельности",
            ["Level of education"] = "Уровень образования",
            ["Frequency"] = "Периодичность",
            ["Currency"] = "Валюта",
        };

        /// <summary>Английские расшифровки значений измерений → русские (резерв).</summary>
        private static readonly Dictionary<string, string> ValueNames = new(StringComparer.OrdinalIgnoreCase)
        {
            // Территории
            ["Republic of Belarus"] = "Республика Беларусь",
            ["Brest region"] = "Брестская область",
            ["Vitebsk region"] = "Витебская область",
            ["Gomel region"] = "Гомельская область",
            ["Grodno region"] = "Гродненская область",
            ["Minsk region"] = "Минская область",
            ["Mogilev region"] = "Могилевская область",
            ["Minsk city"] = "г. Минск",

            // Тип местности
            ["By all types"] = "Всего по типам местности",
            ["By all types of area"] = "Всего по типам местности",
            ["Urban area"] = "городская местность",
            ["Rural area"] = "сельская местность",
            ["Large cities"] = "большие города",
            ["Medium and small urban settlements"] = "средние и малые городские поселения",

            // Единицы измерения
            ["unit"] = "единица",
            ["units"] = "единиц",
            ["thousand units"] = "тысяч единиц",
            ["million units"] = "миллионов единиц",
            ["percent"] = "процентов",
            ["percentage"] = "процентов",
            ["per mille"] = "промилле",
            ["ruble"] = "рубль",
            ["rubles"] = "рублей",
            ["thousand rubles"] = "тысяч рублей",
            ["million rubles"] = "миллионов рублей",
            ["billion rubles"] = "миллиардов рублей",
            ["US dollar"] = "доллар США",
            ["US dollars"] = "долларов США",

            // Натуральные и физические единицы
            ["piece"] = "штука",
            ["pieces"] = "штук",
            ["thousand pieces"] = "тысяч штук",
            ["tonne"] = "тонна",
            ["tonnes"] = "тонн",
            ["thousand tonnes"] = "тысяч тонн",
            ["kilogram"] = "килограмм",
            ["kilograms"] = "килограммов",
            ["gram"] = "грамм",
            ["litre"] = "литр",
            ["litres"] = "литров",
            ["thousand litres"] = "тысяч литров",
            ["hectare"] = "гектар",
            ["hectares"] = "гектаров",
            ["square metre"] = "квадратный метр",
            ["square metres"] = "квадратных метров",
            ["thousand square metres"] = "тысяч квадратных метров",
            ["cubic metre"] = "кубический метр",
            ["cubic metres"] = "кубических метров",
            ["metre"] = "метр",
            ["metres"] = "метров",
            ["kilometre"] = "километр",
            ["kilometres"] = "километров",
            ["kilowatt-hour"] = "киловатт-час",
            ["kilowatt-hours"] = "киловатт-часов",
            ["gigawatt-hour"] = "гигаватт-час",
            ["gigawatt-hours"] = "гигаватт-часов",
            ["kilocalorie"] = "килокалория",
            ["kilocalories"] = "килокалорий",
            ["gigacalorie"] = "гигакалория",
            ["gigacalories"] = "гигакалорий",

            // Периодичность и служебные значения
            ["annual"] = "годовая",
            ["monthly"] = "месячная",
            ["quarterly"] = "квартальная",
            ["not specified"] = "не указано",
            ["total"] = "всего",
            ["Total"] = "всего",
        };

        /// <summary>
        /// Переводит английское название измерения на русский (если подобрана подпись).
        /// Неизвестное название возвращается без изменений, null остаётся null.
        /// </summary>
        public static string? TranslateDimensionName(string? name) => Translate(DimensionNames, name);

        /// <summary>
        /// Переводит английскую расшифровку значения измерения на русский (если подобрана подпись).
        /// Неизвестное значение возвращается без изменений, null остаётся null.
        /// </summary>
        public static string? TranslateValueName(string? name) => Translate(ValueNames, name);

        private static string? Translate(Dictionary<string, string> dictionary, string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return name;

            var trimmed = name.Trim();
            return dictionary.TryGetValue(trimmed, out var translated) ? translated : name;
        }
    }
}
