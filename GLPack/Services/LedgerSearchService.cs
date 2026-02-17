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

            // Base: ledger lines (TransactionEntry) joined with Transaction + Account
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

            if (from is not null)
                query = query.Where(x => x.t.Date >= from.Value.Date);

            if (to is not null)
                query = query.Where(x => x.t.Date <= to.Value.Date);

            if (q is not null)
            {
                var qIsInt = int.TryParse(q, out var qInt);

                query = query.Where(x =>
                    (qIsInt && x.t.TransactionNo == qInt) ||
                    EF.Functions.ILike(x.a.Code, $"%{q}%") ||
                    EF.Functions.ILike(x.a.Name, $"%{q}%") ||
                    (x.t.Description != null && EF.Functions.ILike(x.t.Description, $"%{q}%")) ||
                    (x.e.LineDescription != null && EF.Functions.ILike(x.e.LineDescription, $"%{q}%"))
                );
            }

            // Sort newest first
            query = query
                .OrderByDescending(x => x.t.Date)
                .ThenByDescending(x => x.t.TransactionNo)
                .ThenBy(x => x.a.Code);

            // Page + shape DTO
            var rows = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new LedgerRowDto
                {
                    Date = x.t.Date,
                    TransactionNo = x.t.TransactionNo,
                    TransactionDescription = x.t.Description,

                    AccountCode = x.a.Code,
                    AccountName = x.a.Name,

                    LineDescription = x.e.LineDescription,
                    Debit = x.e.Debit,
                    Credit = x.e.Credit
                })
                .ToListAsync(ct);

            return rows;
        }
    }
}
