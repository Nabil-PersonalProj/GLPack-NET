using GLPack.DAL;
using GLPack.Models;
using GLPack.Services;
using GLPack.ViewModels.Companies;
using GLPack.ViewModels.Reports;
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

        public CompanyPagesController(ApplicationDbContext db, IReportsService reports, ITransactionImportService import)
        {
            _db = db;
            _reports = reports;
            _import = import;
        }

        // GET /company/{id}/dashboard
        [HttpGet("{id:int}/dashboard")]
        public async Task<IActionResult> Dashboard(int id, CancellationToken ct)
        {
            Company? company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            if (company == null)
            {
                return NotFound();
            }

            (List<TrialBalanceRow> tbRows, decimal tbDr, decimal tbCr) = await _reports.GetTrialBalanceAsync(company.Id, ct);

            IQueryable<TransactionEntry> errorQuery = _db.TransactionEntries
                .AsNoTracking()
                .Where(e => e.CompanyId == company.Id && (e.HasError || (e.Debit == 0m && e.Credit == 0m)));

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
                    Issue = e.Debit == 0m && e.Credit == 0m
                        ? "Debit and credit are both zero"
                        : "Entry marked as error"
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
                    HasErrors = t.Items.Any(i => i.HasError || (i.Debit == 0m && i.Credit == 0m))
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
            Company? company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, ct);

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
            Company? company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, ct);

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
            Company? company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, ct);
            if (company == null) return NotFound();
            DashboardViewModel vm = new DashboardViewModel
            {
                CompanyId = company.Id,
                CompanyName = company.Name
            };
            return View("~/Views/Ledger/Search.cshtml", vm);
        }

        [HttpPost("{id:int}/import")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportCsv(int id, IFormFile csvFile, CancellationToken ct)
        {
            try
            {
                TransactionImportResult result = await _import.ImportCsvAsync(id, csvFile, ct);
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
    }
}
