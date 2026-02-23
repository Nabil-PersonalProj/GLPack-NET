using System.Text;
using GLPack.DAL;
using GLPack.Services;
using GLPack.ViewModels.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            var company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId, ct);

            if (company == null) return NotFound();

            var (tbRows, tbDr, tbCr) = await _reports.GetTrialBalanceAsync(companyId, ct);
            var (plSections, netProfit) = await _reports.GetProfitAndLossAsync(companyId, ct);

            var vm = new ReportsIndexViewModel
            {
                CompanyId = company.Id,
                CompanyName = company.Name,
                TrialBalanceRows = tbRows,
                TrialBalanceTotalDebit = tbDr,
                TrialBalanceTotalCredit = tbCr,
                ProfitLossSections = plSections,
                NetProfit = netProfit
            };

            return View("Reports", vm);
        }

        [HttpGet("trial-balance.csv")]
        public async Task<IActionResult> TrialBalanceCsv(int companyId, CancellationToken ct)
        {
            var company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId, ct);

            if (company == null) return NotFound();

            var csv = await _reports.GetTrialBalanceCsvAsync(companyId, ct);
            var bytes = Encoding.UTF8.GetBytes(csv);

            var fileName = $"TrialBalance_{company.Name}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return File(bytes, "text/csv", fileName);
        }

        [HttpGet("profit-loss.csv")]
        public async Task<IActionResult> ProfitLossCsv(int companyId, CancellationToken ct)
        {
            var company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId, ct);

            if (company == null) return NotFound();

            var csv = await _reports.GetProfitAndLossCsvAsync(companyId, ct);
            var bytes = Encoding.UTF8.GetBytes(csv);

            var fileName = $"ProfitLoss_{company.Name}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return File(bytes, "text/csv", fileName);
        }
    }
}