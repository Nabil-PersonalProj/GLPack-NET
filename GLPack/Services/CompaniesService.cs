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
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ICompanyAccessService _companyAccess;

        public CompaniesService(
            ApplicationDbContext db,
            IAppLogger appLogger,
            IHttpContextAccessor httpContextAccessor,
            ICompanyAccessService companyAccess)
        {
            _db = db;
            _appLogger = appLogger;
            _httpContextAccessor = httpContextAccessor;
            _companyAccess = companyAccess;
        }

        public async Task<CompanyDto> CreateAsync(CompanyUpsertDto dto, CancellationToken ct)
        {
            Company entity = new Company
            {
                Name = dto.Name.Trim(),
                Currency = NormalizeCurrency(dto.Currency),
                FinancialYearEnd = NormalizeFinancialYearEnd(dto.FinancialYearEnd)
            };
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
            return ToDto(entity);
        }

        public async Task<CompanyDto?> GetAsync(int id, CancellationToken ct)
        {
            IQueryable<Company> query = _companyAccess.ApplyCompanyAccess(_db.Companies.AsNoTracking(), CurrentUser);
            Company? c = await query.FirstOrDefaultAsync(x => x.Id == id, ct);
            return c is null ? null : ToDto(c);
        }

        public async Task<IReadOnlyList<CompanyDto>> ListAsync(string? q, int page, int pageSize, CancellationToken ct)
        {
            IQueryable<Company> query = _companyAccess.ApplyCompanyAccess(_db.Companies.AsNoTracking(), CurrentUser);
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(c => c.Name.Contains(q));
            return await query
                .OrderBy(c => c.Name)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(c => new CompanyDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Currency = c.Currency,
                    FinancialYearEnd = c.FinancialYearEnd
                })
                .ToListAsync(ct);
        }

        public async Task UpdateAsync(int id, CompanyUpsertDto dto, CancellationToken ct)
        {
            Company? c = await _db.Companies.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null)
            {
                await _appLogger.LogAsync(
                    eventType: "ERROR",
                    level: "WARN",
                    logCode: "COMPANY_UPDATE_REJECTED",
                    logMessage: $"Company {id} was not found.",
                    companyId: id,
                    sourceFile: nameof(CompaniesService),
                    sourceFunction: nameof(UpdateAsync),
                    ct: ct);

                throw new KeyNotFoundException("Company not found.");
            }
            c.Name = dto.Name.Trim();
            c.Currency = NormalizeCurrency(dto.Currency);
            c.FinancialYearEnd = NormalizeFinancialYearEnd(dto.FinancialYearEnd);
            await _db.SaveChangesAsync(ct);
            await _appLogger.LogAsync(
                eventType: "AUDIT",
                level: "INFO",
                logCode: "COMPANY_UPDATE_OK",
                logMessage: $"Updated company {id} name='{c.Name}' currency='{c.Currency}' financialYearEnd='{c.FinancialYearEnd}'",
                companyId: id,
                sourceFile: nameof(CompaniesService),
                sourceFunction: nameof(UpdateAsync),
                ct: ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct)
        {
            Company? c = await _db.Companies.Include(x => x.Accounts).Include(x => x.Transactions)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null)
            {
                await _appLogger.LogAsync(
                    eventType: "ERROR",
                    level: "WARN",
                    logCode: "COMPANY_DELETE_REJECTED",
                    logMessage: $"Company {id} was not found.",
                    companyId: id,
                    sourceFile: nameof(CompaniesService),
                    sourceFunction: nameof(DeleteAsync),
                    ct: ct);

                return;
            }

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
            IQueryable<Company> q = _companyAccess.ApplyCompanyAccess(_db.Companies.AsNoTracking(), CurrentUser);

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                q = q.Where(c =>
                    EF.Functions.ILike(c.Name, $"%{s}%"));
            }

            return await q
                .OrderBy(c => c.Name)
                .Take(take)
                .Select(c => new CompanyQuickPick { Id = c.Id, Name = c.Name })
                .ToListAsync(ct);
        }

        private System.Security.Claims.ClaimsPrincipal CurrentUser =>
            _httpContextAccessor.HttpContext?.User ?? new System.Security.Claims.ClaimsPrincipal();

        private static CompanyDto ToDto(Company company)
        {
            return new CompanyDto
            {
                Id = company.Id,
                Name = company.Name,
                Currency = company.Currency,
                FinancialYearEnd = company.FinancialYearEnd
            };
        }

        private static string NormalizeCurrency(string? currency)
        {
            string normalized = (currency ?? "SGD").Trim().ToUpperInvariant();
            return string.IsNullOrWhiteSpace(normalized) ? "SGD" : normalized;
        }

        private static string NormalizeFinancialYearEnd(string? financialYearEnd)
        {
            string normalized = (financialYearEnd ?? "12-31").Trim();
            return string.IsNullOrWhiteSpace(normalized) ? "12-31" : normalized;
        }
    }
}
