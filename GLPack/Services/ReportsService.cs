using System.Globalization;
using System.Text;
using GLPack.DAL;
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

        // ---------- TRIAL BALANCE ----------

        public async Task<string> GetTrialBalanceCsvAsync(int companyId, CancellationToken ct)
        {
            // Sum debits / credits per account
            var rows = await _db.TransactionEntries
                .Where(e => e.CompanyId == companyId)
                .GroupBy(e => new { e.AccountCode, e.Account.Name, e.Account.Type })
                .Select(g => new
                {
                    g.Key.AccountCode,
                    AccountName = g.Key.Name,
                    AccountType = g.Key.Type,
                    Debit = g.Sum(x => x.Debit),
                    Credit = g.Sum(x => x.Credit)
                })
                .OrderBy(r => r.AccountCode)
                .ToListAsync(ct);

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

            // Simple control total
            var totalDebit = rows.Sum(r => r.Debit);
            var totalCredit = rows.Sum(r => r.Credit);
            csv.WriteRow();
            csv.WriteRow("TOTAL", "", "",
                totalDebit.ToString("0.00", CultureInfo.InvariantCulture),
                totalCredit.ToString("0.00", CultureInfo.InvariantCulture));

            return sb.ToString();
        }

        // ---------- PROFIT & LOSS ----------

        public async Task<string> GetProfitAndLossCsvAsync(int companyId, CancellationToken ct)
        {
            var entries = await _db.TransactionEntries
                .Where(e => e.CompanyId == companyId)
                .Select(e => new
                {
                    e.AccountCode,
                    e.Account.Name,
                    e.Account.Type,
                    e.Debit,
                    e.Credit
                })
                .ToListAsync(ct);

            var grouped = entries
                .GroupBy(e => new { e.AccountCode, e.Name, e.Type })
                .Select(g => new
                {
                    g.Key.AccountCode,
                    AccountName = g.Key.Name,
                    AccountType = g.Key.Type,
                    TotalDebit = g.Sum(x => x.Debit),
                    TotalCredit = g.Sum(x => x.Credit)
                })
                .ToList();

            // Categorise
            var sales = new List<dynamic>();
            var costOfSales = new List<dynamic>();
            var expenses = new List<dynamic>();
            var profitLossBf = new List<dynamic>();

            decimal totalSales = 0, totalCostOfSales = 0, totalExpenses = 0, totalPLBf = 0;

            foreach (var a in grouped)
            {
                // base balance: debit - credit (debit-normal)
                var balance = a.TotalDebit - a.TotalCredit;

                switch (a.AccountType)
                {
                    case "Sales":
                        var salesAmt = -balance; // credits positive
                        if (salesAmt != 0)
                        {
                            sales.Add(new { a.AccountCode, a.AccountName, Amount = salesAmt });
                            totalSales += salesAmt;
                        }
                        break;

                    case "Cost of Sale":
                        if (balance != 0)
                        {
                            costOfSales.Add(new { a.AccountCode, a.AccountName, Amount = balance });
                            totalCostOfSales += balance;
                        }
                        break;

                    case "Expense":
                        if (balance != 0)
                        {
                            expenses.Add(new { a.AccountCode, a.AccountName, Amount = balance });
                            totalExpenses += balance;
                        }
                        break;

                    case "Profit & Loss":
                        if (balance != 0)
                        {
                            profitLossBf.Add(new { a.AccountCode, a.AccountName, Amount = balance });
                            totalPLBf += balance;
                        }
                        break;
                }
            }

            var netProfit = totalSales - totalCostOfSales - totalExpenses + totalPLBf;

            var sb = new StringBuilder();
            var csv = new CsvWriter(sb);

            csv.WriteRow("Category", "Account Code", "Account Name", "Amount");

            void writeSection(string title, IEnumerable<dynamic> items, decimal total)
            {
                csv.WriteRow(title);
                foreach (var i in items)
                {
                    csv.WriteRow("",
                        i.AccountCode,
                        i.AccountName,
                        ((decimal)i.Amount).ToString("0.00", CultureInfo.InvariantCulture));
                }
                csv.WriteRow("", "", "Total " + title,
                    total.ToString("0.00", CultureInfo.InvariantCulture));
                csv.WriteRow();
            }

            writeSection("Sales", sales, totalSales);
            writeSection("Cost of Sales", costOfSales, totalCostOfSales);
            writeSection("Expenses", expenses, totalExpenses);
            writeSection("Profit & Loss B/F", profitLossBf, totalPLBf);

            csv.WriteRow("Net Profit", "", "",
                netProfit.ToString("0.00", CultureInfo.InvariantCulture));

            return sb.ToString();
        }

        // ---------- tiny CSV helper ----------

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
