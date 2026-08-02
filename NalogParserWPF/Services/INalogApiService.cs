using System.Threading;
using System.Threading.Tasks;
using NalogParserWPF.Models;

namespace NalogParserWPF.Services
{
    public interface INalogApiService
    {
        Task<NalogResponse?> SearchLegalEntityAsync(LegalSearchParams parameters, CancellationToken cancellationToken = default);
        Task<NalogResponse?> SearchPhysicalPersonAsync(PhysicalSearchParams parameters, CancellationToken cancellationToken = default);
        Task<NalogResponse?> GetCompanyInfoAsync(string unp, CancellationToken cancellationToken = default);
    }
}
