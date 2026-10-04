using NalogParserWPF.Models;

namespace NalogParserWPF.Services
{
    public interface IResultFormatter
    {
        string FormatCompanyInfo(CompanyRow company);
        string FormatHistoryEntry(SearchHistory entry);
    }
}
