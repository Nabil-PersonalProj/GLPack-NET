using System.Globalization;
using System.Text;
using GLPack.DAL;
using GLPack.ViewModels.Reports;
using Microsoft.EntityFrameworkCore;

namespace GLPack.Services
{
    public sealed class ReportsService : IReportsService
    {
        private readonly ApplicationDbContext _db;

        public ReportsService(ApplicationDbContext db)
        {
            _db = db;
        }

        // ----------------------------
        // DATA BUILDERS (shared)
        // ----------------------------

        public async Task<(List<TrialBalanceRow> Rows, decimal TotalDebit, decimal TotalCredit)>
            GetTrialBalanceAsync(int companyId, CancellationToken ct)
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

        public async Task<(List<ProfitLossSection> Sections, decimal NetProfit)>
            GetProfitAndLossAsync(int companyId, CancellationToken ct)
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

        // ----------------------------
        // CSV EXPORTS (reuse builders)
        // ----------------------------

        public async Task<string> GetTrialBalanceCsvAsync(int companyId, CancellationToken ct)
        {
            var (rows, totalDebit, totalCredit) = await GetTrialBalanceAsync(companyId, ct);

            var sb = new StringBuilder();
            var csv = new CsvWriter(sb);

            csv.WriteRow("Account Code", "Account Name", "Account Type", "Debit", "Credit");

            foreach (var r in rows)
            {
                csv.WriteRow(
                    r.AccountCode,
                    r.AccountName,
                    r.AccountType,
                    r.Debit.ToString("0.00", CultureInfo.InvariantCulture),
                    r.Credit.ToString("0.00", CultureInfo.InvariantCulture)
                );
            }

            csv.WriteRow();
            csv.WriteRow("TOTAL", "", "",
                totalDebit.ToString("0.00", CultureInfo.InvariantCulture),
                totalCredit.ToString("0.00", CultureInfo.InvariantCulture));

            return sb.ToString();
        }

        public async Task<string> GetProfitAndLossCsvAsync(int companyId, CancellationToken ct)
        {
            var (sections, netProfit) = await GetProfitAndLossAsync(companyId, ct);

            var sb = new StringBuilder();
            var csv = new CsvWriter(sb);

            csv.WriteRow("Category", "Account Code", "Account Name", "Amount");

            foreach (var section in sections)
            {
                csv.WriteRow(section.Title);

                foreach (var line in section.Lines)
                {
                    csv.WriteRow("",
                        line.AccountCode,
                        line.AccountName,
                        line.Amount.ToString("0.00", CultureInfo.InvariantCulture));
                }

                csv.WriteRow("", "", "Total " + section.Title,
                    section.Total.ToString("0.00", CultureInfo.InvariantCulture));

                csv.WriteRow();
            }

            csv.WriteRow("Net Profit", "", "",
                netProfit.ToString("0.00", CultureInfo.InvariantCulture));

            return sb.ToString();
        }

        // ----------------------------
        // tiny CSV helper
        // ----------------------------

        private sealed class CsvWriter
        {
            private readonly StringBuilder _sb;
            public CsvWriter(StringBuilder sb) => _sb = sb;

            public void WriteRow(params string?[] values)
            {
                if (values.Length == 0)
                {
                    _sb.AppendLine();
                    return;
                }

                for (int i = 0; i < values.Length; i++)
                {
                    if (i > 0) _sb.Append(',');
                    _sb.Append(Escape(values[i] ?? string.Empty));
                }
                _sb.AppendLine();
            }

            private static string Escape(string value)
            {
                if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
                {
                    return "\"" + value.Replace("\"", "\"\"") + "\"";
                }
                return value;
            }
        }
    }
}