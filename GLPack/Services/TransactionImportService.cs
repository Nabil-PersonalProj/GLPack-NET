using System.Globalization;
using System.Text;
using GLPack.DAL;
using GLPack.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GLPack.Services
{
    public sealed class TransactionImportService : ITransactionImportService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAppLogger _logger;

        public TransactionImportService(ApplicationDbContext db, IAppLogger logger)
        {
            _db = db;
            _logger = logger;
        }

        private sealed record CsvRow(
            DateTime DateUtc,
            string SourceTrxNo,
            string AccountCode,
            string Particular,
            decimal Debit,
            decimal Credit);

        public async Task<int> ImportCsvAsync(int companyId, IFormFile csvFile, CancellationToken ct)
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                await _logger.LogAsync(
                  eventType: "ERROR",
                  level: "INFO",
                  logCode: "CSV_IMPORT_ERROR",
                  logMessage: "No CSV file",
                  companyId: companyId,
                  sourceFile: nameof(TransactionImportService),
                  sourceFunction: nameof(ImportCsvAsync),
                  ct: ct);
                throw new ArgumentException("No CSV file uploaded.");
            }

            var rows = await ReadCsvRowsAsync(csvFile, ct);
            if (rows.Count == 0)
                return 0;

            // Ensure company exists (cheap guard)
            var companyExists = await _db.Companies.AnyAsync(c => c.Id == companyId, ct);
            if (!companyExists)
                throw new KeyNotFoundException("Company not found.");

            // 1) Ensure accounts exist
            var codes = rows
                .Select(r => r.AccountCode)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var existing = await _db.Accounts
                .Where(a => a.CompanyId == companyId && codes.Contains(a.Code))
                .Select(a => a.Code)
                .ToListAsync(ct);

            var missing = codes
                .Except(existing, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (missing.Length > 0)
            {
                foreach (var code in missing)
                {
                    _db.Accounts.Add(new Account
                    {
                        CompanyId = companyId,
                        Code = code.Trim(),
                        Name = code.Trim(),
                        Type = "Uncategorized"
                    });
                }

                await _db.SaveChangesAsync(ct);
            }

            // 2) Allocate new TransactionNos
            var maxTxnNo = await _db.Transactions
                .Where(t => t.CompanyId == companyId)
                .Select(t => (int?)t.TransactionNo)
                .MaxAsync(ct) ?? 0;

            // Group key = Date + SourceTrxNo (as per requirement)
            // Use yyyy-MM-dd to avoid string-format differences.
            static string Key(DateTime utcDate, string sourceNo)
                => $"{utcDate:yyyy-MM-dd}|{sourceNo}";

            var orderedDistinctKeys = rows
                .Select(r => Key(r.DateUtc, r.SourceTrxNo))
                .Distinct()
                .ToList();

            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            var next = maxTxnNo + 1;
            foreach (var k in orderedDistinctKeys)
                map[k] = next++;

            // 3) Insert headers + lines
            await using var dbTx = await _db.Database.BeginTransactionAsync(ct);

            // Create headers (one per distinct key)
            var headers = new List<Transaction>(orderedDistinctKeys.Count);
            foreach (var k in orderedDistinctKeys)
            {
                // Parse date back out of key (first segment) - safe because we made it.
                var datePart = k.Split('|', 2)[0];
                var dt = DateTime.ParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);

                headers.Add(new Transaction
                {
                    CompanyId = companyId,
                    TransactionNo = map[k],
                    Date = dt,
                    Description = null
                });
            }

            _db.Transactions.AddRange(headers);
            await _db.SaveChangesAsync(ct);

            // Create lines
            var lines = new List<TransactionEntry>(rows.Count);
            foreach (var r in rows)
            {
                var k = Key(r.DateUtc, r.SourceTrxNo);
                lines.Add(new TransactionEntry
                {
                    CompanyId = companyId,
                    TransactionNo = map[k],
                    AccountCode = r.AccountCode,
                    LineDescription = string.IsNullOrWhiteSpace(r.Particular) ? null : r.Particular,
                    Debit = r.Debit,
                    Credit = r.Credit
                });
            }

            _db.TransactionEntries.AddRange(lines);
            await _db.SaveChangesAsync(ct);
            await dbTx.CommitAsync(ct);

            await _logger.LogAsync(
                eventType: "AUDIT",
                level: "INFO",
                logCode: "CSV_IMPORT_OK",
                logMessage: $"Imported {rows.Count} lines across {orderedDistinctKeys.Count} transactions",
                companyId: companyId,
                sourceFile: nameof(TransactionImportService),
                sourceFunction: nameof(ImportCsvAsync),
                ct: ct);

            return rows.Count;
        }

        private async Task<List<CsvRow>> ReadCsvRowsAsync(IFormFile csvFile, CancellationToken ct)
        {
            var result = new List<CsvRow>();

            await using var stream = csvFile.OpenReadStream();
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue; 
                }

                var cols = SplitCsvLine(line);
                if (cols.Count < 6)
                {
                    continue;
                }
                // fixed positions
                var dateStr = cols[0].Trim();
                var trxNo = cols[1].Trim();
                var ledger = cols[2].Trim();
                var particular = cols[3].Trim();
                var drStr = cols[4].Trim();
                var crStr = cols[5].Trim();

                var dateFormats = new[] { "dd/MM/yy", "dd/MM/yyyy" };
                if (!DateTime.TryParseExact(
                    dateStr,
                    dateFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dt))
                {
                    continue;
                }

                dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);

                var dr = string.IsNullOrWhiteSpace(drStr) ? 0m : decimal.Parse(drStr, NumberStyles.Any, CultureInfo.InvariantCulture);
                var cr = string.IsNullOrWhiteSpace(crStr) ? 0m : decimal.Parse(crStr, NumberStyles.Any, CultureInfo.InvariantCulture);

                result.Add(new CsvRow(
                    DateUtc: dt,
                    SourceTrxNo: trxNo,
                    AccountCode: ledger,
                    Particular: particular,
                    Debit: dr,
                    Credit: cr));
            }

            return result;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var cols = new List<string>();
            var sb = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];

                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote
                        sb.Append('"');
                        i++;
                        continue;
                    }

                    inQuotes = !inQuotes;
                    continue;
                }

                if (ch == ',' && !inQuotes)
                {
                    cols.Add(sb.ToString());
                    sb.Clear();
                    continue;
                }

                sb.Append(ch);
            }

            cols.Add(sb.ToString());
            return cols;
        }
    }
}
