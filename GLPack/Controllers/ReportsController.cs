using GLPack.DAL;
using GLPack.Models;
using GLPack.Services;
using GLPack.ViewModels.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace GLPack.Controllers
{
    [Route("company/{companyId:int}/reports")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IReportsService _reports;

        public ReportsController(ApplicationDbContext db, IReportsService reports)
        {
            _db = db;
            _reports = reports;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(int companyId, CancellationToken ct)
        {
            Company? company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId, ct);

            if (company == null) return NotFound();

            (List<TrialBalanceRow> tbRows, decimal tbDr, decimal tbCr) = await _reports.GetTrialBalanceAsync(companyId, ct);
            List<ProfitLossRowVm> plRows = await _reports.GetProfitAndLossAsync(companyId, ct);
            BalanceSheetVm bs = await _reports.GetBalanceSheetAsync(companyId, ct);

            ReportsIndexViewModel vm = new ReportsIndexViewModel
            {
                CompanyId = company.Id,
                CompanyName = company.Name,
                TrialBalanceRows = tbRows,
                TrialBalanceTotalDebit = tbDr,
                TrialBalanceTotalCredit = tbCr,
                ProfitLossRows = plRows,
                BalanceSheet = bs,
            };

            return View("Reports", vm);
        }

        [HttpGet("trial-balance.csv")]
        public async Task<IActionResult> TrialBalanceCsv(int companyId, CancellationToken ct)
        {
            Company? company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId, ct);

            if (company == null) return NotFound();

            string csv = await _reports.GetTrialBalanceCsvAsync(companyId, ct);
            byte[] bytes = Encoding.UTF8.GetBytes(csv);

            string fileName = $"TrialBalance_{company.Name}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return File(bytes, "text/csv", fileName);
        }

        [HttpGet("profit-loss.csv")]
        public async Task<IActionResult> ProfitLossCsv(int companyId, CancellationToken ct)
        {
            Company? company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId, ct);

            if (company == null) return NotFound();

            string csv = await _reports.GetProfitAndLossCsvAsync(companyId, ct);
            byte[] bytes = Encoding.UTF8.GetBytes(csv);

            string fileName = $"ProfitLoss_{company.Name}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return File(bytes, "text/csv", fileName);
        }

        [HttpGet("balance-sheet.csv")]
        public async Task<IActionResult> BalanceSheetCsv(int companyId, CancellationToken ct)
        {
            Company? company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId, ct);

            if (company == null) return NotFound();

            string csv = await _reports.GetBalanceSheetCsvAsync(companyId, ct);
            byte[] bytes = Encoding.UTF8.GetBytes(csv);

            string fileName = $"BalanceSheet_{company.Name}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return File(bytes, "text/csv", fileName);
        }
    }
}