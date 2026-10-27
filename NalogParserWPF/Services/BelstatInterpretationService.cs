using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NalogParserWPF.Models;

namespace NalogParserWPF.Services
{
    /// <summary>
    /// Реализация интерпретации статистических данных Белстата для следователя.
    /// Сервис не зависит от API и UI: принимает готовую таблицу результата (DataTable),
    /// извлекает временной ряд «год → значение» и возвращает разбор с рекомендациями.
    /// </summary>
    public class BelstatInterpretationService : IBelstatInterpretationService
    {
        /// <summary>Порог аномального отклонения от среднего (в процентах).</summary>
        private const double AnomalyPercentThreshold = 30.0;

        /// <summary>Порог заметного отклонения от среднего (в процентах).</summary>
        private const double NoticeablePercentThreshold = 10.0;

        /// <summary>Порог скачка «год к году» (в процентах).</summary>
        private const double JumpPercentThreshold = 50.0;

        /// <summary>Минимальное число однонаправленных шагов для «устойчивого» тренда.</summary>
        private const int SustainedSteps = 3;

        /// <summary>Порог аномалии по стандартному отклонению (в сигмах σ).</summary>
        private const double AnomalySigma = 2.0;

        private const string DefaultIndicator = "Показатель государственной статистики";

        /// <summary>Подсказки в имени столбца с годами.</summary>
        private static readonly string[] YearColumnHints = { "год", "период", "year", "date", "годы" };

        /// <summary>Подсказки в имени столбца со значениями.</summary>
        private static readonly string[] ValueColumnHints = { "значение", "value", "показатель" };

        private static readonly Regex YearPattern = new(@"(1[89]\d{2}|2[01]\d{2})", RegexOptions.Compiled);

