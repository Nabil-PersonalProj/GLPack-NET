using System.Globalization;
using System.Text;
using GLPack.DAL;
using GLPack.ViewModels.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GLPack.Controllers
{
    // Everything under /company/{companyId}/reports
    [Route("company/{companyId:int}/reports")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public ReportsController(ApplicationDbContext db)
        {
            _db = db;
        }

        // ---------- MAIN REPORT PAGE ----------

        [HttpGet("")]
        public async Task<IActionResult> Index(int companyId, CancellationToken ct)
        {
            var company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId, ct);

            if (company == null) return NotFound();

            var (tbRows, tbDr, tbCr) = await BuildTrialBalanceAsync(companyId, ct);
            var (plSections, netProfit) = await BuildProfitAndLossAsync(companyId, ct);

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

        // ---------- CSV ENDPOINTS ----------

        [HttpGet("trial-balance.csv")]
        public async Task<IActionResult> TrialBalanceCsv(int companyId, CancellationToken ct)
        {
            var company = await _db.Companies
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId, ct);

            if (company == null) return NotFound();

            var (rows, totalDr, totalCr) = await BuildTrialBalanceAsync(companyId, ct);

            var sb = new StringBuilder();
            sb.AppendLine("Account Code,Account Name,Account Type,Debit,Credit");

            foreach (var r in rows)
            {
                sb.AppendLine(string.Join(',', new[]
                {
                    EscapeCsv(r.AccountCode),
                    EscapeCsv(r.AccountName),
                    EscapeCsv(r.AccountType),
                    r.Debit.ToString("0.00", CultureInfo.InvariantCulture),
                    r.Credit.ToString("0.00", CultureInfo.InvariantCulture)
                }));
            }

            sb.AppendLine();
            sb.AppendLine(string.Join(',', new[]
            {
                "TOTAL",
                "",
                "",
                totalDr.ToString("0.00", CultureInfo.InvariantCulture),
                totalCr.ToString("0.00", CultureInfo.InvariantCulture)
            }));

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
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

            var (sections, netProfit) = await BuildProfitAndLossAsync(companyId, ct);

            var sb = new StringBuilder();
            sb.AppendLine("Category,Account Code,Account Name,Amount");

            foreach (var section in sections)
            {
                sb.AppendLine(EscapeCsv(section.Title)); // section header

                foreach (var line in section.Lines)
                {
                    sb.AppendLine(string.Join(',', new[]
                    {
                        "",
                        EscapeCsv(line.AccountCode),
                        EscapeCsv(line.AccountName),
                        line.Amount.ToString("0.00", CultureInfo.InvariantCulture)
                    }));
                }

                sb.AppendLine(string.Join(',', new[]
                {
                    "",
                    "",
                    EscapeCsv("Total " + section.Title),
                    section.Total.ToString("0.00", CultureInfo.InvariantCulture)
                }));

                sb.AppendLine();
            }

            sb.AppendLine(string.Join(',', new[]
            {
                EscapeCsv("Net Profit"),
                "",
                "",
                netProfit.ToString("0.00", CultureInfo.InvariantCulture)
            }));

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"ProfitLoss_{company.Name}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return File(bytes, "text/csv", fileName);
        }

        // ---------- HELPERS: build reports from DB ----------

        private async Task<(List<TrialBalanceRow> Rows, decimal TotalDebit, decimal TotalCredit)> BuildTrialBalanceAsync(
            int companyId,
            CancellationToken ct)
        {
            var rows = await _db.TransactionEntries
                .Where(e => e.CompanyId == companyId)
                .GroupBy(e => new { e.AccountCode, e.Account.Name, e.Account.Type })
                .Select(g => new TrialBalanceRow
                {
                    AccountCode = g.Key.AccountCode,
                    AccountName = g.Key.Name,
                    AccountType = g.Key.Type,
                    Debit = g.Sum(x => x.Debit),
                    Credit = g.Sum(x => x.Credit)
                })
                .OrderBy(r => r.AccountCode)
                .ToListAsync(ct);

            var totalDebit = rows.Sum(r => r.Debit);
            var totalCredit = rows.Sum(r => r.Credit);

            return (rows, totalDebit, totalCredit);
        }

        private async Task<(List<ProfitLossSection> Sections, decimal NetProfit)> BuildProfitAndLossAsync(
            int companyId,
            CancellationToken ct)
        {
            var entries = await _db.TransactionEntries
                .Where(e => e.CompanyId == companyId)
                .Select(e => new
                {
                    e.AccountCode,
                    AccountName = e.Account.Name,
                    AccountType = e.Account.Type,
                    e.Debit,
                    e.Credit
                })
                .ToListAsync(ct);

            var grouped = entries
                .GroupBy(e => new { e.AccountCode, e.AccountName, e.AccountType })
                .Select(g => new
                {
                    g.Key.AccountCode,
                    g.Key.AccountName,
                    g.Key.AccountType,
                    TotalDebit = g.Sum(x => x.Debit),
                    TotalCredit = g.Sum(x => x.Credit)
                })
                .ToList();

            var sales = new ProfitLossSection { Title = "Sales" };
            var costOfSales = new ProfitLossSection { Title = "Cost of Sales" };
            var expenses = new ProfitLossSection { Title = "Expenses" };
            var plBf = new ProfitLossSection { Title = "P&L B/F" };

            foreach (var a in grouped)
            {
                // base balance: debit - credit (debit-normal)
                var balance = a.TotalDebit - a.TotalCredit;

                switch (a.AccountType)
                {
                    case "Sales":
                        // credits positive
                        var salesAmt = -balance;
                        if (salesAmt != 0)
                        {
                            sales.Lines.Add(new ProfitLossLine
                            {
                                AccountCode = a.AccountCode,
                                AccountName = a.AccountName,
                                Amount = salesAmt
                            });
                            sales.Total += salesAmt;
                        }
                        break;

                    case "Cost of Sale":
                        if (balance != 0)
                        {
                            costOfSales.Lines.Add(new ProfitLossLine
                            {
                                AccountCode = a.AccountCode,
                                AccountName = a.AccountName,
                                Amount = balance
                            });
                            costOfSales.Total += balance;
                        }
                        break;

                    case "Expense":
                        if (balance != 0)
                        {
                            expenses.Lines.Add(new ProfitLossLine
                            {
                                AccountCode = a.AccountCode,
                                AccountName = a.AccountName,
                                Amount = balance
                            });
                            expenses.Total += balance;
                        }
                        break;

                    case "P&L":
                        if (balance != 0)
                        {
                            plBf.Lines.Add(new ProfitLossLine
                            {
                                AccountCode = a.AccountCode,
                                AccountName = a.AccountName,
                                Amount = balance
                            });
                            plBf.Total += balance;
                        }
                        break;
                }
            }

            var sections = new List<ProfitLossSection>();
            if (sales.Lines.Count > 0 || sales.Total != 0) sections.Add(sales);
            if (costOfSales.Lines.Count > 0 || costOfSales.Total != 0) sections.Add(costOfSales);
            if (expenses.Lines.Count > 0 || expenses.Total != 0) sections.Add(expenses);
            if (plBf.Lines.Count > 0 || plBf.Total != 0) sections.Add(plBf);

            var netProfit = sales.Total - costOfSales.Total - expenses.Total + plBf.Total;

            return (sections, netProfit);
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }
    }
}
