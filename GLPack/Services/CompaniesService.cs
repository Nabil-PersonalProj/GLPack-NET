using GLPack.Contracts;
using GLPack.DAL;
using GLPack.Models;
using GLPack.ViewModels.Home;
using Microsoft.EntityFrameworkCore;

namespace GLPack.Services
{
    public sealed class CompaniesService : ICompaniesService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAppLogger _appLogger;
        public CompaniesService(ApplicationDbContext db, IAppLogger appLogger)
        {
            _db = db;
            _appLogger = appLogger;
        }

        public async Task<CompanyDto> CreateAsync(CompanyUpsertDto dto, CancellationToken ct)
        {
            var entity = new Company { Name = dto.Name };
            _db.Companies.Add(entity);
            await _db.SaveChangesAsync(ct);
            await _appLogger.LogAsync(
                eventType: "AUDIT",
                level: "INFO",
                logCode: "COMPANY_CREATE_OK",
                logMessage: $"Created company {entity.Id} '{entity.Name}'",
                companyId: entity.Id,
                sourceFile: nameof(CompaniesService),
                sourceFunction: nameof(CreateAsync),
                ct: ct);
            return new CompanyDto { Id = entity.Id, Name = entity.Name };
        }

        public async Task<CompanyDto?> GetAsync(int id, CancellationToken ct)
        {
            var c = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            return c is null ? null : new CompanyDto { Id = c.Id, Name = c.Name };
        }

        public async Task<IReadOnlyList<CompanyDto>> ListAsync(string? q, int page, int pageSize, CancellationToken ct)
        {
            var query = _db.Companies.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(c => c.Name.Contains(q));
            return await query
                .OrderBy(c => c.Name)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(c => new CompanyDto { Id = c.Id, Name = c.Name })
                .ToListAsync(ct);
        }

        public async Task UpdateAsync(int id, CompanyUpsertDto dto, CancellationToken ct)
        {
            var c = await _db.Companies.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null) throw new KeyNotFoundException("Company not found.");
            c.Name = dto.Name;
            await _db.SaveChangesAsync(ct);
            await _appLogger.LogAsync(
                eventType: "AUDIT",
                level: "INFO",
                logCode: "COMPANY_UPDATE_OK",
                logMessage: $"Updated company {id} name='{dto.Name}'",
                companyId: id,
                sourceFile: nameof(CompaniesService),
                sourceFunction: nameof(UpdateAsync),
                ct: ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct)
        {
            var c = await _db.Companies.Include(x => x.Accounts).Include(x => x.Transactions)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null) return;

            // Choose policy: restrict if data exists (safer), or cascade (auto-remove children).
            if (c.Accounts.Any() || c.Transactions.Any())
            {
                await _appLogger.LogAsync(
                    eventType: "ERROR",
                    level: "WARN",
                    logCode: "COMPANY_DELETE_BLOCKED",
                    logMessage: $"Company {id} has Accounts/Transactions",
                    companyId: id,
                    sourceFile: nameof(CompaniesService),
                    sourceFunction: nameof(DeleteAsync),
                    ct: ct);
                throw new InvalidOperationException("Company has data; delete accounts/transactions first.");
            }

            _db.Companies.Remove(c);
            await _db.SaveChangesAsync(ct);
            await _appLogger.LogAsync(
                eventType: "AUDIT",
                level: "INFO",
                logCode: "COMPANY_DELETE_OK",
                logMessage: $"Deleted company {id}",
                companyId: id,
                sourceFile: nameof(CompaniesService),
                sourceFunction: nameof(DeleteAsync),
                ct: ct);
        }

        public async Task<IReadOnlyList<CompanyQuickPick>> GetQuickPicksAsync(string? search, int take = 24, CancellationToken ct = default)
        {
            var q = _db.Companies.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(c =>
                    EF.Functions.ILike(c.Name, $"%{s}%"));
            }

            return await q
                .OrderBy(c => c.Name)
                .Take(take)
                .Select(c => new CompanyQuickPick { Id = c.Id, Name = c.Name })
                .ToListAsync(ct);
        }
    }
}
