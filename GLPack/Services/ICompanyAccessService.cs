using GLPack.Models;
using System.Security.Claims;

namespace GLPack.Services
{
    public interface ICompanyAccessService
    {
        bool IsAdmin(ClaimsPrincipal user);
        int? GetUserId(ClaimsPrincipal user);
        IQueryable<Company> ApplyCompanyAccess(IQueryable<Company> companies, ClaimsPrincipal user);
        Task<bool> CanAccessCompanyAsync(ClaimsPrincipal user, int companyId, CancellationToken ct);
    }
}
