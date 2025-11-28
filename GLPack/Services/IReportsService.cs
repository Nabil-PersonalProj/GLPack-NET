using System.Threading;
using System.Threading.Tasks;

namespace GLPack.Services
{
    public interface IReportsService
    {
        Task<string> GetTrialBalanceCsvAsync(int companyId, CancellationToken ct);
        Task<string> GetProfitAndLossCsvAsync(int companyId, CancellationToken ct);
    }
}
