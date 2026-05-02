using GLPack.ViewModels.Reports;

namespace GLPack.Services
{
    public interface IReportsService
    {
        Task<string> GetTrialBalanceCsvAsync(int companyId, CancellationToken ct);
        Task<string> GetProfitAndLossCsvAsync(int companyId, CancellationToken ct);
        Task<string> GetBalanceSheetCsvAsync(int companyId, CancellationToken ct);
        Task<(List<TrialBalanceRow> Rows, decimal TotalDebit, decimal TotalCredit)>
            GetTrialBalanceAsync(int companyId, CancellationToken ct);
        Task<List<ProfitLossRowVm>> GetProfitAndLossAsync(int companyId, CancellationToken ct);
        Task<BalanceSheetVm> GetBalanceSheetAsync(int companyId, CancellationToken ct);
    }
}
