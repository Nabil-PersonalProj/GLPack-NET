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

        private sealed class ImportRow
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

        private sealed record ParsedImportRows(
            List<ImportRow> Rows,
            List<SkippedImportLine> SkippedLines);

        private sealed record DbfField(
            string Name,
            char Type,
            int Offset,
            int Length,
            int DecimalCount);

        public async Task<TransactionImportResult> ImportAsync(int companyId, IFormFile importFile, CancellationToken ct)
        {
            if (importFile == null || importFile.Length == 0)
            {
                await _logger.LogAsync(
                    eventType: "ERROR",
                    level: "INFO",
                    logCode: "IMPORT_ERROR",
                    logMessage: "No import file",
                    companyId: companyId,
                    sourceFile: nameof(TransactionImportService),
                    sourceFunction: nameof(ImportAsync),
                    ct: ct);

                throw new ArgumentException("No file uploaded.");
            }

            string extension = Path.GetExtension(importFile.FileName).ToLowerInvariant();

            return extension switch
            {
                ".csv" => await ImportCsvAsync(companyId, importFile, ct),
                ".dbf" => await ImportDbfAsync(companyId, importFile, ct),
                _ => throw new ArgumentException("Unsupported import file. Please upload a CSV or DBF file.")
            };
        }

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

            ParsedImportRows parsed = await ReadCsvRowsAsync(csvFile, ct);
            return await SaveImportedRowsAsync(
                companyId,
                parsed,
                importType: "CSV",
                sourceFunction: nameof(ImportCsvAsync),
                ct);
        }

        public async Task<TransactionImportResult> ImportDbfAsync(int companyId, IFormFile dbfFile, CancellationToken ct)
        {
            if (dbfFile == null || dbfFile.Length == 0)
            {
                await _logger.LogAsync(
                    eventType: "ERROR",
                    level: "INFO",
                    logCode: "DBF_IMPORT_ERROR",
                    logMessage: "No DBF file",
                    companyId: companyId,
                    sourceFile: nameof(TransactionImportService),
                    sourceFunction: nameof(ImportDbfAsync),
                    ct: ct);

                throw new ArgumentException("No DBF file uploaded.");
            }

            ParsedImportRows parsed = await ReadDbfRowsAsync(dbfFile, ct);
            return await SaveImportedRowsAsync(
                companyId,
                parsed,
                importType: "DBF",
                sourceFunction: nameof(ImportDbfAsync),
                ct);
        }

        private async Task<TransactionImportResult> SaveImportedRowsAsync(
            int companyId,
            ParsedImportRows parsed,
            string importType,
            string sourceFunction,
            CancellationToken ct)
        {
            List<ImportRow> rows = parsed.Rows;

            if (rows.Count == 0)
                return new TransactionImportResult(0, 0, parsed.SkippedLines);

            // Ensure company exists
            bool companyExists = await _db.Companies.AnyAsync(c => c.Id == companyId, ct);
            if (!companyExists)
                throw new KeyNotFoundException("Company not found.");

            // Group key = Date + SourceTrxNo
            // Use yyyy-MM-dd to avoid string-format differences.
            static string Key(DateTime utcDate, string sourceNo)
                => $"{utcDate:yyyy-MM-dd}|{sourceNo}";

            foreach (IGrouping<string, ImportRow> group in rows.GroupBy(r => Key(r.DateUtc, r.SourceTrxNo)))
            {
                decimal totalDr = group.Sum(r => r.Debit);
                decimal totalCr = group.Sum(r => r.Credit);

                if (Math.Round(totalDr - totalCr, 2) != 0m)
                {
                    foreach (ImportRow row in group)
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
            // Create headers, one per distinct key
            List<Transaction> headers = new List<Transaction>(orderedDistinctKeys.Count);

            foreach (string k in orderedDistinctKeys)
            {
                // Parse date back out of key, safe because we created the key.
                string datePart = k.Split('|', 2)[0];

                DateTime dt = DateTime.ParseExact(
                    datePart,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal);

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

            foreach (ImportRow r in rows)
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
                logCode: $"{importType}_IMPORT_OK",
                logMessage: $"Imported {rows.Count} lines across {orderedDistinctKeys.Count} transactions",
                companyId: companyId,
                sourceFile: nameof(TransactionImportService),
                sourceFunction: sourceFunction,
                ct: ct);

            return new TransactionImportResult(
                ImportedLines: rows.Count,
                ImportedLinesWithErrors: rows.Count(r => r.HasError),
                SkippedLines: parsed.SkippedLines);
        }

        private async Task<ParsedImportRows> ReadCsvRowsAsync(IFormFile csvFile, CancellationToken ct)
        {
            List<ImportRow> result = new List<ImportRow>();
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
                    continue;

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

                // Fixed positions:
                // DATE, TRXNO, LEDGERNAME, PARTICULAR, DRAMOUNT, CRAMOUNT
                AddParsedImportRow(
                    result,
                    skipped,
                    lineNumber,
                    dateStr: cols[0].Trim(),
                    trxNo: cols[1].Trim(),
                    ledgerRaw: cols[2],
                    particular: cols[3].Trim(),
                    drStr: cols[4].Trim(),
                    crStr: cols[5].Trim(),
                    originalText: line);
            }

            return new ParsedImportRows(result, skipped);
        }

        private async Task<ParsedImportRows> ReadDbfRowsAsync(IFormFile dbfFile, CancellationToken ct)
        {
            List<ImportRow> result = new List<ImportRow>();
            List<SkippedImportLine> skipped = new List<SkippedImportLine>();

            await using Stream input = dbfFile.OpenReadStream();
            using MemoryStream ms = new MemoryStream();
            await input.CopyToAsync(ms, ct);

            byte[] bytes = ms.ToArray();

            if (bytes.Length < 32)
                throw new ArgumentException("Invalid DBF file. Header is too short.");

            int recordCount = BitConverter.ToInt32(bytes, 4);
            int headerLength = BitConverter.ToUInt16(bytes, 8);
            int recordLength = BitConverter.ToUInt16(bytes, 10);

            if (recordCount < 0)
                throw new ArgumentException("Invalid DBF file. Record count could not be read.");

            if (headerLength <= 32 || recordLength <= 1 || headerLength > bytes.Length)
                throw new ArgumentException("Invalid DBF file. Header information could not be read.");

            List<DbfField> fields = ReadDbfFields(bytes, headerLength);

            Dictionary<string, DbfField> fieldMap = fields
                .ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

            string[] requiredFields =
            {
                "DATE",
                "TRXNO",
                "LEDGERNAME",
                "PARTICULAR",
                "DRAMOUNT",
                "CRAMOUNT"
            };

            string[] missingFields = requiredFields
                .Where(name => !fieldMap.ContainsKey(name))
                .ToArray();

            if (missingFields.Length > 0)
            {
                throw new ArgumentException(
                    $"DBF file is missing required field(s): {string.Join(", ", missingFields)}.");
            }

            Encoding textEncoding = Encoding.Latin1;

            int maxRecordsByLength = Math.Max(0, (bytes.Length - headerLength) / recordLength);
            int recordsToRead = Math.Min(recordCount, maxRecordsByLength);

            for (int i = 0; i < recordsToRead; i++)
            {
                int recordStart = headerLength + (i * recordLength);
                int lineNumber = i + 1;

                if (recordStart < 0 || recordStart >= bytes.Length)
                    break;

                // Deleted DBF records start with '*'.
                // Active records usually start with a space.
                if (bytes[recordStart] == (byte)'*')
                    continue;

                string dateStr = GetDbfValue(bytes, recordStart, fieldMap["DATE"], textEncoding);
                string trxNo = GetDbfValue(bytes, recordStart, fieldMap["TRXNO"], textEncoding);
                string ledger = GetDbfValue(bytes, recordStart, fieldMap["LEDGERNAME"], textEncoding);
                string particular = GetDbfValue(bytes, recordStart, fieldMap["PARTICULAR"], textEncoding);
                string drStr = GetDbfValue(bytes, recordStart, fieldMap["DRAMOUNT"], textEncoding);
                string crStr = GetDbfValue(bytes, recordStart, fieldMap["CRAMOUNT"], textEncoding);

                string rowText =
                    $"DATE={dateStr}; TRXNO={trxNo}; LEDGERNAME={ledger}; PARTICULAR={particular}; DRAMOUNT={drStr}; CRAMOUNT={crStr}";

                AddParsedImportRow(
                    result,
                    skipped,
                    lineNumber,
                    dateStr,
                    trxNo,
                    ledger,
                    particular,
                    drStr,
                    crStr,
                    rowText);
            }

            return new ParsedImportRows(result, skipped);
        }

        private static List<DbfField> ReadDbfFields(byte[] bytes, int headerLength)
        {
            List<DbfField> fields = new List<DbfField>();
            Encoding ascii = Encoding.ASCII;

            int offset = 1; // First byte of each record is the deleted flag.

            for (int fieldStart = 32; fieldStart + 32 <= headerLength; fieldStart += 32)
            {
                if (bytes[fieldStart] == 0x0D)
                    break;

                string name = ascii
                    .GetString(bytes, fieldStart, 11)
                    .TrimEnd('\0', ' ');

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                char type = (char)bytes[fieldStart + 11];
                int length = bytes[fieldStart + 16];
                int decimalCount = bytes[fieldStart + 17];

                fields.Add(new DbfField(
                    Name: name,
                    Type: type,
                    Offset: offset,
                    Length: length,
                    DecimalCount: decimalCount));

                offset += length;
            }

            return fields;
        }

        private static string GetDbfValue(
            byte[] bytes,
            int recordStart,
            DbfField field,
            Encoding encoding)
        {
            int start = recordStart + field.Offset;

            if (start < 0 || start >= bytes.Length)
                return "";

            int length = Math.Min(field.Length, bytes.Length - start);

            string raw = field.Type == 'D'
                ? Encoding.ASCII.GetString(bytes, start, length)
                : encoding.GetString(bytes, start, length);

            return raw.Trim('\0', ' ');
        }

        private static void AddParsedImportRow(
            List<ImportRow> result,
            List<SkippedImportLine> skipped,
            int lineNumber,
            string dateStr,
            string trxNo,
            string ledgerRaw,
            string particular,
            string drStr,
            string crStr,
            string originalText)
        {
            string ledger = NormalizeImportedAccountCode(ledgerRaw);

            if (string.IsNullOrWhiteSpace(dateStr))
            {
                skipped.Add(new SkippedImportLine(lineNumber, "Missing date.", originalText));
                return;
            }

            if (string.IsNullOrWhiteSpace(trxNo))
            {
                skipped.Add(new SkippedImportLine(lineNumber, "Missing transaction number.", originalText));
                return;
            }

            if (string.IsNullOrWhiteSpace(ledger))
            {
                skipped.Add(new SkippedImportLine(lineNumber, "Missing ledger account code.", originalText));
                return;
            }

            if (!TryParseImportDate(dateStr, out DateTime dt))
            {
                skipped.Add(new SkippedImportLine(lineNumber, "Invalid date.", originalText));
                return;
            }

            dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);

            if (!TryParseImportAmount(drStr, out decimal dr))
            {
                skipped.Add(new SkippedImportLine(lineNumber, "Invalid debit amount.", originalText));
                return;
            }

            if (!TryParseImportAmount(crStr, out decimal cr))
            {
                skipped.Add(new SkippedImportLine(lineNumber, "Invalid credit amount.", originalText));
                return;
            }

            if (dr < 0m)
            {
                skipped.Add(new SkippedImportLine(lineNumber, "Negative debit amount.", originalText));
                return;
            }

            if (cr < 0m)
            {
                skipped.Add(new SkippedImportLine(lineNumber, "Negative credit amount.", originalText));
                return;
            }

            bool hasError = false;

            result.Add(new ImportRow
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

        private static bool TryParseImportDate(string value, out DateTime date)
        {
            string[] dateFormats =
            {
                "dd/MM/yy",
                "dd/MM/yyyy",
                "yyyyMMdd"
            };

            return DateTime.TryParseExact(
                value.Trim(),
                dateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date);
        }

        private static bool TryParseImportAmount(string value, out decimal amount)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                amount = 0m;
                return true;
            }

            return decimal.TryParse(
                value.Trim(),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out amount);
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