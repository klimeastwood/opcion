using System;
using System.Collections.Generic;
using System.Globalization;

namespace NalogParserWPF.Models
{
    /// <summary>
    /// Важность записи интерпретации. Управляет цветовым акцентом примечания
    /// и рекомендации (обычная, информационная, предупреждение, критичная).
    /// </summary>
    public enum InterpretationSeverity
    {
        /// <summary>В пределах нормы.</summary>
        Normal,

        /// <summary>Информационная заметка (не отклонение).</summary>
        Info,

        /// <summary>Предупреждение: заметное отклонение.</summary>
        Warning,

        /// <summary>Критично: аномальное отклонение, требует внимания следователя.</summary>
        Critical
    }

    /// <summary>
    /// Одна запись интерпретации — примечание к году либо рекомендация для следственной работы.
    /// </summary>
    public class InterpretationNote
    {
        /// <summary>Тип записи: «Аномалия», «Скачок», «Тренд», «Рекомендация», «Данные».</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>Текст записи.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Важность записи (влияет на оформление в интерфейсе и PDF).</summary>
        public InterpretationSeverity Severity { get; set; } = InterpretationSeverity.Normal;
    }

    /// <summary>
    /// Одна точка временного ряда (год) с результатами анализа:
    /// отклонение от среднего в процентах, тренд «год к году», отметка аномалии и примечание.
    /// </summary>
    public class InterpretationPoint
    {
        /// <summary>Год наблюдения.</summary>
        public int Year { get; set; }

        /// <summary>Значение показателя за год.</summary>
        public double Value { get; set; }

        /// <summary>Отклонение от среднего за период, в процентах.</summary>
        public double DeviationPercent { get; set; }

        /// <summary>Отношение отклонения к стандартному отклонению (в сигмах σ).</summary>
        public double Sigma { get; set; }

        /// <summary>Тренд «год к году»: ↑ рост, ↓ снижение, → без изменения.</summary>
        public string Trend { get; set; } = "→";

        /// <summary>Отметка аномалии (пусто, если аномалии нет).</summary>
        public string AnomalyMark { get; set; } = string.Empty;

        /// <summary>Примечание-пометка по значению года.</summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>Важность пометки (используется для оформления строки).</summary>
        public InterpretationSeverity Severity { get; set; } = InterpretationSeverity.Normal;

        /// <summary>Значение для отображения в таблице.</summary>
        public string ValueDisplay => Value.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>Отклонение от среднего для отображения в таблице (со знаком).</summary>
        public string DeviationDisplay =>
            DeviationPercent.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " %";
    }

    /// <summary>
    /// Результат интерпретации статистического ряда: исходные годы и значения,
    /// сводные статистики, тренд, разбор по годам и рекомендации для следователя.
    /// </summary>
    public class InterpretationResult
    {
        /// <summary>Название показателя.</summary>
        public string IndicatorName { get; set; } = string.Empty;

        /// <summary>Годы периода (по возрастанию).</summary>
        public List<int> Years { get; set; } = new();

        /// <summary>Значения показателя по годам.</summary>
        public List<double> Values { get; set; } = new();

        /// <summary>Среднее значение за период.</summary>
        public double Average { get; set; }

        /// <summary>Стандартное отклонение (σ) за период.</summary>
        public double Deviation { get; set; }

        /// <summary>Описание тренда (направление, наклон, изменение «первый → последний»).</summary>
        public string Trend { get; set; } = string.Empty;

        /// <summary>Краткое резюме по аномалиям.</summary>
        public string Anomaly { get; set; } = string.Empty;

        /// <summary>Количество скачков «год к году» (изменение более 50 %).</summary>
        public int JumpCount { get; set; }

        /// <summary>Описание устойчивого роста/падения (3+ года подряд) либо «не выявлено».</summary>
        public string SustainedTrend { get; set; } = string.Empty;

        /// <summary>Разбор по годам (для таблиц).</summary>
        public List<InterpretationPoint> Points { get; set; } = new();

        /// <summary>Заметки статистического анализа (для блока «Статистический анализ»).</summary>
        public List<InterpretationNote> Notes { get; set; } = new();

        /// <summary>Рекомендации для следственной работы.</summary>
        public List<InterpretationNote> Recommendations { get; set; } = new();

        /// <summary>Есть ли данные для отображения.</summary>
        public bool HasData => Points.Count > 0;

        /// <summary>Среднее значение для отображения.</summary>
        public string AverageDisplay => Average.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>Стандартное отклонение для отображения.</summary>
        public string DeviationDisplay => Deviation.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
