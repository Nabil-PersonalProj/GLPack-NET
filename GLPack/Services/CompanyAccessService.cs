using GLPack.DAL;
using GLPack.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GLPack.Services
{
    public sealed class CompanyAccessService : ICompanyAccessService
    {
        private readonly ApplicationDbContext _db;

        public CompanyAccessService(ApplicationDbContext db)
        {
            _db = db;
        }

        public bool IsAdmin(ClaimsPrincipal user)
        {
            return user.HasClaim("IsAdmin", "True");
        }

        public int? GetUserId(ClaimsPrincipal user)
        {
            string? value = user.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out int userId) ? userId : null;
        }

        public IQueryable<Company> ApplyCompanyAccess(IQueryable<Company> companies, ClaimsPrincipal user)
        {
            if (IsAdmin(user))
            {
                return companies;
            }

            int? userId = GetUserId(user);
            if (userId == null)
            {
                return companies.Where(_ => false);
            }

            return companies.Where(c =>
                _db.CompanyUserAccesses.Any(a =>
                    a.CompanyId == c.Id &&
                    a.UserId == userId.Value));
        }

        public async Task<bool> CanAccessCompanyAsync(ClaimsPrincipal user, int companyId, CancellationToken ct)
        {
            if (IsAdmin(user))
            {
                return await _db.Companies
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == companyId, ct);
            }

            int? userId = GetUserId(user);
            if (userId == null)
            {
                return false;
            }

            return await _db.CompanyUserAccesses
                .AsNoTracking()
                .AnyAsync(x => x.CompanyId == companyId && x.UserId == userId.Value, ct);
        }
    }
}
