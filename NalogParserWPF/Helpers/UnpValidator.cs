using System.Linq;

namespace NalogParserWPF.Helpers
{
    public static class UnpValidator
    {
        public static bool IsValid(string? unp)
        {
            if (string.IsNullOrWhiteSpace(unp))
                return false;

            string normalized = Normalize(unp);
            return normalized.Length == 9 && normalized.All(char.IsDigit);
        }

        public static string Normalize(string? unp)
        {
            if (string.IsNullOrEmpty(unp))
                return string.Empty;

            return unp.Replace(" ", string.Empty).Replace("-", string.Empty);
        }

        public static bool IsBelarusianResident(string? unp)
        {
            if (!IsValid(unp))
                return false;

            char firstDigit = Normalize(unp)[0];
            return firstDigit >= '1' && firstDigit <= '9';
        }
    }
}
