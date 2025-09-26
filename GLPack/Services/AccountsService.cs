using GLPack.Contracts;
using GLPack.DAL;
using GLPack.Models;
using Microsoft.EntityFrameworkCore;

namespace GLPack.Services
{
    public class AccountsService : IAccountsService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAppLogger _appLogger;
        public AccountsService(ApplicationDbContext db, IAppLogger appLogger)
        {
            _db = db;
            _appLogger = appLogger;
        }

        public async Task<AccountDto?> GetAsync(int companyId, string accountCode, CancellationToken ct)
        {
            var a = await _db.Accounts.AsNoTracking()
                .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Code == accountCode, ct);
            return a is null ? null : new AccountDto
            {
                Id = a.Id,
                CompanyId = a.CompanyId,
                AccountCode = a.Code,
                Name = a.Name,
                Type = a.Type
            };
        }

        public async Task<IReadOnlyList<AccountDto>> ListAsync(int companyId, string? q, int page, int pageSize, CancellationToken ct)
        {
            var query = _db.Accounts.AsNoTracking().Where(a => a.CompanyId == companyId);
            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(a => a.Code.Contains(q) || a.Name.Contains(q));
            }

            var items = await query
                .OrderBy(a => a.Code)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AccountDto
                {
                    Id = a.Id,
                    CompanyId = a.CompanyId,
                    AccountCode = a.Code,
                    Name = a.Name,
                    Type = a.Type
                })
                .ToListAsync(ct);

            return items;
        }

        public async Task<AccountDto> CreateAsync(AccountUpsertDto dto, CancellationToken ct)
        {
            // Enforce unique (CompanyId, Code)
            var exists = await _db.Accounts.AnyAsync(a => a.CompanyId == dto.CompanyId && a.Code == dto.AccountCode, ct);
            if (exists)
            {
                await _appLogger.LogAsync(
                    eventType: "ERROR",
                    level: "WARN",
                    logCode: "ACCOUNTS_CODE_DUP",
                    logMessage: $"AccountCode '{dto.AccountCode}' already exists",
                    companyId: dto.CompanyId,
                    sourceFile: nameof(AccountsService),
                    sourceFunction: nameof(CreateAsync),
                    ct: ct);
                throw new InvalidOperationException("AccountCode already exists for this company.");
            }

            var entity = new Account
            {
                CompanyId = dto.CompanyId,
                Code = dto.AccountCode,
                Name = dto.Name,
                Type = dto.Type
            };

            _db.Accounts.Add(entity);
            await _db.SaveChangesAsync(ct);
            await _appLogger.LogAsync(
                eventType: "AUDIT",
                level: "INFO",
                logCode: "ACCOUNTS_CREATE_OK",
                logMessage: $"Created account {entity.Code}",
                companyId: entity.CompanyId,
                sourceFile: nameof(AccountsService),
                sourceFunction: nameof(CreateAsync),
                ct: ct);

            return new AccountDto
            {
                Id = entity.Id,
                CompanyId = entity.CompanyId,
                AccountCode = entity.Code,
                Name = entity.Name,
                Type = entity.Type
            };
        }

        public async Task UpdateAsync(int companyId, string accountCode, AccountUpsertDto dto, CancellationToken ct)
        {
            var a = await _db.Accounts.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Code == accountCode, ct);
            if (a is null) throw new KeyNotFoundException("Account not found.");

            // We do NOT let callers change the business key (Code) via update path.
            a.Name = dto.Name;
            a.Type = dto.Type;

            await _db.SaveChangesAsync(ct);
            await _appLogger.LogAsync(
                eventType: "AUDIT",
                level: "INFO",
                logCode: "ACCOUNTS_UPDATE_OK",
                logMessage: $"Updated account {accountCode}",
                companyId: companyId,
                sourceFile: nameof(AccountsService),
                sourceFunction: nameof(UpdateAsync),
                ct: ct);
        }

        public async Task DeleteAsync(int companyId, string accountCode, CancellationToken ct)
        {
            var a = await _db.Accounts.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Code == accountCode, ct);
            if (a is null) return;
            _db.Accounts.Remove(a);
            await _db.SaveChangesAsync(ct);
            await _appLogger.LogAsync(
                eventType: "AUDIT",
                level: "INFO",
                logCode: "ACCOUNTS_DELETE_OK",
                logMessage: $"Deleted account {accountCode}",
                companyId: companyId,
                sourceFile: nameof(AccountsService),
                sourceFunction: nameof(DeleteAsync),
                ct: ct);
        }
    }
}