        /// <summary>
        /// Словарь рекомендаций по типам показателей. Ключ — категория, значение — ключевые слова
        /// (по которым определяется тип показателя по его названию) и текст рекомендации.
        /// </summary>
        private static readonly Dictionary<string, RecommendationRule> RecommendationRules = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Смертность"] = new(new[] { "смерт", "летальн", "погибш" },
                "Проверить случаи насильственной смерти, отравлений, суицидов"),
            ["Миграция"] = new(new[] { "миграц", "мигрант", "прибыв", "выбыв" },
                "Проанализировать причины оттока/притока населения"),
            ["Дети-сироты"] = new(new[] { "сирот", "опек", "приемн", "приёмн" },
                "Проверить исполнение алиментных обязательств"),
            ["Преступность"] = new(new[] { "преступ", "правонаруш" },
                "Проверить статистику по видам преступлений"),
            ["Доходы"] = new(new[] { "доход", "зарплат", "заработн", "прожиточн" },
                "Проверить уровень жизни, задолженность по зарплате"),
            ["Занятость"] = new(new[] { "занят", "безработ", "трудоустр", "рабочей силы" },
                "Проверить уровень безработицы, скрытую занятость"),
            ["Образование"] = new(new[] { "образован", "учащ", "школ", "студент" },
                "Проверить доступность образования, отсев"),
            ["Здравоохранение"] = new(new[] { "здравоохран", "медицин", "заболев", "заболева" },
                "Проверить доступность медпомощи, смертность"),
        };

        private const string DefaultRecommendation = "Проанализировать причины отклонения";

        /// <summary>Правило подбора рекомендации: категория, ключевые слова и текст рекомендации.</summary>
        private sealed record RecommendationRule(string[] Keywords, string Recommendation);

        public InterpretationResult AnalyzeSimple(DataTable data, string indicatorName)
        {
            var (years, values, warnings) = ExtractSeries(data);
            var result = CreateBase(indicatorName, warnings);

            if (values.Count == 0)
            {
                result.Notes.Add(new InterpretationNote
                {
                    Type = "Данные",
                    Text = "Недостаточно числовых данных для интерпретации показателя.",
                    Severity = InterpretationSeverity.Warning
                });
                result.Recommendations.Add(new InterpretationNote
                {
                    Type = "Рекомендация",
                    Text = DefaultRecommendation,
                    Severity = InterpretationSeverity.Info
                });
                return result;
            }

            result.Years = years;
            result.Values = values;
            // Этап 1 — быстрый предварительный расчёт (среднее, минимум, максимум).
            var preliminary = ComputePreliminary(values);
            result.Average = preliminary.Average;
            result.Deviation = StandardDeviation(values);

            int highAnomalies = 0;
            int lowAnomalies = 0;

            for (int i = 0; i < values.Count; i++)
            {
                double deviation = PercentDeviation(values[i], result.Average);
                var point = new InterpretationPoint
                {
                    Year = years[i],
                    Value = values[i],
                    DeviationPercent = deviation,
                    Sigma = Sigma(value: values[i], average: result.Average, sigma: result.Deviation),
                    Trend = TrendArrow(values, i)
                };

                ApplySimpleMark(point);
                if (deviation > AnomalyPercentThreshold) highAnomalies++;
                else if (deviation < -AnomalyPercentThreshold) lowAnomalies++;

                result.Points.Add(point);
            }

            result.Anomaly = BuildAnomalySummary(highAnomalies + lowAnomalies, values.Count);

            BuildSimpleRecommendations(result, highAnomalies, lowAnomalies);
            return result;
        }

        public InterpretationResult AnalyzeAdvanced(DataTable data, string indicatorName)
        {
            var (years, values, warnings) = ExtractSeries(data);
            var result = CreateBase(indicatorName, warnings);

            if (values.Count == 0)
            {
                result.Notes.Add(new InterpretationNote
                {
                    Type = "Данные",
                    Text = "Недостаточно числовых данных для расширенного анализа.",
                    Severity = InterpretationSeverity.Warning
                });
                result.Recommendations.Add(new InterpretationNote
                {
                    Type = "Рекомендация",
                    Text = BuildCategoryRecommendation(indicatorName),
                    Severity = InterpretationSeverity.Info
                });
                return result;
            }

            result.Years = years;
            result.Values = values;
            // Этап 1 — быстрый предварительный расчёт (среднее, минимум, максимум),
            // этап 2 — детальный анализ (тренд, скачки, аномалии).
            var preliminary = ComputePreliminary(values);
            result.Average = preliminary.Average;
            result.Deviation = StandardDeviation(values);
            (double slope, _) = LinearRegression(values);
            result.Trend = BuildTrendDescription(values, result.Average, slope);
            result.SustainedTrend = DetectSustainedTrend(years, values);
            result.JumpCount = CountJumps(values);

            int anomalyCount = 0;
            for (int i = 0; i < values.Count; i++)
            {
                double sigmaValue = Sigma(value: values[i], average: result.Average, sigma: result.Deviation);
                var point = new InterpretationPoint
                {
                    Year = years[i],
                    Value = values[i],
                    DeviationPercent = PercentDeviation(values[i], result.Average),
                    Sigma = sigmaValue,
                    Trend = TrendArrow(values, i)
                };

                if (Math.Abs(sigmaValue) > AnomalySigma)
                {
                    point.AnomalyMark = $"⚠️ {Math.Abs(sigmaValue):0.0}σ";
                    point.Note = sigmaValue > 0 ? "Аномально высокое значение (более 2σ)" : "Аномально низкое значение (более 2σ)";
                    point.Severity = InterpretationSeverity.Critical;
                    anomalyCount++;
                }
                else
                {
                    ApplySimpleMark(point);
                }

                result.Points.Add(point);
            }

            result.Anomaly = BuildAnomalySummary(anomalyCount, values.Count);
            BuildAdvancedNotes(result);
            BuildAdvancedRecommendations(result, indicatorName, anomalyCount);
            return result;
        }

        /// <summary>
        /// Фоновый вариант простой интерпретации: расчёт выносится в пул потоков,
        /// чтобы интерфейс не блокировался на больших таблицах (50 000+ строк).
        /// </summary>
        public Task<InterpretationResult> AnalyzeSimpleAsync(
            DataTable data, string indicatorName, CancellationToken cancellationToken = default)
            => Task.Run(() => AnalyzeSimple(data, indicatorName), cancellationToken);

        /// <summary>
        /// Фоновый вариант расширенной интерпретации: расчёт выносится в пул потоков.
        /// </summary>
        public Task<InterpretationResult> AnalyzeAdvancedAsync(
            DataTable data, string indicatorName, CancellationToken cancellationToken = default)
            => Task.Run(() => AnalyzeAdvanced(data, indicatorName), cancellationToken);

        /// <summary>
        /// Быстрый предварительный этап: среднее, минимум и максимум ряда за один проход.
        /// Выполняется до детального анализа, чтобы сразу получить базовые статистики.
        /// </summary>
        private static (double Average, double Min, double Max) ComputePreliminary(IReadOnlyList<double> values)
        {
            double sum = 0.0;
            double min = values[0];
            double max = values[0];

            for (int i = 0; i < values.Count; i++)
            {
                double v = values[i];
                sum += v;
                if (v < min) min = v;
                if (v > max) max = v;
            }

            return (sum / values.Count, min, max);
        }

        // ================= Формирование заметок и рекомендаций =================

        private static InterpretationResult CreateBase(string indicatorName, List<string> warnings)
        {
            var result = new InterpretationResult
            {
                IndicatorName = string.IsNullOrWhiteSpace(indicatorName) ? DefaultIndicator : indicatorName.Trim()
            };

            foreach (var warning in warnings)
            {
                result.Notes.Add(new InterpretationNote
                {
                    Type = "Данные",
                    Text = warning,
                    Severity = InterpretationSeverity.Info
                });
            }

            return result;
        }

        /// <summary>Пометка года для простой интерпретации (пороговые отклонения 10 % и 30 %).</summary>
        private static void ApplySimpleMark(InterpretationPoint point)
        {
            double deviation = point.DeviationPercent;

            if (deviation > AnomalyPercentThreshold)
            {
                point.Note = "⚠️ Аномально высокое значение";
                point.Severity = InterpretationSeverity.Warning;
                point.AnomalyMark = "⚠️";
            }
            else if (deviation < -AnomalyPercentThreshold)
            {
                point.Note = "⚠️ Аномально низкое значение";
                point.Severity = InterpretationSeverity.Warning;
                point.AnomalyMark = "⚠️";
            }
            else if (deviation > NoticeablePercentThreshold)
            {
                point.Note = "📊 Выше среднего";
                point.Severity = InterpretationSeverity.Info;
            }
            else if (deviation < -NoticeablePercentThreshold)
            {
                point.Note = "📊 Ниже среднего";
                point.Severity = InterpretationSeverity.Info;
            }
            else
            {
                point.Note = "✅ В пределах нормы";
                point.Severity = InterpretationSeverity.Normal;
            }
        }

        private static void BuildSimpleRecommendations(InterpretationResult result, int highAnomalies, int lowAnomalies)
        {
            if (highAnomalies + lowAnomalies == 0)
            {
                result.Recommendations.Add(new InterpretationNote
                {
                    Type = "Рекомендация",
                    Text = "Существенных отклонений (более 30 %) не выявлено. Показатель изменяется в пределах нормы.",
                    Severity = InterpretationSeverity.Normal
                });
            }
            else
            {
                result.Recommendations.Add(new InterpretationNote
                {
                    Type = "Рекомендация",
                    Text = "Рекомендуется проверить причины отклонения.",
                    Severity = InterpretationSeverity.Warning
                });
            }

            if (highAnomalies > 0)
            {
                result.Recommendations.Add(new InterpretationNote
                {
                    Type = "Рекомендация",
                    Text = "Возможно, требуется проверка по факту резкого роста.",
                    Severity = InterpretationSeverity.Warning
                });
            }

            if (lowAnomalies > 0)
            {
                result.Recommendations.Add(new InterpretationNote
                {
                    Type = "Рекомендация",
                    Text = "Проверить достоверность данных.",
                    Severity = InterpretationSeverity.Warning
                });
            }

            result.Recommendations.Add(new InterpretationNote
            {
                Type = "Рекомендация",
                Text = BuildCategoryRecommendation(result.IndicatorName),
                Severity = InterpretationSeverity.Info
            });
        }

        /// <summary>
        /// Формирует рекомендации для следственной работы по итогам расширенного анализа:
        /// аномалии (более 2σ), резкие скачки «год к году» (более 50 %), устойчивое изменение
        /// и тип показателя (по его названию).
        /// </summary>
        private static void BuildAdvancedRecommendations(InterpretationResult result, string indicatorName, int anomalyCount)
        {
            if (anomalyCount == 0)
            {
                result.Recommendations.Add(new InterpretationNote
                {
                    Type = "Рекомендация",
                    Text = "Аномальных отклонений (более 2σ) не выявлено: показатель изменяется в статистически ожидаемых пределах.",
                    Severity = InterpretationSeverity.Normal
                });
            }
            else
            {
                result.Recommendations.Add(new InterpretationNote
                {
                    Type = "Рекомендация",
                    Text = $"Выявлено аномальных значений (более 2σ): {anomalyCount}. Установить причины отклонений и проверить первичные документы за эти годы.",
                    Severity = InterpretationSeverity.Critical
                });
            }

            if (result.JumpCount > 0)
            {
                result.Recommendations.Add(new InterpretationNote
                {
                    Type = "Рекомендация",
                    Text = $"Обнаружены резкие изменения показателя «год к году» (более 50 %): {result.JumpCount}. Проверить основания изменения показателя и достоверность данных за эти годы.",
                    Severity = InterpretationSeverity.Warning
                });
            }

            if (result.SustainedTrend.StartsWith("Устойчивое снижение", StringComparison.Ordinal))
            {
                result.Recommendations.Add(new InterpretationNote
                {
                    Type = "Рекомендация",
                    Text = "Выявлено устойчивое снижение показателя несколько лет подряд: проверить причины снижения и полноту учёта данных.",
                    Severity = InterpretationSeverity.Info
                });
            }
            else if (result.SustainedTrend.StartsWith("Устойчивый рост", StringComparison.Ordinal))
            {
                result.Recommendations.Add(new InterpretationNote
                {
                    Type = "Рекомендация",
                    Text = "Выявлен устойчивый рост показателя несколько лет подряд: проверить причины роста и его обоснованность.",
                    Severity = InterpretationSeverity.Info
                });
            }

            result.Recommendations.Add(new InterpretationNote
            {
                Type = "Рекомендация",
                Text = BuildCategoryRecommendation(indicatorName),
                Severity = InterpretationSeverity.Info
            });
        }

        private static void BuildAdvancedNotes(InterpretationResult result)
        {
            result.Notes.Add(new InterpretationNote
            {
                Type = "Среднее",
                Text = $"Среднее значение за период: {result.Average:0.###}",
                Severity = InterpretationSeverity.Info
            });

            result.Notes.Add(new InterpretationNote
            {
                Type = "Отклонение",
                Text = $"Стандартное отклонение (σ): {result.Deviation:0.###}",
                Severity = InterpretationSeverity.Info
            });

            result.Notes.Add(new InterpretationNote
            {
                Type = "Тренд",
                Text = result.Trend,
                Severity = InterpretationSeverity.Info
            });

            result.Notes.Add(new InterpretationNote
            {
                Type = "Скачки",
                Text = result.JumpCount == 0
                    ? "Резких скачков (более 50 % год к году) не выявлено."
                    : $"Выявлено резких скачков (более 50 % год к году): {result.JumpCount}.",
                Severity = result.JumpCount > 0 ? InterpretationSeverity.Warning : InterpretationSeverity.Normal
            });

            result.Notes.Add(new InterpretationNote
            {
                Type = "Устойчивость",
                Text = result.SustainedTrend,
                Severity = InterpretationSeverity.Info
            });

            result.Notes.Add(new InterpretationNote
            {
                Type = "Аномалии",
                Text = result.Anomaly,
                Severity = result.Points.Any(p => p.Severity == InterpretationSeverity.Critical)
                    ? InterpretationSeverity.Critical
                    : InterpretationSeverity.Normal
            });

            AddJumpNotes(result);
        }

        private static void AddJumpNotes(InterpretationResult result)
        {
            for (int i = 1; i < result.Values.Count; i++)
            {
                double previous = result.Values[i - 1];
                if (previous == 0) continue;

                double change = (result.Values[i] - previous) / Math.Abs(previous) * 100.0;
                if (Math.Abs(change) <= JumpPercentThreshold) continue;

                result.Notes.Add(new InterpretationNote
                {
                    Type = "Скачок",
                    Text = $"{result.Years[i - 1]} → {result.Years[i]}: изменение {change:+0.0;-0.0;0.0} %.",
                    Severity = InterpretationSeverity.Warning
                });
            }
        }

        /// <summary>Подбирает рекомендацию для следственной работы по типу показателя (по его названию).</summary>
        public static string BuildCategoryRecommendation(string? indicatorName)
        {
            if (string.IsNullOrWhiteSpace(indicatorName))
                return DefaultRecommendation;

            var name = indicatorName.ToLowerInvariant();
            foreach (var rule in RecommendationRules.Values)
            {
                if (rule.Keywords.Any(keyword => name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                    return rule.Recommendation;
            }

            return DefaultRecommendation;
        }

        // ================= Извлечение временного ряда из таблицы =================

        /// <summary>
        /// Извлекает из таблицы результата временной ряд «год → значение».
        /// Столбец годов ищется по подписи, затем по содержимому; столбец значений — по подписи
        /// («Значение»), иначе берётся последний столбец. Если за год несколько наблюдений,
        /// используется среднее; если столбец годов не распознан — порядковые номера.
        /// </summary>
        private static (List<int> Years, List<double> Values, List<string> Warnings) ExtractSeries(DataTable? data)
        {
            var years = new List<int>();
            var values = new List<double>();
            var warnings = new List<string>();

            if (data == null || data.Rows.Count == 0)
                return (years, values, warnings);

            int valueIndex = FindValueColumn(data);
            if (valueIndex < 0)
            {
                warnings.Add("Не найден столбец со значениями показателя.");
                return (years, values, warnings);
            }

            int yearIndex = FindYearColumn(data, valueIndex);
            var grouped = new SortedDictionary<int, List<double>>();
            bool fallbackYears = yearIndex < 0;
            int sequence = 0;

            foreach (DataRow row in data.Rows)
            {
                string? rawValue = valueIndex < row.ItemArray.Length ? row[valueIndex]?.ToString() : null;
                if (!TryParseNumber(rawValue, out double value))
                    continue;

                int year;
                if (yearIndex >= 0)
                {
                    if (!TryParseYear(row[yearIndex]?.ToString(), out year))
                        continue;
                }
                else
                {
                    year = ++sequence;
                }

                if (!grouped.TryGetValue(year, out var bucket))
                {
                    bucket = new List<double>();
                    grouped[year] = bucket;
                }

                bucket.Add(value);
            }

            bool hasDuplicates = false;
            foreach (var pair in grouped)
            {
                years.Add(pair.Key);
                values.Add(pair.Value.Average());
                if (pair.Value.Count > 1) hasDuplicates = true;
            }

            if (fallbackYears && years.Count > 0)
                warnings.Add("Столбец с годами не распознан — годы заменены порядковыми номерами.");
            if (hasDuplicates)
                warnings.Add("За отдельные годы в данных несколько наблюдений — использовано среднее значение.");

            return (years, values, warnings);
        }

        /// <summary>Определяет индекс столбца годов по подписи, затем по содержимому (исключая столбец значений).</summary>
        private static int FindYearColumn(DataTable data, int valueIndex)
        {
            for (int i = 0; i < data.Columns.Count; i++)
            {
                var name = data.Columns[i].ColumnName;
                if (YearColumnHints.Any(hint => name.Contains(hint, StringComparison.OrdinalIgnoreCase)))
                    return i;
            }

            for (int i = 0; i < data.Columns.Count; i++)
            {
                if (i == valueIndex) continue;

                int total = 0;
                int yearish = 0;
                foreach (DataRow row in data.Rows)
                {
                    var text = row[i]?.ToString();
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    total++;
                    if (TryParseYear(text, out _)) yearish++;
                }

                if (total > 0 && yearish == total)
                    return i;
            }

            return -1;
        }

        /// <summary>Определяет индекс столбца значений по подписи, иначе — последний столбец.</summary>
        private static int FindValueColumn(DataTable data)
        {
            for (int i = 0; i < data.Columns.Count; i++)
            {
                var name = data.Columns[i].ColumnName;
                if (ValueColumnHints.Any(hint => name.Contains(hint, StringComparison.OrdinalIgnoreCase)))
                    return i;
            }

            return data.Columns.Count - 1;
        }

        // ================= Разбор значений =================

        /// <summary>Разбирает число с учётом пробелов-разделителей, запятой и неразрывного пробела.</summary>
        private static bool TryParseNumber(string? raw, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var text = raw.Trim();
            if (text is "-" or "—" or "–" or "x" or "X" or "н/д" or "н.д." or "…" or "...")
                return false;

            text = text.Replace("\u00A0", string.Empty)
                       .Replace(" ", string.Empty)
                       .Replace("'", string.Empty)
                       .Replace("−", "-");

            if (text.Contains(','))
                text = text.Replace(".", string.Empty).Replace(',', '.');

            return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>Разбирает год: сначала как число, затем — поиском четырёхзначного года в тексте.</summary>
        private static bool TryParseYear(string? raw, out int year)
        {
            year = 0;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            if (TryParseNumber(raw, out double numeric) && numeric is >= 1500 and <= 2200)
            {
                year = (int)Math.Round(numeric);
                return true;
            }

            var match = YearPattern.Match(raw);
            if (match.Success && int.TryParse(match.Value, out int parsed))
            {
                year = parsed;
                return true;
            }

            return false;
        }

        // ================= Статистические вычисления =================

        /// <summary>Среднеквадратическое (стандартное) отклонение генеральной совокупности.</summary>
        private static double StandardDeviation(IReadOnlyList<double> values)
        {
            if (values.Count < 2)
                return 0;

            double average = values.Average();
            double sum = values.Sum(v => (v - average) * (v - average));
            return Math.Sqrt(sum / values.Count);
        }

        /// <summary>Линейная регрессия (метод наименьших квадратов) по индексам 0..n-1.</summary>
        private static (double Slope, double Intercept) LinearRegression(IReadOnlyList<double> values)
        {
            int n = values.Count;
            if (n < 2)
                return n == 1 ? (0, values[0]) : (0, 0);

            double sumX = 0, sumY = 0, sumXy = 0, sumXx = 0;
            for (int i = 0; i < n; i++)
            {
                sumX += i;
                sumY += values[i];
                sumXy += i * values[i];
                sumXx += (double)i * i;
            }

            double denominator = n * sumXx - sumX * sumX;
            if (Math.Abs(denominator) < double.Epsilon)
                return (0, values.Average());

            double slope = (n * sumXy - sumX * sumY) / denominator;
            double intercept = (sumY - slope * sumX) / n;
            return (slope, intercept);
        }

        /// <summary>Отклонение значения от среднего в процентах.</summary>
        private static double PercentDeviation(double value, double average)
        {
            if (Math.Abs(average) < double.Epsilon)
                return 0;
            return (value - average) / Math.Abs(average) * 100.0;
        }

        /// <summary>Отклонение значения от среднего в единицах стандартного отклонения (σ).</summary>
        private static double Sigma(double value, double average, double sigma)
        {
            if (sigma <= double.Epsilon)
                return 0;
            return (value - average) / sigma;
        }

        /// <summary>Стрелка направления изменения относительно предыдущего года.</summary>
        private static string TrendArrow(IReadOnlyList<double> values, int index)
        {
            if (index <= 0)
                return "—";

            double delta = values[index] - values[index - 1];
            if (Math.Abs(delta) < 1e-9) return "→";
            return delta > 0 ? "↑" : "↓";
        }

        /// <summary>Формирует текстовое описание общего тренда по коэффициенту наклона регрессии.</summary>
        private static string BuildTrendDescription(IReadOnlyList<double> values, double average, double slope)
        {
            if (values.Count < 2)
                return "Тренд не рассчитывается: недостаточно данных.";

            double threshold = Math.Max(Math.Abs(average) * 0.01, 1e-9);
            if (Math.Abs(slope) <= threshold)
                return $"Тренд: стабильный (наклон {slope:+0.###;-0.###;0} за год).";

            string direction = slope > 0 ? "рост" : "снижение";
            return $"Тренд: {direction} — наклон {slope:+0.###;-0.###;0} единиц за год.";
        }

        /// <summary>Считает число резких скачков «год к году» (более 50 %).</summary>
        private static int CountJumps(IReadOnlyList<double> values)
        {
            int jumps = 0;
            for (int i = 1; i < values.Count; i++)
            {
                double previous = values[i - 1];
                if (previous == 0) continue;

                double change = Math.Abs((values[i] - previous) / Math.Abs(previous) * 100.0);
                if (change > JumpPercentThreshold) jumps++;
            }

            return jumps;
        }

        /// <summary>Ищет устойчивое (3 года и более) однонаправленное изменение показателя.</summary>
        private static string DetectSustainedTrend(IReadOnlyList<int> years, IReadOnlyList<double> values)
        {
            if (values.Count < SustainedSteps + 1)
                return "Недостаточно данных для оценки устойчивости тренда.";

            int bestLength = 0, bestStart = -1, bestDirection = 0;
            int currentLength = 0, currentStart = 0, currentDirection = 0;

            for (int i = 1; i < values.Count; i++)
            {
                int direction = Math.Sign(values[i] - values[i - 1]);
                if (direction == 0)
                {
                    currentLength = 0;
                    continue;
                }

                if (direction == currentDirection)
                {
                    currentLength++;
                }
                else
                {
                    currentDirection = direction;
                    currentStart = i - 1;
                    currentLength = 1;
                }

                if (currentLength > bestLength)
                {
                    bestLength = currentLength;
                    bestStart = currentStart;
                    bestDirection = direction;
                }
            }

            if (bestLength >= SustainedSteps && bestStart >= 0)
            {
                string kind = bestDirection > 0 ? "Устойчивый рост" : "Устойчивое снижение";
                return $"{kind}: {years[bestStart]}–{years[bestStart + bestLength]} ({bestLength + 1} лет подряд).";
            }

            return "Устойчивого (3 года и более) однонаправленного тренда не выявлено.";
        }

        /// <summary>Краткая сводка по выявленным аномалиям.</summary>
        private static string BuildAnomalySummary(int anomalyCount, int total)
        {
            if (anomalyCount == 0)
                return "Аномалий не выявлено.";
            return $"Выявлено аномальных значений: {anomalyCount} из {total}.";
        }
    }
}
