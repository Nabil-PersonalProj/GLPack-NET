using GLPack.Contracts;

namespace GLPack.Services
{
    public interface IAccountsService
    {
        Task<AccountDto?> GetAsync(int companyId, string accountCode, CancellationToken ct);
        Task<PagedResult<AccountDto>> ListAsync(int companyId, string? q, string? accountType, int page, int pageSize, CancellationToken ct);
        Task UpdateAsync(int companyId, string accountCode, AccountUpsertDto dto, CancellationToken ct);
        Task DeleteAsync(int companyId, string accountCode, CancellationToken ct); // optional
        Task<AccountDto> CreateFromPrefixAsync(AccountCreateFromPrefixDto dto, CancellationToken ct);
        Task<AccountImportResult> ImportAsync(int companyId, IFormFile importFile, CancellationToken ct);
    }
}
