using System;
using System.Globalization;

namespace NalogParserWPF.Helpers
{
    public static class StringHelper
    {
        public static string FormatDate(string dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString))
                return string.Empty;

            if (DateTime.TryParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
            {
                return date.ToString("dd.MM.yyyy");
            }

            return dateString;
        }

        public static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || maxLength <= 0)
                return string.Empty;

            if (text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength) + "...";
        }

        public static bool IsValidUnp(string unp)
        {
            if (string.IsNullOrEmpty(unp) || unp.Length != 9)
                return false;

            foreach (char c in unp)
            {
                if (!char.IsDigit(c))
                    return false;
            }

            return true;
        }
    }
}
