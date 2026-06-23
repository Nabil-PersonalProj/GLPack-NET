using GLPack.DAL;
using GLPack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Globalization;
using System.Text;

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

        private sealed class CsvRow
        {
            public int LineNumber { get; init; }
            public required DateTime DateUtc { get; init; }
            public required string SourceTrxNo { get; init; }
            public required string AccountCode { get; init; }
            public required string Particular { get; init; }
            public required decimal Debit { get; init; }
            public required decimal Credit { get; init; }
            public bool HasError { get; set; }
        }

        private sealed record ParsedCsvRows(
            List<CsvRow> Rows,
            List<SkippedImportLine> SkippedLines);

        public async Task<TransactionImportResult> ImportCsvAsync(int companyId, IFormFile csvFile, CancellationToken ct)
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

            ParsedCsvRows parsed = await ReadCsvRowsAsync(csvFile, ct);
            List<CsvRow> rows = parsed.Rows;
            if (rows.Count == 0)
                return new TransactionImportResult(0, 0, parsed.SkippedLines);

            // Ensure company exists (cheap guard)
            bool companyExists = await _db.Companies.AnyAsync(c => c.Id == companyId, ct);
            if (!companyExists)
                throw new KeyNotFoundException("Company not found.");

            // Group key = Date + SourceTrxNo (as per requirement)
            // Use yyyy-MM-dd to avoid string-format differences.
            static string Key(DateTime utcDate, string sourceNo)
                => $"{utcDate:yyyy-MM-dd}|{sourceNo}";

            foreach (IGrouping<string, CsvRow> group in rows.GroupBy(r => Key(r.DateUtc, r.SourceTrxNo)))
            {
                decimal totalDr = group.Sum(r => r.Debit);
                decimal totalCr = group.Sum(r => r.Credit);
                if (Math.Round(totalDr - totalCr, 2) != 0m)
                {
                    foreach (CsvRow row in group)
                    {
                        row.HasError = true;
                    }
                }
            }

            await using IDbContextTransaction dbTx = await _db.Database.BeginTransactionAsync(ct);

            // 1) Ensure accounts exist
            string[] codes = rows
                .Select(r => r.AccountCode)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            List<string> existing = await _db.Accounts
                .Where(a => a.CompanyId == companyId && codes.Contains(a.Code))
                .Select(a => a.Code)
                .ToListAsync(ct);

            string[] missing = codes
                .Except(existing, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (missing.Length > 0)
            {
                List<AccountTypePrefix> prefixRules = await _db.AccountTypePrefixes
                   .AsNoTracking()
                   .ToListAsync(ct);

                foreach (string code in missing)
                {
                    string trimmedCode = code.Trim();
                    string accountType = ResolveAccountType(trimmedCode, prefixRules);

                    _db.Accounts.Add(new Account
                    {
                        CompanyId = companyId,
                        Code = trimmedCode,
                        Name = trimmedCode,
                        Type = accountType
                    });
                }


                await _db.SaveChangesAsync(ct);
            }

            // 2) Allocate new TransactionNos
            int maxTxnNo = await _db.Transactions
                .Where(t => t.CompanyId == companyId)
                .Select(t => (int?)t.TransactionNo)
                .MaxAsync(ct) ?? 0;

            List<string> orderedDistinctKeys = rows
                .Select(r => Key(r.DateUtc, r.SourceTrxNo))
                .Distinct()
                .ToList();

            Dictionary<string, int> map = new Dictionary<string, int>(StringComparer.Ordinal);
            int next = maxTxnNo + 1;
            foreach (string k in orderedDistinctKeys)
                map[k] = next++;

            // 3) Insert headers + lines
            // Create headers (one per distinct key)
            List<Transaction> headers = new List<Transaction>(orderedDistinctKeys.Count);
            foreach (string k in orderedDistinctKeys)
            {
                // Parse date back out of key (first segment) - safe because we made it.
                string datePart = k.Split('|', 2)[0];
                DateTime dt = DateTime.ParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
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
            List<TransactionEntry> lines = new List<TransactionEntry>(rows.Count);
            foreach (CsvRow r in rows)
            {
                string k = Key(r.DateUtc, r.SourceTrxNo);
                lines.Add(new TransactionEntry
                {
                    CompanyId = companyId,
                    TransactionNo = map[k],
                    AccountCode = r.AccountCode,
                    LineDescription = string.IsNullOrWhiteSpace(r.Particular) ? null : r.Particular,
                    Debit = r.Debit,
                    Credit = r.Credit,
                    HasError = r.HasError
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

            return new TransactionImportResult(
                ImportedLines: rows.Count,
                ImportedLinesWithErrors: rows.Count(r => r.HasError),
                SkippedLines: parsed.SkippedLines);
        }

        private async Task<ParsedCsvRows> ReadCsvRowsAsync(IFormFile csvFile, CancellationToken ct)
        {
            List<CsvRow> result = new List<CsvRow>();
            List<SkippedImportLine> skipped = new List<SkippedImportLine>();
            bool firstNonEmptyLine = true;

            await using Stream stream = csvFile.OpenReadStream();
            using StreamReader reader = new StreamReader(stream);

            string? line;
            int lineNumber = 0;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                List<string> cols = SplitCsvLine(line);
                if (firstNonEmptyLine && IsHeaderRow(cols))
                {
                    firstNonEmptyLine = false;
                    continue;
                }

                firstNonEmptyLine = false;

                if (cols.Count < 6)
                {
                    skipped.Add(new SkippedImportLine(lineNumber, "Expected at least 6 columns.", line));
                    continue;
                }

                // fixed positions
                string dateStr = cols[0].Trim();
                string trxNo = cols[1].Trim();
                string ledger = NormalizeImportedAccountCode(cols[2]);
                string particular = cols[3].Trim();
                string drStr = cols[4].Trim();
                string crStr = cols[5].Trim();

                if (string.IsNullOrWhiteSpace(dateStr))
                {
                    skipped.Add(new SkippedImportLine(lineNumber, "Missing date.", line));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(trxNo))
                {
                    skipped.Add(new SkippedImportLine(lineNumber, "Missing transaction number.", line));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(ledger))
                {
                    skipped.Add(new SkippedImportLine(lineNumber, "Missing ledger account code.", line));
                    continue;
                }

                string[] dateFormats = new[] { "dd/MM/yy", "dd/MM/yyyy" };
                if (!DateTime.TryParseExact(
                    dateStr,
                    dateFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime dt))
                {
                    skipped.Add(new SkippedImportLine(lineNumber, "Invalid date.", line));
                    continue;
                }

                dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);

                if (!TryParseImportAmount(drStr, out decimal dr))
                {
                    skipped.Add(new SkippedImportLine(lineNumber, "Invalid debit amount.", line));
                    continue;
                }

                if (!TryParseImportAmount(crStr, out decimal cr))
                {
                    skipped.Add(new SkippedImportLine(lineNumber, "Invalid credit amount.", line));
                    continue;
                }

                if (dr < 0m)
                {
                    skipped.Add(new SkippedImportLine(lineNumber, "Negative debit amount.", line));
                    continue;
                }

                if (cr < 0m)
                {
                    skipped.Add(new SkippedImportLine(lineNumber, "Negative credit amount.", line));
                    continue;
                }

                bool hasError =
                    string.IsNullOrWhiteSpace(drStr) ||
                    string.IsNullOrWhiteSpace(crStr) ||
                    (dr == 0m && cr == 0m);

                result.Add(new CsvRow
                {
                    LineNumber = lineNumber,
                    DateUtc = dt,
                    SourceTrxNo = trxNo,
                    AccountCode = ledger,
                    Particular = particular,
                    Debit = dr,
                    Credit = cr,
                    HasError = hasError
                });
            }

            return new ParsedCsvRows(result, skipped);
        }

        private static List<string> SplitCsvLine(string line)
        {
            List<string> cols = new List<string>();
            StringBuilder sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];

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

        private static bool IsHeaderRow(List<string> cols)
        {
            if (cols.Count == 0)
                return false;

            string first = cols[0].Trim().Trim('"');
            return first.StartsWith("DATE", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseImportAmount(string value, out decimal amount)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                amount = 0m;
                return true;
            }

            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out amount);
        }

        private static string NormalizeImportedAccountCode(string? accountCode)
        {
            string code = (accountCode ?? "").Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(code))
                return "";

            int splitIndex = code.Length;

            while (splitIndex > 0 && char.IsDigit(code[splitIndex - 1]))
            {
                splitIndex--;
            }

            string prefix = code[..splitIndex];
            string numberPart = code[splitIndex..];

            if (string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(numberPart))
                return code;

            if (!int.TryParse(numberPart, out int number))
                return code;

            return $"{prefix}{number:000}";
        }

        private static string ResolveAccountType(string accountCode, List<AccountTypePrefix> prefixRules)
        {
            if (string.IsNullOrWhiteSpace(accountCode))
                return "Uncategorized";

            string code = accountCode.Trim().ToUpperInvariant();

            AccountTypePrefix? match = prefixRules
                .OrderByDescending(x => x.Prefix.Length)
                .FirstOrDefault(x => code.StartsWith(x.Prefix.ToUpperInvariant()));

            return match?.AccountType ?? "Uncategorized";
        }
    }
}
