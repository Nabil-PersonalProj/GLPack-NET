using GLPack.Contracts;
using GLPack.DAL;
using GLPack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Text;

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

        private sealed class AccountImportRow
        {
            public int LineNumber { get; init; }
            public required string AccountCode { get; init; }
            public required string Name { get; init; }
            public required string OriginalText { get; init; }
        }

        private sealed record ParsedAccountImportRows(
            List<AccountImportRow> Rows,
            List<SkippedAccountImportLine> SkippedLines);

        private sealed record DbfField(
            string Name,
            char Type,
            int Offset,
            int Length,
            int DecimalCount);

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

        public async Task<PagedResult<AccountDto>> ListAsync(int companyId, string? q, string? accountType, int page, int pageSize, CancellationToken ct)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 10 : pageSize;

            IQueryable<Account> query = _db.Accounts.AsNoTracking().Where(a => a.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(a =>
                    EF.Functions.ILike(a.Code, $"%{q}%") ||
                    EF.Functions.ILike(a.Name, $"%{q}%"));
            }

            if (!string.IsNullOrWhiteSpace(accountType))
            {
                accountType = accountType.Trim();
                query = query.Where(a => a.Type == accountType);
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

            bool linkedDepreciationAccountCreated = false;
            string? linkedDepreciationAccountCode = null;

            if (prefix == "FA")
            {
                string numberPart = nextCode[prefix.Length..];
                string pdCode = $"PD{numberPart}";
                linkedDepreciationAccountCode = pdCode;

                bool pdExists = await _db.Accounts.AnyAsync(
                    x => x.CompanyId == dto.CompanyId && x.Code == pdCode,
                    ct);

                if (!pdExists)
                {
                    AccountTypePrefix? pdPrefixRule = await _db.AccountTypePrefixes
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Prefix == "PD", ct);

                    if (pdPrefixRule is null)
                        throw new InvalidOperationException("PD prefix rule was not found.");

                    Account entityPD = new Account
                    {
                        CompanyId = dto.CompanyId,
                        Code = pdCode,
                        Name = $"Acc Dep - {name} - {nextCode}",
                        Type = pdPrefixRule.AccountType
                    };

                    _db.Accounts.Add(entityPD);
                    linkedDepreciationAccountCreated = true;
                }
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
                Type = entity.Type,
                LinkedDepreciationAccountCreated = linkedDepreciationAccountCreated,
                LinkedDepreciationAccountCode = linkedDepreciationAccountCode
            };
        }

        public async Task<AccountImportResult> ImportAsync(int companyId, IFormFile importFile, CancellationToken ct)
        {
            if (importFile == null || importFile.Length == 0)
                throw new ArgumentException("No file uploaded.");

            string extension = Path.GetExtension(importFile.FileName).ToLowerInvariant();

            ParsedAccountImportRows parsed = extension switch
            {
                ".csv" => await ReadCsvRowsAsync(importFile, ct),
                ".dbf" => await ReadDbfRowsAsync(importFile, ct),
                _ => throw new ArgumentException("Unsupported account import file. Please upload a CSV or DBF file.")
            };

            if (parsed.Rows.Count == 0)
                return new AccountImportResult(0, parsed.SkippedLines.Count, parsed.SkippedLines);

            bool companyExists = await _db.Companies.AnyAsync(c => c.Id == companyId, ct);
            if (!companyExists)
                throw new KeyNotFoundException("Company not found.");

            List<AccountTypePrefix> prefixRules = await _db.AccountTypePrefixes
                .AsNoTracking()
                .ToListAsync(ct);

            List<string> existingCodes = await _db.Accounts
                .AsNoTracking()
                .Where(a => a.CompanyId == companyId)
                .Select(a => a.Code)
                .ToListAsync(ct);

            HashSet<string> existingCodeSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);
            HashSet<string> importedCodeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<Account> accountsToAdd = new List<Account>();
            List<SkippedAccountImportLine> skipped = new List<SkippedAccountImportLine>(parsed.SkippedLines);

            foreach (AccountImportRow row in parsed.Rows)
            {
                if (existingCodeSet.Contains(row.AccountCode))
                {
                    skipped.Add(new SkippedAccountImportLine(
                        row.LineNumber,
                        row.AccountCode,
                        "Account already exists.",
                        row.OriginalText));
                    continue;
                }

                if (!importedCodeSet.Add(row.AccountCode))
                {
                    skipped.Add(new SkippedAccountImportLine(
                        row.LineNumber,
                        row.AccountCode,
                        "Duplicate account in import file.",
                        row.OriginalText));
                    continue;
                }

                accountsToAdd.Add(new Account
                {
                    CompanyId = companyId,
                    Code = row.AccountCode,
                    Name = row.Name,
                    Type = ResolveAccountType(row.AccountCode, prefixRules)
                });
            }

            if (accountsToAdd.Count > 0)
            {
                await using IDbContextTransaction dbTx = await _db.Database.BeginTransactionAsync(ct);
                _db.Accounts.AddRange(accountsToAdd);
                await _db.SaveChangesAsync(ct);
                await dbTx.CommitAsync(ct);
            }

            await _appLogger.LogAsync(
                eventType: "AUDIT",
                level: "INFO",
                logCode: "ACCOUNTS_IMPORT_OK",
                logMessage: $"Imported {accountsToAdd.Count} accounts. Skipped {skipped.Count} accounts.",
                companyId: companyId,
                sourceFile: nameof(AccountsService),
                sourceFunction: nameof(ImportAsync),
                ct: ct);

            return new AccountImportResult(
                ImportedAccounts: accountsToAdd.Count,
                SkippedAccounts: skipped.Count,
                SkippedLines: skipped);
        }

        private async Task<ParsedAccountImportRows> ReadCsvRowsAsync(IFormFile csvFile, CancellationToken ct)
        {
            List<AccountImportRow> rows = new List<AccountImportRow>();
            List<SkippedAccountImportLine> skipped = new List<SkippedAccountImportLine>();
            bool firstNonEmptyLine = true;
            int codeIndex = 0;
            int nameIndex = 1;

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

                if (firstNonEmptyLine && TryReadAccountHeader(cols, out int headerCodeIndex, out int headerNameIndex))
                {
                    codeIndex = headerCodeIndex;
                    nameIndex = headerNameIndex;
                    firstNonEmptyLine = false;
                    continue;
                }

                firstNonEmptyLine = false;

                int requiredColumnCount = Math.Max(codeIndex, nameIndex) + 1;
                if (cols.Count < requiredColumnCount)
                {
                    skipped.Add(new SkippedAccountImportLine(lineNumber, "", "Expected account code and name columns.", line));
                    continue;
                }

                AddParsedAccountRow(
                    rows,
                    skipped,
                    lineNumber,
                    codeRaw: cols[codeIndex],
                    nameRaw: cols[nameIndex],
                    originalText: line);
            }

            return new ParsedAccountImportRows(rows, skipped);
        }

        private async Task<ParsedAccountImportRows> ReadDbfRowsAsync(IFormFile dbfFile, CancellationToken ct)
        {
            List<AccountImportRow> rows = new List<AccountImportRow>();
            List<SkippedAccountImportLine> skipped = new List<SkippedAccountImportLine>();

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

            string[] requiredFields = { "CODE", "ACCNAME" };

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

                if (bytes[recordStart] == (byte)'*')
                    continue;

                string code = GetDbfValue(bytes, recordStart, fieldMap["CODE"], textEncoding);
                string name = GetDbfValue(bytes, recordStart, fieldMap["ACCNAME"], textEncoding);
                string rowText = $"CODE={code}; ACCNAME={name}";

                AddParsedAccountRow(
                    rows,
                    skipped,
                    lineNumber,
                    codeRaw: code,
                    nameRaw: name,
                    originalText: rowText);
            }

            return new ParsedAccountImportRows(rows, skipped);
        }

        private static void AddParsedAccountRow(
            List<AccountImportRow> rows,
            List<SkippedAccountImportLine> skipped,
            int lineNumber,
            string codeRaw,
            string nameRaw,
            string originalText)
        {
            string code = NormalizeImportedAccountCode(codeRaw);
            string name = (nameRaw ?? "").Trim();

            if (string.IsNullOrWhiteSpace(code))
            {
                skipped.Add(new SkippedAccountImportLine(lineNumber, "", "Missing account code.", originalText));
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                skipped.Add(new SkippedAccountImportLine(lineNumber, code, "Missing account name.", originalText));
                return;
            }

            if (code.Length > 50)
            {
                skipped.Add(new SkippedAccountImportLine(lineNumber, code, "Account code is longer than 50 characters.", originalText));
                return;
            }

            rows.Add(new AccountImportRow
            {
                LineNumber = lineNumber,
                AccountCode = code,
                Name = name.Length <= 255 ? name : name[..255],
                OriginalText = originalText
            });
        }

        private static List<DbfField> ReadDbfFields(byte[] bytes, int headerLength)
        {
            List<DbfField> fields = new List<DbfField>();
            Encoding ascii = Encoding.ASCII;

            int offset = 1;

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

        private static bool TryReadAccountHeader(List<string> cols, out int codeIndex, out int nameIndex)
        {
            codeIndex = -1;
            nameIndex = -1;

            for (int i = 0; i < cols.Count; i++)
            {
                string header = NormalizeHeader(cols[i]);

                if (header is "CODE" or "ACCOUNTCODE" or "ACCCODE")
                    codeIndex = i;

                if (header is "ACCNAME" or "ACCOUNTNAME" or "NAME")
                    nameIndex = i;
            }

            return codeIndex >= 0 && nameIndex >= 0;
        }

        private static string NormalizeHeader(string value)
        {
            StringBuilder sb = new StringBuilder();

            foreach (char ch in value.Trim().Trim('"'))
            {
                if (char.IsLetterOrDigit(ch))
                    sb.Append(char.ToUpperInvariant(ch));
            }

            return sb.ToString();
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
