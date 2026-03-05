using AspNetCoreGeneratedDocument;
using GLPack.DAL;
using GLPack.Services;
using GLPack.ViewModels.Companies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            var company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            if (company == null)
            {
                return NotFound();
            }

            var (tbRows, tbDr, tbCr) = await _reports.GetTrialBalanceAsync(company.Id, ct);

            var vm = new DashboardViewModel
            {
                CompanyId = company.Id,
                CompanyName = company.Name,
                TrialBalanceRows = tbRows,
                TrialBalanceTotalDebit = tbDr,
                TrialBalanceTotalCredit = tbCr
            };

            // Explicit path so we use Views/Dashboard/Dashboard.cshtml
            return View("~/Views/Dashboard/Dashboard.cshtml", vm);
        }

        [HttpGet("{id:int}/accounts")]
        public async Task<IActionResult> Accounts(int id, CancellationToken ct)
        {
            var company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            if (company == null) return NotFound();

            var vm = new DashboardViewModel
            {
                CompanyId = company.Id,
                CompanyName = company.Name
            };

            return View("~/Views/Accounts/Accounts.cshtml", vm);
        }

        [HttpGet("{id:int}/transactions")]
        public async Task<IActionResult> Transactions(int id, CancellationToken ct)
        {
            var company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            if (company == null) return NotFound();

            var vm = new DashboardViewModel
            {
                CompanyId = company.Id,
                CompanyName = company.Name
            };

            return View("~/Views/Transactions/Transactions.cshtml", vm);
        }

        [HttpGet("{id:int}/search")]
        public async Task<IActionResult> Search(int id, CancellationToken ct)
        {
            var company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, ct);
            if (company == null) return NotFound();
            var vm = new DashboardViewModel
            {
                CompanyId = company.Id,
                CompanyName = company.Name
            };
            return View("~/Views/Ledger/Search.cshtml", vm);
        }

        [HttpPost("{id:int}/import")]
        public async Task<IActionResult> ImportCsv(int id, IFormFile csvFile, CancellationToken ct)
        {
            try
            {
                var count = await _import.ImportCsvAsync(id, csvFile, ct);
                TempData["ImportSuccess"] = $"Imported {count} lines.";
            }
            catch (Exception ex)
            {
                TempData["ImportError"] = ex.Message;
            }

            return Redirect($"/company/{id}/dashboard");
        }
    }
}
