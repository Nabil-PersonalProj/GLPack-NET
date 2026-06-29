using GLPack.DAL;
using Microsoft.EntityFrameworkCore;
using static GLPack.Contracts.LedgerSearchDtos;

namespace GLPack.Services
{
    public sealed class LedgerSearchService : ILedgerSearchService
    {
        private readonly ApplicationDbContext _db;
        public LedgerSearchService(ApplicationDbContext db) => _db = db;

        public async Task<IReadOnlyList<LedgerRowDto>> SearchAsync(
            int companyId,
            string? q,
            string? accountCode,
            string? accountType,
            int? transactionNo,
            DateTime? from,
            DateTime? to,
            int page = 1,
            int pageSize = 200,
            CancellationToken ct = default)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 500);

            q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
            accountCode = string.IsNullOrWhiteSpace(accountCode) ? null : accountCode.Trim();
            accountType = string.IsNullOrWhiteSpace(accountType) ? null : accountType.Trim();
            SearchModifier? modifier = TryParseSearchModifier(q);

            var query =
                from e in _db.TransactionEntries.AsNoTracking()
                join t in _db.Transactions.AsNoTracking()
                    on new { e.CompanyId, e.TransactionNo } equals new { t.CompanyId, t.TransactionNo }
                join a in _db.Accounts.AsNoTracking()
                    on new { e.CompanyId, Code = e.AccountCode } equals new { a.CompanyId, Code = a.Code }
                where e.CompanyId == companyId
                select new { e, t, a };

            // Filters
            if (transactionNo is not null)
                query = query.Where(x => x.t.TransactionNo == transactionNo.Value);

            if (accountCode is not null)
                query = query.Where(x => x.a.Code == accountCode);

            if (accountType is not null)
                query = query.Where(x => x.a.Type == accountType);

            if (from is not null)
                query = query.Where(x => x.t.Date >= from.Value.Date);

            if (to is not null)
                query = query.Where(x => x.t.Date <= to.Value.Date);

            if (modifier is not null)
            {
                if (modifier.Value.Length == 0)
                {
                    query = query.Where(x => false);
                }
                else if (modifier.Key.Equals("t", StringComparison.OrdinalIgnoreCase))
                {
                    query = int.TryParse(modifier.Value, out int modifierTxNo)
                        ? query.Where(x => x.t.TransactionNo == modifierTxNo)
                        : query.Where(x => false);
                }
                else if (modifier.Key.Equals("a", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(x => EF.Functions.ILike(x.a.Code, $"{modifier.Value}%"));
                }
                else if (modifier.Key.Equals("memo", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(x =>
                        x.e.LineDescription != null &&
                        EF.Functions.ILike(x.e.LineDescription, $"%{modifier.Value}%"));
                }
            }
            else if (q is not null)
            {
                bool qIsInt = int.TryParse(q, out int qInt);

                query = query.Where(x =>
                    (qIsInt && x.t.TransactionNo == qInt) ||
                    EF.Functions.ILike(x.a.Code, $"%{q}%") ||
                    EF.Functions.ILike(x.a.Name, $"%{q}%") ||
                    (x.t.Description != null && EF.Functions.ILike(x.t.Description, $"%{q}%")) ||
                    (x.e.LineDescription != null && EF.Functions.ILike(x.e.LineDescription, $"%{q}%")) ||
                    (qIsInt && x.e.Debit == qInt) ||
                    (qIsInt && x.e.Credit == qInt)
                );
            }

            // Sort newest first
            query = query
                .OrderBy(x => x.t.Date)
                .ThenBy(x => x.t.TransactionNo)
                .ThenBy(x => x.a.Code);

            // Page + shape DTO
            List<LedgerRowDto> rows = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new LedgerRowDto
                {
                    Date = x.t.Date,
                    TransactionNo = x.t.TransactionNo,
                    TransactionDescription = x.t.Description,

                    AccountCode = x.a.Code,
                    AccountName = x.a.Name,
                    AccountType = x.a.Type,

                    LineDescription = x.e.LineDescription,
                    Debit = x.e.Debit,
                    Credit = x.e.Credit
                })
                .ToListAsync(ct);

            return rows;
        }

        private static SearchModifier? TryParseSearchModifier(string? q)
        {
            if (q is null) return null;

            int separatorIndex = q.IndexOf(':');
            if (separatorIndex <= 0) return null;

            string key = q[..separatorIndex].Trim();
            string value = q[(separatorIndex + 1)..].Trim();

            return key.Equals("t", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("a", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("memo", StringComparison.OrdinalIgnoreCase)
                ? new SearchModifier(key, value)
                : null;
        }

        private sealed record SearchModifier(string Key, string Value);
    }
}
