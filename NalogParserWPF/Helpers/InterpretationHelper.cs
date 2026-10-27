using System.Windows.Media;
using NalogParserWPF.Models;

namespace NalogParserWPF.Helpers
{
    /// <summary>
    /// Оформление записей интерпретации статистических данных: цвет по важности записи
    /// (интерфейс, Excel, PDF) и текст отметки года. Один источник цветов для всех
    /// представлений, как <see cref="StatusHelper"/> для статусов УНП.
    /// Цвета подобраны так, чтобы текст читался и на светлой, и на тёмной теме.
    /// </summary>
    public static class InterpretationHelper
    {
        /// <summary>Цвет «в пределах нормы».</summary>
        public const string NormalHex = "#2E9E4F";

        /// <summary>Цвет информационной заметки.</summary>
        public const string InfoHex = "#1A6FCC";

        /// <summary>Цвет предупреждения (заметное отклонение).</summary>
        public const string WarningHex = "#E08A00";

        /// <summary>Цвет критичной записи (аномалия).</summary>
        public const string CriticalHex = "#D32F2F";

        /// <summary>Непрозрачность фона плашки (0x26 ≈ 15 %): важность видна, текст остаётся читаемым.</summary>
        private const byte SoftAlpha = 0x26;

        private static readonly SolidColorBrush NormalBrush = CreateBrush(NormalHex);
        private static readonly SolidColorBrush InfoBrush = CreateBrush(InfoHex);
        private static readonly SolidColorBrush WarningBrush = CreateBrush(WarningHex);
        private static readonly SolidColorBrush CriticalBrush = CreateBrush(CriticalHex);

        private static readonly SolidColorBrush SoftNormalBrush = CreateSoftBrush(NormalHex);
        private static readonly SolidColorBrush SoftInfoBrush = CreateSoftBrush(InfoHex);
        private static readonly SolidColorBrush SoftWarningBrush = CreateSoftBrush(WarningHex);
        private static readonly SolidColorBrush SoftCriticalBrush = CreateSoftBrush(CriticalHex);

        /// <summary>Цвет текста по важности записи (строковый HEX для Excel и PDF).</summary>
        public static string GetHexColor(InterpretationSeverity severity) => severity switch
        {
            InterpretationSeverity.Critical => CriticalHex,
            InterpretationSeverity.Warning => WarningHex,
            InterpretationSeverity.Info => InfoHex,
            _ => NormalHex
        };

        /// <summary>Цвет текста по важности записи (кисть для WPF).</summary>
        public static SolidColorBrush GetBrush(InterpretationSeverity severity) => severity switch
        {
            InterpretationSeverity.Critical => CriticalBrush,
            InterpretationSeverity.Warning => WarningBrush,
            InterpretationSeverity.Info => InfoBrush,
            _ => NormalBrush
        };

        /// <summary>
        /// Бледный фон плашки по важности записи (WPF): подсказывает важность, не мешая чтению.
        /// Текст в такой плашке рисуется обычным цветом палитры, а цвет важности остаётся
        /// у пометки типа записи и у рамки — на светлой теме это читается заметно лучше,
        /// чем полностью окрашенный текст.
        /// </summary>
        public static SolidColorBrush GetSoftBrush(InterpretationSeverity severity) => severity switch
        {
            InterpretationSeverity.Critical => SoftCriticalBrush,
            InterpretationSeverity.Warning => SoftWarningBrush,
            InterpretationSeverity.Info => SoftInfoBrush,
            _ => SoftNormalBrush
        };

        /// <summary>Отметка года: пометка аномалии (если есть) и примечание сервиса интерпретации.</summary>
        public static string GetPointNote(InterpretationPoint point)
        {
            if (string.IsNullOrWhiteSpace(point.AnomalyMark))
                return point.Note;

            return string.IsNullOrWhiteSpace(point.Note)
                ? point.AnomalyMark
                : $"{point.AnomalyMark} {point.Note}";
        }

        private static SolidColorBrush CreateBrush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        /// <summary>Полупрозрачная кисть того же цвета — фон плашки записи интерпретации.</summary>
        private static SolidColorBrush CreateSoftBrush(string hex)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            color.A = SoftAlpha;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
