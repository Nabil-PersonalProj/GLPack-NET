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

            List<CsvRow> rows = await ReadCsvRowsAsync(csvFile, ct);
            if (rows.Count == 0)
                return 0;

            // Ensure company exists (cheap guard)
            bool companyExists = await _db.Companies.AnyAsync(c => c.Id == companyId, ct);
            if (!companyExists)
                throw new KeyNotFoundException("Company not found.");

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

            // Group key = Date + SourceTrxNo (as per requirement)
            // Use yyyy-MM-dd to avoid string-format differences.
            static string Key(DateTime utcDate, string sourceNo)
                => $"{utcDate:yyyy-MM-dd}|{sourceNo}";

            List<string> orderedDistinctKeys = rows
                .Select(r => Key(r.DateUtc, r.SourceTrxNo))
                .Distinct()
                .ToList();

            Dictionary<string, int> map = new Dictionary<string, int>(StringComparer.Ordinal);
            int next = maxTxnNo + 1;
            foreach (string k in orderedDistinctKeys)
                map[k] = next++;

            // 3) Insert headers + lines
            await using IDbContextTransaction dbTx = await _db.Database.BeginTransactionAsync(ct);

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
            List<CsvRow> result = new List<CsvRow>();

            await using Stream stream = csvFile.OpenReadStream();
            using StreamReader reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                List<string> cols = SplitCsvLine(line);
                if (cols.Count < 6)
                {
                    continue;
                }
                // fixed positions
                string dateStr = cols[0].Trim();
                string trxNo = cols[1].Trim();
                string ledger = NormalizeImportedAccountCode(cols[2]);
                string particular = cols[3].Trim();
                string drStr = cols[4].Trim();
                string crStr = cols[5].Trim();

                string[] dateFormats = new[] { "dd/MM/yy", "dd/MM/yyyy" };
                if (!DateTime.TryParseExact(
                    dateStr,
                    dateFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime dt))
                {
                    continue;
                }

                dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);

                decimal dr = string.IsNullOrWhiteSpace(drStr) ? 0m : decimal.Parse(drStr, NumberStyles.Any, CultureInfo.InvariantCulture);
                decimal cr = string.IsNullOrWhiteSpace(crStr) ? 0m : decimal.Parse(crStr, NumberStyles.Any, CultureInfo.InvariantCulture);

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
