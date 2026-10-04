using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NalogParserWPF.Models
{
    /// <summary>
    /// Рубрика (тематический раздел) каталога Белстата.
    /// Используется как узел дерева (rubricTreeNodeGet) и как элемент плоского списка для ComboBox.
    /// </summary>
    public class BelstatRubric
    {
        [JsonProperty("code")]
        public string Code { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("children")]
        public List<BelstatRubric> Children { get; set; } = new();

        /// <summary>Код родительской рубрики (заполняется при обходе дерева, в JSON отсутствует).</summary>
        [JsonIgnore]
        public string? ParentCode { get; set; }

        /// <summary>Уровень вложенности (0 — верхний уровень), используется для отступа в списке.</summary>
        [JsonIgnore]
        public int Depth { get; set; }

        /// <summary>
        /// Иерархический номер рубрики в дереве («1», «1.2», «1.2.3»).
        /// Заполняется при разворачивании дерева (BelstatApiService.FlattenRubrics), в JSON отсутствует.
        /// </summary>
        [JsonIgnore]
        public string? Number { get; set; }

        /// <summary>Отображаемое имя с иерархическим номером и отступом по уровню вложенности — для ComboBox.</summary>
        [JsonIgnore]
        public string DisplayName =>
            new string(' ', Depth * 3)
            + (string.IsNullOrWhiteSpace(Number) ? string.Empty : Number + ". ")
            + (string.IsNullOrWhiteSpace(Name) ? "(без названия)" : Name);

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// Показатель государственной статистики (карточка из каталога indicatorSearch / rubricIndicatorsGet).
    /// </summary>
    public class BelstatIndicator
    {
        [JsonProperty("code")]
        public string Code { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Код рубрики, к которой относится показатель (для поиска по каталогу не заполняется).</summary>
        [JsonIgnore]
        public string RubricCode { get; set; } = string.Empty;

        /// <summary>
        /// Ключевые слова из каталога Белстата (поле key_words) — используются для полнотекстового
        /// поиска показателей в <c>SearchIndicatorsAsync</c>. В ответах SDMX-JSON отсутствуют.
        /// </summary>
        [JsonIgnore]
        public string? KeyWords { get; set; }

        [JsonIgnore]
        public string DisplayName => $"{Code} — {Name}";

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// Ответ каталога показателей (POST indicator/indicatorSearch, GET rubric/rubricIndicatorsGet).
    /// </summary>
    public class BelstatItemsResponse
    {
        [JsonProperty("items")]
        public List<BelstatIndicatorItem> Items { get; set; } = new();
    }

    /// <summary>Элемент каталога показателей: code, name, key_words, periodicities, nextPublicationDate.</summary>
    public class BelstatIndicatorItem
    {
        [JsonProperty("code")]
        public string Code { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("key_words")]
        public string? KeyWords { get; set; }

        [JsonProperty("periodicities")]
        public List<string>? Periodicities { get; set; }

        [JsonProperty("nextPublicationDate")]
        public DateTime? NextPublicationDate { get; set; }
    }

    /// <summary>Обёртка ответа дерева рубрик: {"item": {...}}.</summary>
    public class BelstatRubricTreeResponse
    {
        [JsonProperty("item")]
        public BelstatRubric? Item { get; set; }
    }

    /// <summary>
    /// SDMX-JSON ответ со значениями показателя (sdmx-api/indicator/values/SDMX-JSON/{code}).
    /// </summary>
    public class BelstatDataResponse
    {
        [JsonProperty("header")]
        public BelstatHeader? Header { get; set; }

        [JsonProperty("dataSets")]
        public List<BelstatDataSet> DataSets { get; set; } = new();

        [JsonProperty("structure")]
        public BelstatStructure? Structure { get; set; }
    }

    /// <summary>Заголовок SDMX-JSON ответа.</summary>
    public class BelstatHeader
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("prepared")]
        public string? Prepared { get; set; }

        [JsonProperty("test")]
        public bool Test { get; set; }
    }

    /// <summary>Набор данных SDMX-JSON: наблюдения вида "0:0:0:-1" → ["2096.0"].</summary>
    public class BelstatDataSet
    {
        [JsonProperty("action")]
        public string? Action { get; set; }

        [JsonProperty("observations")]
        public Dictionary<string, string[]> Observations { get; set; } = new();
    }

    /// <summary>Структура SDMX-JSON: расшифровка измерений (dimensions).</summary>
    public class BelstatStructure
    {
        [JsonProperty("dimensions")]
        public BelstatDimensions? Dimensions { get; set; }
    }

    /// <summary>Наборы измерений: наборов данных (dataset), серийные (series) и наблюдений (observation).</summary>
    public class BelstatDimensions
    {
        [JsonProperty("dataset")]
        public List<BelstatDimension> Dataset { get; set; } = new();

        [JsonProperty("series")]
        public List<BelstatDimension> Series { get; set; } = new();

        [JsonProperty("observation")]
        public List<BelstatDimension> Observation { get; set; } = new();
    }

    /// <summary>Измерение (например, «Территория», «Период»).</summary>
    public class BelstatDimension
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("values")]
        public List<BelstatDimensionValue> Values { get; set; } = new();

        /// <summary>Подпись измерения для колонки таблицы.</summary>
        [JsonIgnore]
        public string Caption => string.IsNullOrWhiteSpace(Name) ? Id : Name!;
    }

    /// <summary>Значение измерения (код + расшифровка).</summary>
    public class BelstatDimensionValue
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonIgnore]
        public string Caption => string.IsNullOrWhiteSpace(Name) ? Id : Name!;
    }

    /// <summary>Разобранное наблюдение (ключ измерения → значение) для построения таблицы.</summary>
    public class BelstatObservation
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Запись истории запросов государственной статистики (таблица BelstatHistory).
    /// </summary>
    public class BelstatHistoryItem
    {
        public int Id { get; set; }

        public string RubricCode { get; set; } = string.Empty;

        public string IndicatorCode { get; set; } = string.Empty;

        public string IndicatorName { get; set; } = string.Empty;

        /// <summary>Годы запроса, перечисленные через запятую (например, "2019,2020,2021").</summary>
        public string Years { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; }
    }

    // ==========================================================================
    // Справочник структуры данных (GET sdmx-api/indicator/datastructure/SDMX-JSON/{code}).
    // Именно этот ответ содержит русские названия измерений (names.ru схемы понятий)
    // и русские расшифровки значений (names.ru кодов классификаторов CL_*).
    // Эндпоинт значений (/values/SDMX-JSON/{code}) независимо от Accept-Language
    // отдаёт подписи на английском — их и заменяют данные этих моделей.
    // ==========================================================================

    /// <summary>Локализованное название элемента справочника SDMX (английский, русский, белорусский).</summary>
    public class BelstatLocalizedNames
    {
        [JsonProperty("en")]
        public string? En { get; set; }

        [JsonProperty("ru")]
        public string? Ru { get; set; }

        [JsonProperty("be")]
        public string? Be { get; set; }

        /// <summary>Название для отображения: русское, при его отсутствии — английское.</summary>
        [JsonIgnore]
        public string? Preferred => !string.IsNullOrWhiteSpace(Ru) ? Ru : En;
    }

    /// <summary>Обёртка ответа справочника: {"data": {...}}.</summary>
    public class BelstatStructureReferenceResponse
    {
        [JsonProperty("data")]
        public BelstatStructureReferenceData Data { get; set; } = new();
    }

    /// <summary>Содержимое ответа справочника: схемы понятий, классификаторы, описания структур данных.</summary>
    public class BelstatStructureReferenceData
    {
        [JsonProperty("conceptSchemes")]
        public List<BelstatConceptScheme> ConceptSchemes { get; set; } = new();

        [JsonProperty("codelists")]
        public List<BelstatReferenceCodelist> Codelists { get; set; } = new();

        [JsonProperty("dataStructures")]
        public List<BelstatDataStructureReference> DataStructures { get; set; } = new();
    }

    /// <summary>Схема понятий (conceptScheme) — источник русских названий измерений.</summary>
    public class BelstatConceptScheme
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("concepts")]
        public List<BelstatConcept> Concepts { get; set; } = new();
    }

    /// <summary>Понятие (concept): идентификатор совпадает с идентификатором измерения, например razrez_594.</summary>
    public class BelstatConcept
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("names")]
        public BelstatLocalizedNames? Names { get; set; }
    }

    /// <summary>Классификатор значений измерения (codelist), например CL_razrez_594.</summary>
    public class BelstatReferenceCodelist
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("names")]
        public BelstatLocalizedNames? Names { get; set; }

        [JsonProperty("codes")]
        public List<BelstatReferenceCode> Codes { get; set; } = new();
    }

    /// <summary>Код классификатора: идентификатор совпадает с идентификатором значения измерения в ответе значений.</summary>
    public class BelstatReferenceCode
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("names")]
        public BelstatLocalizedNames? Names { get; set; }
    }

    /// <summary>Описание структуры набора данных (dataStructure) с перечнем измерений.</summary>
    public class BelstatDataStructureReference
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("names")]
        public BelstatLocalizedNames? Names { get; set; }

        [JsonProperty("dataStructureComponents")]
        public BelstatDataStructureComponents? DataStructureComponents { get; set; }
    }

    /// <summary>Компоненты структуры данных: перечень измерений (dimensionList).</summary>
    public class BelstatDataStructureComponents
    {
        [JsonProperty("dimensionList")]
        public BelstatReferenceDimensionList? DimensionList { get; set; }
    }

    /// <summary>Перечень измерений структуры данных.</summary>
    public class BelstatReferenceDimensionList
    {
        [JsonProperty("dimensions")]
        public List<BelstatReferenceDimension> Dimensions { get; set; } = new();
    }

    /// <summary>
    /// Измерение в справочнике: ссылка на понятие (conceptIdentity, URN) и на классификатор значений
    /// (localRepresentation.enumeration, URN вида urn:sdmx:...Codelist=BELSTAT:CL_xxx(1.0)).
    /// </summary>
    public class BelstatReferenceDimension
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("conceptIdentity")]
        public string? ConceptIdentity { get; set; }

        [JsonProperty("localRepresentation")]
        public BelstatLocalRepresentation? LocalRepresentation { get; set; }
    }

    /// <summary>Локальное представление измерения: ссылка на классификатор (enumeration).</summary>
    public class BelstatLocalRepresentation
    {
        [JsonProperty("enumeration")]
        public string? Enumeration { get; set; }
    }
}
