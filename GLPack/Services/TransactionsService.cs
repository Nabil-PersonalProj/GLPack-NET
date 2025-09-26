using GLPack.Contracts;
using GLPack.DAL;                 // <-- ensure this matches your AppDbContext namespace
using GLPack.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using Tx = GLPack.Contracts.TransactionsDtos;

namespace GLPack.Services
{
    public sealed class TransactionsService : ITransactionsService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAppLogger _appLogger;
        public TransactionsService(ApplicationDbContext db, IAppLogger appLogger) {_db = db;_appLogger = appLogger;}

        public async Task<Tx.TransactionDto> CreateAsync(Tx.TransactionCreateDto dto, CancellationToken ct)
        {
            // Validate Payload
            if (dto.Entries == null || dto.Entries.Count < 2)
                throw new ArgumentException("A transaction must have at least two entries.");

            foreach (var entry in dto.Entries)
            {
                if (entry.Amount <= 0)
                    throw new ArgumentException("Entry amounts must be positive.");
                if (entry.DrCr != "DR" && entry.DrCr != "CR")
                    throw new ArgumentException("Entry DrCr must be 'DR' or 'CR'.");
            }

            var totalDr = dto.Entries.Where(e => e.DrCr.Equals("DR", StringComparison.OrdinalIgnoreCase)).Sum(e => e.Amount);
            var totalCr = dto.Entries.Where(e => e.DrCr.Equals("CR", StringComparison.OrdinalIgnoreCase)).Sum(e => e.Amount);

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
            var codes = dto.Entries.Select(e => e.AccountCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var existingCodes = await _db.Accounts
                .Where(a => a.CompanyId == dto.CompanyId && codes.Contains(a.Code))
                .Select(a => a.Code)
                .ToListAsync(ct);

            var missing = codes.Except(existingCodes, StringComparer.OrdinalIgnoreCase).ToArray();
            if (missing.Length > 0)
            {
                throw new ArgumentException($"The following account codes do not exist: {string.Join(", ", missing)}");
            }

            // ensure unique transaction no per company
            var exists = await _db.Transactions.AnyAsync(t =>
            t.CompanyId == dto.CompanyId && t.TransactionNo == dto.TransactionNo, ct);
            if (exists)
            {
                throw new InvalidOperationException("TransactionNo already exists for this company.");
            }

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            var utcDate = DateTime.SpecifyKind(dto.TxnDate, DateTimeKind.Utc);
            var header = new Transaction
            {
                CompanyId = dto.CompanyId,
                TransactionNo = dto.TransactionNo, // int
                Date = utcDate,       // map DTO.TxnDate -> model.Date
                Description = dto.Description
            };

            _db.Transactions.Add(header);
            await _db.SaveChangesAsync(ct);

            var lines = dto.Entries.Select(e => new TransactionEntry
            {
                CompanyId = dto.CompanyId,
                TransactionNo = dto.TransactionNo,
                AccountCode = e.AccountCode,
                LineDescription = e.Memo,
                Debit = e.DrCr.Equals("DR", StringComparison.OrdinalIgnoreCase) ? e.Amount : 0m,
                Credit = e.DrCr.Equals("CR", StringComparison.OrdinalIgnoreCase) ? e.Amount : 0m
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
                    Amount = l.Debit > 0 ? l.Debit : l.Credit,
                    DrCr = l.Debit > 0 ? "DR" : "CR",
                    Memo = l.LineDescription
                }).ToList()
            };
        }

