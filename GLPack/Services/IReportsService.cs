using GLPack.ViewModels.Reports;
using System.Threading;
using System.Threading.Tasks;

namespace GLPack.Services
{
    public interface IReportsService
    {
        Task<string> GetTrialBalanceCsvAsync(int companyId, CancellationToken ct);
        Task<string> GetProfitAndLossCsvAsync(int companyId, CancellationToken ct);

        Task<(List<TrialBalanceRow> Rows, decimal TotalDebit, decimal TotalCredit)>
            GetTrialBalanceAsync(int companyId, CancellationToken ct);

        Task<(List<ProfitLossSection> Sections, decimal NetProfit)>
            GetProfitAndLossAsync(int companyId, CancellationToken ct);
    }
}
