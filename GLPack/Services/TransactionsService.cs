using GLPack.DAL;
using GLPack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Tx = GLPack.Contracts.TransactionsDtos;

namespace GLPack.Services
{
    public sealed class TransactionsService : ITransactionsService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAppLogger _appLogger;
        public TransactionsService(ApplicationDbContext db, IAppLogger appLogger) { _db = db; _appLogger = appLogger; }

        public async Task<Tx.TransactionDto> CreateAsync(Tx.TransactionCreateDto dto, CancellationToken ct)
        {
            // Validate Payload
            if (dto.Entries == null || dto.Entries.Count < 2)
                throw new ArgumentException("A transaction must have at least two entries.");

            foreach (Tx.TransactionEntryDto entry in dto.Entries)
            {
                if (entry.Debit < 0m || entry.Credit < 0m)
                    throw new ArgumentException("Entry debit and credit must be zero or positive.");
            }

            decimal totalDr = dto.Entries.Sum(e => e.Debit);
            decimal totalCr = dto.Entries.Sum(e => e.Credit);

            if (Math.Round(totalDr - totalCr, 2) != 0m)
            {
                await _appLogger.LogAsync(
                    eventType: "ERROR",
                    level: "WARN",
                    logCode: "TX_UNBALANCED",
                    logMessage: $"DR={totalDr} CR={totalCr} for txn {dto.TransactionNo}",
                    companyId: dto.CompanyId,
                    sourceFile: nameof(TransactionsService),
                    sourceFunction: nameof(CreateAsync),
                    ct: ct
                    );
                throw new InvalidOperationException("Entries are not balanced (DR != CR).");
            }

            // check if company exists
            string[] codes = dto.Entries.Select(e => e.AccountCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            List<string> existingCodes = await _db.Accounts
                .Where(a => a.CompanyId == dto.CompanyId && codes.Contains(a.Code))
                .Select(a => a.Code)
                .ToListAsync(ct);

            string[] missing = codes.Except(existingCodes, StringComparer.OrdinalIgnoreCase).ToArray();
            if (missing.Length > 0)
            {
                throw new ArgumentException($"The following account codes do not exist: {string.Join(", ", missing)}");
            }

            // ensure unique transaction no per company
            bool exists = await _db.Transactions.AnyAsync(t =>
            t.CompanyId == dto.CompanyId && t.TransactionNo == dto.TransactionNo, ct);
            if (exists)
            {
                throw new InvalidOperationException("TransactionNo already exists for this company.");
            }

            await using IDbContextTransaction tx = await _db.Database.BeginTransactionAsync(ct);
            DateTime utcDate = DateTime.SpecifyKind(dto.TxnDate, DateTimeKind.Utc);
            Transaction header = new Transaction
            {
                CompanyId = dto.CompanyId,
                TransactionNo = dto.TransactionNo, // int
                Date = utcDate,       // map DTO.TxnDate -> model.Date
                Description = dto.Description
            };

            _db.Transactions.Add(header);
            await _db.SaveChangesAsync(ct);

            List<TransactionEntry> lines = dto.Entries.Select(e => new TransactionEntry
            {
                CompanyId = dto.CompanyId,
                TransactionNo = dto.TransactionNo,
                AccountCode = e.AccountCode,
                LineDescription = e.Memo,
                Debit = e.Debit,
                Credit = e.Credit,
                HasError = IsZeroZeroEntry(e.Debit, e.Credit)
            }).ToList();

            _db.TransactionEntries.AddRange(lines);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _appLogger.LogAsync(
                eventType: "AUDIT",
                level: "INFO",
                logCode: "TX_CREATE_OK",
                logMessage: $"Created transaction {dto.TransactionNo}",
                companyId: dto.CompanyId,
                sourceFile: nameof(TransactionsService),
                sourceFunction: nameof(CreateAsync),
                ct: ct
                );

            return new Tx.TransactionDto
            {
                CompanyId = header.CompanyId,
                TransactionNo = header.TransactionNo,
                TxnDate = header.Date,       // model.Date -> DTO.TxnDate
                Description = header.Description,
                Entries = lines.Select(l => new Tx.TransactionEntryDto
                {
                    AccountCode = l.AccountCode,
                    Debit = l.Debit,
                    Credit = l.Credit,
                    Memo = l.LineDescription,
                    HasError = l.HasError || IsZeroZeroEntry(l.Debit, l.Credit)
                }).ToList()
            };
        }

        public async Task<Tx.TransactionDto?> GetAsync(int companyId, int transactionNo, CancellationToken ct)
        {
            Transaction? h = await _db.Transactions.AsNoTracking()
                .FirstOrDefaultAsync(t => t.CompanyId == companyId && t.TransactionNo == transactionNo, ct);
            if (h is null) return null;

            List<Tx.TransactionEntryDto> e = await _db.TransactionEntries.AsNoTracking()
                .Where(l => l.CompanyId == companyId && l.TransactionNo == transactionNo)
                .OrderBy(l => l.Id)
                .Select(l => new Tx.TransactionEntryDto
                {
                    AccountCode = l.AccountCode,
                    Debit = l.Debit,
                    Credit = l.Credit,
                    Memo = l.LineDescription,
                    HasError = l.HasError || IsZeroZeroEntry(l.Debit, l.Credit)
                })
                .ToListAsync(ct);

            return new Tx.TransactionDto
            {
                CompanyId = h.CompanyId,
                TransactionNo = h.TransactionNo,
                TxnDate = h.Date,
                Description = h.Description,
                Entries = e
            };
        }

        public async Task<(IReadOnlyList<Tx.TransactionDto> Items, int Total)> ListAsync(int companyId, int page, int pageSize, string? q, int? transactionNo, DateTime? from, DateTime? to, CancellationToken ct)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 10 : pageSize;

            IQueryable<Transaction> query = _db.Transactions
                .AsNoTracking()
                .Where(t => t.CompanyId == companyId);
            if (transactionNo.HasValue)
            {
                query = query.Where(t => t.TransactionNo == transactionNo.Value);
            }
            else if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                string pattern = $"%{q}%";

                query = query.Where(t =>
                    EF.Functions.ILike(t.TransactionNo.ToString(), pattern) ||
                    (t.Description != null && EF.Functions.ILike(t.Description, pattern)) ||
                    _db.TransactionEntries.Any(e =>
                        e.CompanyId == t.CompanyId &&
                        e.TransactionNo == t.TransactionNo &&
                        (
                            EF.Functions.ILike(e.AccountCode, pattern) ||
                            (e.LineDescription != null && EF.Functions.ILike(e.LineDescription, pattern))
                        )
                    ));
            }

