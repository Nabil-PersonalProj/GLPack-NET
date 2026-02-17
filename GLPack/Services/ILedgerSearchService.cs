using GLPack.Contracts;

namespace GLPack.Services
{
    public interface ILedgerSearchService
    {
        Task<IReadOnlyList<LedgerSearchDtos.LedgerRowDto>> SearchAsync(
            int companyId,
            string? q,
            string? accountCode,
            int? transactionNo,
            DateTime? from,
            DateTime? to,
            int page = 1,
            int pageSize = 100,
            CancellationToken ct = default);
    }
}
