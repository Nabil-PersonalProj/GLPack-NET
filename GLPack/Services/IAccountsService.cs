using GLPack.Contracts;

namespace GLPack.Services
{
    public interface IAccountsService
    {
        Task<AccountDto?> GetAsync(int companyId, string accountCode, CancellationToken ct);
        Task<IReadOnlyList<AccountDto>> ListAsync(int companyId, string? q, int page, int pageSize, CancellationToken ct);
        Task<AccountDto> CreateAsync(AccountUpsertDto dto, CancellationToken ct);
        Task UpdateAsync(int companyId, string accountCode, AccountUpsertDto dto, CancellationToken ct);
        Task DeleteAsync(int companyId, string accountCode, CancellationToken ct); // optional
    }
}