        public async Task<Tx.TransactionDto?> GetAsync(int companyId, int transactionNo, CancellationToken ct)
        {
            var h = await _db.Transactions.AsNoTracking()
                .FirstOrDefaultAsync(t => t.CompanyId == companyId && t.TransactionNo == transactionNo, ct);
            if (h is null) return null;

            var e = await _db.TransactionEntries.AsNoTracking()
                .Where(l => l.CompanyId == companyId && l.TransactionNo == transactionNo)
                .OrderBy(l => l.Id)
                .Select(l => new Tx.TransactionEntryDto
                {
                    AccountCode = l.AccountCode,
                    Amount = l.Debit > 0 ? l.Debit : l.Credit,
                    DrCr = l.Debit > 0 ? "DR" : "CR",
                    Memo = l.LineDescription
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

        public async Task<(IReadOnlyList<Tx.TransactionDto> Items, int Total)> ListAsync(
        int companyId, int page, int pageSize, DateTime? from, DateTime? to, CancellationToken ct)
        {
            var q = _db.Transactions.AsNoTracking().Where(t => t.CompanyId == companyId);
            if (from.HasValue) q = q.Where(t => t.Date >= from.Value.Date);
            if (to.HasValue) q = q.Where(t => t.Date < to.Value.Date.AddDays(1));

            var total = await q.CountAsync(ct);

            var headers = await q.OrderByDescending(t => t.Date)
                .ThenBy(t => t.TransactionNo)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var txnNos = headers.Select(h => h.TransactionNo).ToArray();

            var allLines = await _db.TransactionEntries.AsNoTracking()
                .Where(l => l.CompanyId == companyId && txnNos.Contains(l.TransactionNo))
                .OrderBy(l => l.TransactionNo).ThenBy(l => l.Id)
                .ToListAsync(ct);

            var grouped = allLines.GroupBy(l => l.TransactionNo).ToDictionary(
                g => g.Key,
                g => g.Select(l => new Tx.TransactionEntryDto
                {
                    AccountCode = l.AccountCode,
                    Amount = l.Debit > 0 ? l.Debit : l.Credit,
                    DrCr = l.Debit > 0 ? "DR" : "CR",
                    Memo = l.LineDescription
                }).ToList());

            var items = headers.Select(h => new Tx.TransactionDto
            {
                CompanyId = h.CompanyId,
                TransactionNo = h.TransactionNo,
                TxnDate = h.Date,
                Description = h.Description,
                Entries = grouped.TryGetValue(h.TransactionNo, out var es) ? es : new List<Tx.TransactionEntryDto>()
            }).ToList();

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

            foreach (var e in dto.Entries)
            {
                if (e.Amount <= 0m) throw new InvalidOperationException("All amounts must be positive.");
                var side = e.DrCr?.Trim().ToUpperInvariant();
                if (side is not ("DR" or "CR"))
                    throw new InvalidOperationException("DrCr must be 'DR' or 'CR'.");
            }
            var totalDr = dto.Entries.Where(x => x.DrCr.Equals("DR", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount);
            var totalCr = dto.Entries.Where(x => x.DrCr.Equals("CR", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount);
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
            var codes = dto.Entries.Select(e => e.AccountCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var existingCodes = await _db.Accounts
                .Where(a => a.CompanyId == dto.CompanyId && codes.Contains(a.Code))
                .Select(a => a.Code)
                .ToListAsync(ct);
            var missing = codes.Except(existingCodes, StringComparer.OrdinalIgnoreCase).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException($"Missing accounts for CompanyId={dto.CompanyId}: {string.Join(", ", missing)}");

            // 3) Load existing header + lines
            var header = await _db.Transactions
                .FirstOrDefaultAsync(t => t.CompanyId == companyId && t.TransactionNo == transactionNo, ct);
            if (header is null) throw new KeyNotFoundException("Transaction not found.");

            // 4) Persist inside a DB transaction: replace header+lines
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            // Normalize date to UTC to satisfy timestamptz
            var utcDate = dto.TxnDate.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dto.TxnDate, DateTimeKind.Utc)
                : dto.TxnDate.ToUniversalTime();

            header.Date = utcDate;
            header.Description = dto.Description;

            // Remove existing lines then insert new ones
            var existingLines = await _db.TransactionEntries
                .Where(l => l.CompanyId == companyId && l.TransactionNo == transactionNo)
                .ToListAsync(ct);
            _db.TransactionEntries.RemoveRange(existingLines);
            await _db.SaveChangesAsync(ct);

            var newLines = dto.Entries.Select(e => new TransactionEntry
            {
                CompanyId = companyId,
                TransactionNo = transactionNo,
                AccountCode = e.AccountCode,
                LineDescription = e.Memo,
                Debit = e.DrCr.Equals("DR", StringComparison.OrdinalIgnoreCase) ? e.Amount : 0m,
                Credit = e.DrCr.Equals("CR", StringComparison.OrdinalIgnoreCase) ? e.Amount : 0m
            }).ToList();

            _db.TransactionEntries.AddRange(newLines);
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
                Entries = newLines.Select(l => new Tx.TransactionEntryDto
                {
                    AccountCode = l.AccountCode,
                    Amount = l.Debit > 0 ? l.Debit : l.Credit,
                    DrCr = l.Debit > 0 ? "DR" : "CR",
                    Memo = l.LineDescription
                }).ToList()
            };
        }

        public async Task DeleteAsync(int companyId, int transactionNo, CancellationToken ct)
        {
            // Option A: hard delete lines then header (safe regardless of FK cascade)
            var lines = await _db.TransactionEntries
                .Where(l => l.CompanyId == companyId && l.TransactionNo == transactionNo)
                .ToListAsync(ct);

            var header = await _db.Transactions
                .FirstOrDefaultAsync(t => t.CompanyId == companyId && t.TransactionNo == transactionNo, ct);

            if (header is null) return;

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

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
    }

}