            if (from.HasValue)
                query = query.Where(t => t.Date >= from.Value.Date);

            if (to.HasValue)
                query = query.Where(t => t.Date < to.Value.Date.AddDays(1));

            int total = await query.CountAsync(ct);

            List<Transaction> headers = await query
                .OrderByDescending(t => _db.TransactionEntries.Any(e =>
                    e.CompanyId == t.CompanyId &&
                    e.TransactionNo == t.TransactionNo &&
                    (e.HasError || (e.Debit == 0m && e.Credit == 0m))))
                .ThenByDescending(t => t.Date)
                .ThenByDescending(t => t.TransactionNo)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            List<int> transactionNos = headers.Select(t => t.TransactionNo).ToList();

            List<TransactionEntry> lines = await _db.TransactionEntries
                .AsNoTracking()
                .Where(i => i.CompanyId == companyId && transactionNos.Contains(i.TransactionNo))
                .OrderByDescending(i => i.HasError || (i.Debit == 0m && i.Credit == 0m))
                .ThenBy(i => i.TransactionNo)
                .ThenBy(i => i.Id)
                .ToListAsync(ct);

            Dictionary<int, IReadOnlyList<Tx.TransactionEntryDto>> linesLookup = lines
                .GroupBy(x => x.TransactionNo)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<Tx.TransactionEntryDto>)g.Select(x => new Tx.TransactionEntryDto
                    {
                        AccountCode = x.AccountCode,
                        Debit = x.Debit,
                        Credit = x.Credit,
                        Memo = x.LineDescription,
                        HasError = x.HasError || IsZeroZeroEntry(x.Debit, x.Credit)
                    }).ToList()
                );

            List<Tx.TransactionDto> items = headers
                .Select(t => new Tx.TransactionDto
                {
                    CompanyId = t.CompanyId,
                    TransactionNo = t.TransactionNo,
                    TxnDate = t.Date,
                    Description = t.Description,
                    Entries = linesLookup.TryGetValue(t.TransactionNo, out var entries)
                        ? entries
                        : Array.Empty<Tx.TransactionEntryDto>()
                })
                .ToList();

            return (items, total);
        }

        public async Task<Tx.TransactionDto> UpdateAsync(int companyId, int transactionNo, Tx.TransactionCreateDto dto, CancellationToken ct)
        {
            if (dto.CompanyId != companyId)
                throw new InvalidOperationException("Mismatched companyId.");
            if (dto.TransactionNo != transactionNo)
                throw new InvalidOperationException("Changing TransactionNo is not allowed.");

            // 1) Validate payload (same rules as create)
            if (dto.Entries is null || dto.Entries.Count < 2)
                throw new InvalidOperationException("Transaction must have at least 2 entries.");

            foreach (Tx.TransactionEntryDto e in dto.Entries)
            {
                if (e.Debit < 0m || e.Credit < 0m)
                    throw new InvalidOperationException("All debit and credit amounts must be zero or positive.");
            }
            decimal totalDr = dto.Entries.Sum(x => x.Debit);
            decimal totalCr = dto.Entries.Sum(x => x.Credit);
            if (Math.Round(totalDr - totalCr, 2) != 0m)
            {
                await _appLogger.LogAsync(
                    eventType: "ERROR",
                    level: "WARN",
                    logCode: "TX_UNBALANCED",
                    logMessage: $"DR={totalDr} CR={totalCr} for txn {dto.TransactionNo}",
                    companyId: dto.CompanyId,
                    sourceFile: nameof(TransactionsService),
                    sourceFunction: nameof(UpdateAsync),
                    ct: ct
                    );
                throw new InvalidOperationException("Entries are not balanced (DR != CR).");
            }

            // 2) Ensure accounts exist
            string[] codes = dto.Entries.Select(e => e.AccountCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            List<string> existingCodes = await _db.Accounts
                .Where(a => a.CompanyId == dto.CompanyId && codes.Contains(a.Code))
                .Select(a => a.Code)
                .ToListAsync(ct);
            string[] missing = codes.Except(existingCodes, StringComparer.OrdinalIgnoreCase).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException($"Missing accounts for CompanyId={dto.CompanyId}: {string.Join(", ", missing)}");

            // 3) Load existing header + lines
            Transaction? header = await _db.Transactions
                .FirstOrDefaultAsync(t => t.CompanyId == companyId && t.TransactionNo == transactionNo, ct);
            if (header is null) throw new KeyNotFoundException("Transaction not found.");

            // 4) Persist inside a DB transaction: replace header+lines
            await using IDbContextTransaction tx = await _db.Database.BeginTransactionAsync(ct);

            // Normalize date to UTC to satisfy timestamptz
            DateTime utcDate = dto.TxnDate.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dto.TxnDate, DateTimeKind.Utc)
                : dto.TxnDate.ToUniversalTime();

            header.Date = utcDate;
            header.Description = dto.Description;

            // Remove existing lines then insert new ones
            List<TransactionEntry> existingLines = await _db.TransactionEntries
                .Where(l => l.CompanyId == companyId && l.TransactionNo == transactionNo)
                .ToListAsync(ct);
            _db.TransactionEntries.RemoveRange(existingLines);
            await _db.SaveChangesAsync(ct);

            List<TransactionEntry> lines = dto.Entries.Select(e => new TransactionEntry
            {
                CompanyId = companyId,
                TransactionNo = transactionNo,
                AccountCode = e.AccountCode,
                LineDescription = e.Memo,
                Debit = e.Debit,
                Credit = e.Credit,
                HasError = IsZeroZeroEntry(e.Debit, e.Credit)
            }).ToList();

            _db.TransactionEntries.AddRange(lines);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _appLogger.LogAsync(
                eventType: "AUDIT",
                level: "INFO",
                logCode: "TX_UPDATE_OK",
                logMessage: $"Updated transaction {transactionNo}",
                companyId: companyId,
                sourceFile: nameof(TransactionsService),
                sourceFunction: nameof(UpdateAsync),
                ct: ct
                );

            // 5) Return DTO
            return new Tx.TransactionDto
            {
                CompanyId = header.CompanyId,
                TransactionNo = header.TransactionNo,
                TxnDate = header.Date,
                Description = header.Description,
                Entries = lines.Select(l => new Tx.TransactionEntryDto
                {
                    AccountCode = l.AccountCode,
                    Debit = l.Debit,
                    Credit = l.Credit,
                    Memo = l.LineDescription,
                    HasError = l.HasError || IsZeroZeroEntry(l.Debit, l.Credit)
                }).ToList()
            };
        }

        public async Task DeleteAsync(int companyId, int transactionNo, CancellationToken ct)
        {
            // Option A: hard delete lines then header (safe regardless of FK cascade)
            List<TransactionEntry> lines = await _db.TransactionEntries
                .Where(l => l.CompanyId == companyId && l.TransactionNo == transactionNo)
                .ToListAsync(ct);

            Transaction? header = await _db.Transactions
                .FirstOrDefaultAsync(t => t.CompanyId == companyId && t.TransactionNo == transactionNo, ct);

            if (header is null) return;

            await using IDbContextTransaction tx = await _db.Database.BeginTransactionAsync(ct);

            if (lines.Count > 0)
            {
                _db.TransactionEntries.RemoveRange(lines);
                await _db.SaveChangesAsync(ct);
            }

            _db.Transactions.Remove(header);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            await _appLogger.LogAsync(
                eventType: "AUDIT",
                level: "INFO",
                logCode: "TX_DELETE_OK",
                logMessage: $"Deleted transaction {transactionNo}",
                companyId: companyId,
                sourceFile: nameof(TransactionsService),
                sourceFunction: nameof(DeleteAsync),
                ct: ct
            );
        }

        private static bool IsZeroZeroEntry(decimal debit, decimal credit)
            => debit == 0m && credit == 0m;
    }

}

