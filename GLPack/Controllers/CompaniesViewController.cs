using GLPack.DAL;
using GLPack.Models;
using GLPack.Services;
using GLPack.ViewModels.Companies;
using GLPack.ViewModels.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GLPack.Controllers
{
    // This controller serves the *pages* for a single company (dashboard etc.)
    [Route("company")]
    public class CompanyPagesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IReportsService _reports;
        private readonly ITransactionImportService _import;
        private readonly IAppLogger _appLogger;
        private readonly ICompanyAccessService _companyAccess;

        public CompanyPagesController(
            ApplicationDbContext db,
            IReportsService reports,
            ITransactionImportService import,
            IAppLogger appLogger,
            ICompanyAccessService companyAccess)
        {
            _db = db;
            _reports = reports;
            _import = import;
            _appLogger = appLogger;
            _companyAccess = companyAccess;
        }

        // GET /company/{id}/dashboard
        [HttpGet("{id:int}/dashboard")]
        public async Task<IActionResult> Dashboard(int id, CancellationToken ct)
        {
            Company? company = await GetAccessibleCompanyAsync(id, ct);

            if (company == null)
            {
                return NotFound();
            }

            (List<TrialBalanceRow> tbRows, decimal tbDr, decimal tbCr) = await _reports.GetTrialBalanceAsync(company.Id, ct);

            IQueryable<TransactionEntry> errorQuery = _db.TransactionEntries
                .AsNoTracking()
                .Where(e => e.CompanyId == company.Id && e.HasError);

            int currentErrorCount = await errorQuery.CountAsync(ct);

            List<DashboardErrorRow> currentErrors = await errorQuery
                .OrderByDescending(e => e.Transaction.Date)
                .ThenByDescending(e => e.TransactionNo)
                .ThenBy(e => e.Id)
                .Take(10)
                .Select(e => new DashboardErrorRow
                {
                    TransactionNo = e.TransactionNo,
                    Date = e.Transaction.Date,
                    AccountCode = e.AccountCode,
                    AccountName = e.Account.Name,
                    Memo = e.LineDescription,
                    Debit = e.Debit,
                    Credit = e.Credit,
                    Issue = "Entry marked as error",
                })
                .ToListAsync(ct);

            List<DashboardRecentTransactionRow> recentTransactions = await _db.Transactions
                .AsNoTracking()
                .Where(t => t.CompanyId == company.Id)
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.TransactionNo)
                .Take(10)
                .Select(t => new DashboardRecentTransactionRow
                {
                    TransactionNo = t.TransactionNo,
                    Date = t.Date,
                    Description = t.Description,
                    TotalDebit = t.Items.Sum(i => i.Debit),
                    TotalCredit = t.Items.Sum(i => i.Credit),
                    HasErrors = t.Items.Any(i => i.HasError)
                })
                .ToListAsync(ct);

            DashboardViewModel vm = new DashboardViewModel
            {
                CompanyId = company.Id,
                CompanyName = company.Name,
                TrialBalanceRows = tbRows,
                TrialBalanceTotalDebit = tbDr,
                TrialBalanceTotalCredit = tbCr,
                CurrentErrorCount = currentErrorCount,
                CurrentErrors = currentErrors,
                RecentTransactions = recentTransactions
            };

            // Explicit path so we use Views/Dashboard/Dashboard.cshtml
            return View("~/Views/Dashboard/Dashboard.cshtml", vm);
        }

        [HttpGet("{id:int}/accounts")]
        public async Task<IActionResult> Accounts(int id, CancellationToken ct)
        {
            Company? company = await GetAccessibleCompanyAsync(id, ct);

            if (company == null) return NotFound();

            DashboardViewModel vm = new DashboardViewModel
            {
                CompanyId = company.Id,
                CompanyName = company.Name
            };

            return View("~/Views/Accounts/Accounts.cshtml", vm);
        }

        [HttpGet("{id:int}/transactions")]
        public async Task<IActionResult> Transactions(int id, CancellationToken ct)
        {
            Company? company = await GetAccessibleCompanyAsync(id, ct);

            if (company == null) return NotFound();

            DashboardViewModel vm = new DashboardViewModel
            {
                CompanyId = company.Id,
                CompanyName = company.Name
            };

            return View("~/Views/Transactions/Transactions.cshtml", vm);
        }

        [HttpGet("{id:int}/search")]
        public async Task<IActionResult> Search(int id, CancellationToken ct)
        {
            Company? company = await GetAccessibleCompanyAsync(id, ct);
            if (company == null) return NotFound();
            DashboardViewModel vm = new DashboardViewModel
            {
                CompanyId = company.Id,
                CompanyName = company.Name
            };
            return View("~/Views/Ledger/Search.cshtml", vm);
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpGet("{id:int}/settings")]
        public async Task<IActionResult> Settings(int id, CancellationToken ct)
        {
            Company? company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            if (company == null) return NotFound();

            CompanySettingsViewModel vm = new CompanySettingsViewModel
            {
                CompanyId = company.Id,
                CompanyName = company.Name,
                Name = company.Name,
                Currency = company.Currency,
                FinancialYearEnd = company.FinancialYearEnd,
                AccessUsers = await GetAssignedCompanyAccessUsersAsync(company.Id, ct),
                AvailableAccessUsers = await GetAvailableCompanyAccessUsersAsync(company.Id, ct)
            };

            return View("~/Views/CompanySettings/Settings.cshtml", vm);
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpPost("{id:int}/settings")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSettings(
            int id,
            CompanySettingsViewModel request,
            CancellationToken ct)
        {
            Company? company = await _db.Companies
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            if (company == null) return NotFound();

            request.Name = (request.Name ?? "").Trim();
            request.Currency = (request.Currency ?? "").Trim().ToUpperInvariant();
            request.FinancialYearEnd = (request.FinancialYearEnd ?? "").Trim();
            request.CompanyId = company.Id;
            request.CompanyName = company.Name;

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                ModelState.AddModelError(nameof(request.Name), "Company name is required.");
            }

            if (request.Currency.Length != 3 || !request.Currency.All(char.IsLetter))
            {
                ModelState.AddModelError(nameof(request.Currency), "Currency must be a 3-letter code, for example SGD.");
            }

            if (!TryNormalizeFinancialYearEnd(request.FinancialYearEnd, out string normalizedFinancialYearEnd))
            {
                ModelState.AddModelError(nameof(request.FinancialYearEnd), "Financial year end must be a valid MM-DD date, for example 12-31.");
            }

            if (!ModelState.IsValid)
            {
                request.AccessUsers = await GetAssignedCompanyAccessUsersAsync(company.Id, ct);
                request.AvailableAccessUsers = await GetAvailableCompanyAccessUsersAsync(company.Id, ct);
                return View("~/Views/CompanySettings/Settings.cshtml", request);
            }

            company.Name = request.Name;
            company.Currency = request.Currency;
            company.FinancialYearEnd = normalizedFinancialYearEnd;

            await _db.SaveChangesAsync(ct);

            await _appLogger.LogAsync(
                eventType: "AUDIT",
                level: "INFO",
                logCode: "COMPANY_SETTINGS_UPDATE_OK",
                logMessage: $"Updated company {id} settings.",
                companyId: id,
                sourceFile: nameof(CompanyPagesController),
                sourceFunction: nameof(UpdateSettings),
                ct: ct);

            TempData["CompanySettingsSuccess"] = "Company settings saved.";
            return Redirect($"/company/{id}/settings");
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpPost("{id:int}/settings/access/add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAccess(
            int id,
            CompanyAccessAddRequest request,
            CancellationToken ct)
        {
            bool companyExists = await _db.Companies
                .AsNoTracking()
                .AnyAsync(c => c.Id == id, ct);

            if (!companyExists) return NotFound();

            bool isAssignableClientUser = await _db.AppUsers
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.UserId && !x.IsAdmin, ct);

            if (!isAssignableClientUser)
            {
                TempData["CompanySettingsError"] = "Select a valid client user.";
                return Redirect($"/company/{id}/settings");
            }

            bool alreadyHasAccess = await _db.CompanyUserAccesses
                .AsNoTracking()
                .AnyAsync(x => x.CompanyId == id && x.UserId == request.UserId, ct);

            if (!alreadyHasAccess)
            {
                _db.CompanyUserAccesses.Add(new CompanyUserAccess
                {
                    CompanyId = id,
                    UserId = request.UserId,
                    CreatedAtUtc = DateTime.UtcNow
                });

                await _db.SaveChangesAsync(ct);
            }

            await LogCompanyAccessCountAsync(id, nameof(AddAccess), ct);

            TempData["CompanySettingsSuccess"] = "User access added.";
            return Redirect($"/company/{id}/settings");
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpPost("{id:int}/settings/access/remove")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAccess(
            int id,
            CompanyAccessRemoveRequest request,
            CancellationToken ct)
        {
            CompanyUserAccess? existing = await _db.CompanyUserAccesses
                .FirstOrDefaultAsync(x => x.CompanyId == id && x.UserId == request.UserId, ct);

            if (existing != null)
            {
                _db.CompanyUserAccesses.Remove(existing);
                await _db.SaveChangesAsync(ct);
            }

            await LogCompanyAccessCountAsync(id, nameof(RemoveAccess), ct);

            TempData["CompanySettingsSuccess"] = "User access removed.";
            return Redirect($"/company/{id}/settings");
        }

        private async Task LogCompanyAccessCountAsync(int companyId, string sourceFunction, CancellationToken ct)
        {
            int assignedUserCount = await _db.CompanyUserAccesses
                .AsNoTracking()
                .CountAsync(x => x.CompanyId == companyId, ct);

            await _appLogger.LogAsync(
                eventType: "AUDIT",
                level: "INFO",
                logCode: "COMPANY_ACCESS_UPDATE_OK",
                logMessage: $"Updated company {companyId} access. Assigned client users: {assignedUserCount}.",
                companyId: companyId,
                sourceFile: nameof(CompanyPagesController),
                sourceFunction: sourceFunction,
                ct: ct);
        }

        [HttpPost("{id:int}/import")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(int id, IFormFile importFile, CancellationToken ct)
        {
            if (!await _companyAccess.CanAccessCompanyAsync(User, id, ct))
            {
                return NotFound();
            }

            try
            {
                TransactionImportResult result = await _import.ImportAsync(id, importFile, ct);
                int skippedCount = result.SkippedLines.Count;
                TempData["ImportSuccess"] =
                    $"Imported {result.ImportedLines} lines. Skipped {skippedCount} lines.";

                if (skippedCount > 0)
                {
                    TempData["ImportSkippedLines"] = JsonSerializer.Serialize(result.SkippedLines);
                }
            }
            catch (Exception ex)
            {
                TempData["ImportError"] = ex.Message;
            }

            return Redirect($"/company/{id}/dashboard");
        }

        private async Task<Company?> GetAccessibleCompanyAsync(int companyId, CancellationToken ct)
        {
            IQueryable<Company> query = _companyAccess.ApplyCompanyAccess(_db.Companies.AsNoTracking(), User);
            return await query.FirstOrDefaultAsync(c => c.Id == companyId, ct);
        }

        private async Task<List<CompanyAccessUserRow>> GetAssignedCompanyAccessUsersAsync(int companyId, CancellationToken ct)
        {
            return await _db.CompanyUserAccesses
                .AsNoTracking()
                .Where(x => x.CompanyId == companyId && !x.User.IsAdmin)
                .OrderByDescending(x => x.User.IsActive)
                .ThenBy(x => x.User.Email)
                .Select(x => new CompanyAccessUserRow
                {
                    UserId = x.User.Id,
                    Email = x.User.Email,
                    IsActive = x.User.IsActive,
                    LastLoginAtUtc = x.User.LastLoginAtUtc
                })
                .ToListAsync(ct);
        }

        private async Task<List<CompanyAccessUserOption>> GetAvailableCompanyAccessUsersAsync(int companyId, CancellationToken ct)
        {
            HashSet<int> assignedUserIds = await _db.CompanyUserAccesses
                .AsNoTracking()
                .Where(x => x.CompanyId == companyId)
                .Select(x => x.UserId)
                .ToHashSetAsync(ct);

            return await _db.AppUsers
                .AsNoTracking()
                .Where(x => !x.IsAdmin && !assignedUserIds.Contains(x.Id))
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => x.Email)
                .Select(x => new CompanyAccessUserOption
                {
                    UserId = x.Id,
                    Email = x.Email,
                    IsActive = x.IsActive
                })
                .ToListAsync(ct);
        }

        private static bool TryNormalizeFinancialYearEnd(string value, out string normalized)
        {
            normalized = "";
            string[] parts = value.Split('-', StringSplitOptions.TrimEntries);

            if (parts.Length != 2 ||
                !int.TryParse(parts[0], out int month) ||
                !int.TryParse(parts[1], out int day) ||
                month < 1 ||
                month > 12)
            {
                return false;
            }

            int maxDay = DateTime.DaysInMonth(2000, month);
            if (day < 1 || day > maxDay)
            {
                return false;
            }

            normalized = $"{month:00}-{day:00}";
            return true;
        }
    }
}
