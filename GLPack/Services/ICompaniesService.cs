using GLPack.Contracts;
using GLPack.ViewModels.Home;

namespace GLPack.Services
{
    public interface ICompaniesService
    {
        Task<CompanyDto> CreateAsync(CompanyUpsertDto dto, CancellationToken ct);
        Task<CompanyDto?> GetAsync(int id, CancellationToken ct);
        Task<IReadOnlyList<CompanyDto>> ListAsync(string? q, int page, int pageSize, CancellationToken ct);
        Task UpdateAsync(int id, CompanyUpsertDto dto, CancellationToken ct);
        Task DeleteAsync(int id, CancellationToken ct);
        Task<IReadOnlyList<CompanyQuickPick>> GetQuickPicksAsync(string? search, int take = 24, CancellationToken ct = default);
    }
}
