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
            Account? a = await _db.Accounts.AsNoTracking()
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

        public async Task<PagedResult<AccountDto>> ListAsync(int companyId, string? q, int page, int pageSize, CancellationToken ct)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 10 : pageSize;

            IQueryable<Account> query = _db.Accounts.AsNoTracking().Where(a => a.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(a => a.Code.Contains(q) || a.Name.Contains(q));
            }

            int totalCount = await query.CountAsync(ct);

            List<AccountDto> items = await query
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

            return new PagedResult<AccountDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task UpdateAsync(int companyId, string accountCode, AccountUpsertDto dto, CancellationToken ct)
        {
            Account? a = await _db.Accounts.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Code == accountCode, ct);
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
            Account? a = await _db.Accounts.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Code == accountCode, ct);
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

        public async Task<AccountDto> CreateFromPrefixAsync(AccountCreateFromPrefixDto dto, CancellationToken ct)
        {
            string prefix = (dto.Prefix ?? "").Trim().ToUpperInvariant();
            string name = (dto.Name ?? "").Trim();

            if (string.IsNullOrWhiteSpace(prefix))
                throw new InvalidOperationException("Prefix is required.");

            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Account name is required.");

            AccountTypePrefix? prefixRule = await _db.AccountTypePrefixes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Prefix == prefix, ct);

            if (prefixRule is null)
                throw new InvalidOperationException($"Prefix rule '{prefix}' was not found.");

            string nextCode = await GenerateNextAccountCodeAsync(dto.CompanyId, prefix, ct);

            Account entity = new Account
            {
                CompanyId = dto.CompanyId,
                Code = nextCode,
                Name = name,
                Type = prefixRule.AccountType
            };

            _db.Accounts.Add(entity);

            if (prefix == "FA")
            {
                string numberPart = nextCode[prefix.Length..];
                string pdCode = $"PD{numberPart}";

                bool pdExists = await _db.Accounts.AnyAsync(
                    x => x.CompanyId == dto.CompanyId && x.Code == pdCode,
                    ct);

                if (pdExists)
                    throw new InvalidOperationException($"Linked depreciation account '{pdCode}' already exists.");

                AccountTypePrefix? pdPrefixRule = await _db.AccountTypePrefixes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Prefix == "PD", ct);

                if (pdPrefixRule is null)
                    throw new InvalidOperationException("PD prefix rule was not found.");

                Account entityPD = new Account
                {
                    CompanyId = dto.CompanyId,
                    Code = pdCode,
                    Name = $"{name} - {nextCode}",
                    Type = pdPrefixRule.AccountType
                };

                _db.Accounts.Add(entityPD);
            }

            await _db.SaveChangesAsync(ct);

            await _appLogger.LogAsync(
                eventType: "AUDIT",
                level: "INFO",
                logCode: "ACCOUNTS_CREATE_FROM_PREFIX_OK",
                logMessage: $"Created account {entity.Code} from prefix {prefix}",
                companyId: entity.CompanyId,
                sourceFile: nameof(AccountsService),
                sourceFunction: nameof(CreateFromPrefixAsync),
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
        private async Task<string> GenerateNextAccountCodeAsync(int companyId, string prefix, CancellationToken ct)
        {
            List<string> existingCodes = await _db.Accounts
                .AsNoTracking()
                .Where(a => a.CompanyId == companyId && a.Code.StartsWith(prefix))
                .Select(a => a.Code)
                .ToListAsync(ct);

            int maxNumber = 0;

            foreach (string code in existingCodes)
            {
                if (code.Length <= prefix.Length)
                    continue;

                string numberPart = code[prefix.Length..];

                if (int.TryParse(numberPart, out int number) && number > maxNumber)
                {
                    maxNumber = number;
                }
            }

            int nextNumber = maxNumber + 1;
            return $"{prefix}{nextNumber:000}";
        }
    }
}
