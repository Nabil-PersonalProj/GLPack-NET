using GLPack.DAL;
using GLPack.ViewModels.Reports;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

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
                    TotalDebit = g.Sum(x => x.Debit),
                    TotalCredit = g.Sum(x => x.Credit)
                })
                .Select(x => new TrialBalanceRow
                {
                    AccountCode = x.AccountCode,
                    AccountName = x.AccountName,
                    AccountType = x.AccountType,
                    Debit = x.TotalDebit > x.TotalCredit ? x.TotalDebit - x.TotalCredit : 0m,
                    Credit = x.TotalCredit > x.TotalDebit ? x.TotalCredit - x.TotalDebit : 0m
                })
                .OrderBy(r => r.AccountCode)
                .ToListAsync(ct);

            var totalDebit = rows.Sum(r => r.Debit);
            var totalCredit = rows.Sum(r => r.Credit);

            return (rows, totalDebit, totalCredit);
        }

        public async Task<List<ProfitLossRowVm>>
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

            var rows = new List<ProfitLossRowVm>();
            decimal salesTotal = 0m;
            decimal costOfSalesTotal = 0m;
            decimal expensesTotal = 0m;
            decimal balanceBroughtDown = 0m;

            var salesLines = new List<ProfitLossRowVm>();
            var costLines = new List<ProfitLossRowVm>();
            var expenseLines = new List<ProfitLossRowVm>();
            var plLines = new List<ProfitLossRowVm>();

            foreach (var a in grouped)
            {
                var debit = a.TotalDebit;
                var credit = a.TotalCredit;
                var accountType = (a.AccountType ?? "").Trim();
                var accountCode = (a.AccountCode ?? "").Trim();

                if (accountType.Equals("Sales", StringComparison.OrdinalIgnoreCase))
                {
                    var amount = credit - debit;
                    if (amount != 0)
                    {
                        salesLines.Add(new ProfitLossRowVm
                        {
                            RowType = "Account",
                            Code = accountCode,
                            Description = a.AccountName,
                            Amount = amount
                        });
                        salesTotal += amount;
                    }
                }
                else if (
                    accountType.Equals("Cost of Sale", StringComparison.OrdinalIgnoreCase) ||
                    accountType.Equals("Cost of Sales", StringComparison.OrdinalIgnoreCase))
                {
                    var amount = debit - credit;
                    if (amount != 0)
                    {
                        costLines.Add(new ProfitLossRowVm
                        {
                            RowType = "Account",
                            Code = accountCode,
                            Description = a.AccountName,
                            Amount = amount
                        });
                        costOfSalesTotal += amount;
                    }
                }
                else if (
                    accountType.Equals("Expense", StringComparison.OrdinalIgnoreCase) ||
                    accountType.Equals("Expenses", StringComparison.OrdinalIgnoreCase))
                {
                    var amount = debit - credit;
                    if (amount != 0)
                    {
                        expenseLines.Add(new ProfitLossRowVm
                        {
                            RowType = "Account",
                            Code = accountCode,
                            Description = a.AccountName,
                            Amount = amount
                        });
                        expensesTotal += amount;
                    }
                }
                else if (accountType.Equals("Profit & Loss", StringComparison.OrdinalIgnoreCase))
                {
                    var amount = credit - debit;
                    if (amount != 0)
                    {
                        plLines.Add(new ProfitLossRowVm
                        {
                            RowType = "Account",
                            Code = accountCode,
                            Description = a.AccountName,
                            Amount = amount
                        });
                        balanceBroughtDown += amount;
                    }
                }
            }

            var grossProfit = salesTotal - costOfSalesTotal;
            var finalProfit = grossProfit - expensesTotal;
            var carriedForward = finalProfit + balanceBroughtDown;

            rows.Add(new ProfitLossRowVm { RowType = "Header", Description = "Sales" });
            rows.AddRange(salesLines);
            rows.Add(new ProfitLossRowVm { RowType = "Subtotal", Description = "Total Sales", Amount = salesTotal });
            rows.Add(new ProfitLossRowVm { RowType = "Spacer" });

            rows.Add(new ProfitLossRowVm { RowType = "Header", Description = "Cost of Sales" });
            rows.AddRange(costLines);
            rows.Add(new ProfitLossRowVm { RowType = "Subtotal", Description = "Total Cost of Sales", Amount = costOfSalesTotal });
            rows.Add(new ProfitLossRowVm { RowType = "Calculated", Description = "Gross Profit", Amount = grossProfit });
            rows.Add(new ProfitLossRowVm { RowType = "Spacer" });

            rows.Add(new ProfitLossRowVm { RowType = "Header", Description = "Expenses" });
            rows.AddRange(expenseLines);
            rows.Add(new ProfitLossRowVm { RowType = "Subtotal", Description = "Total Expenses", Amount = expensesTotal });
            rows.Add(new ProfitLossRowVm { RowType = "Calculated", Description = "Final Profit", Amount = finalProfit });
            rows.Add(new ProfitLossRowVm { RowType = "Spacer" });

            rows.Add(new ProfitLossRowVm { RowType = "Header", Description = "Balance Brought Down" });
            rows.AddRange(plLines);
            rows.Add(new ProfitLossRowVm { RowType = "Subtotal", Description = "Total Balance Brought Down", Amount = balanceBroughtDown });
            rows.Add(new ProfitLossRowVm { RowType = "Calculated", Description = "P&L Carried Forward", Amount = carriedForward });

            return rows;
        }

        public async Task<BalanceSheetVm> GetBalanceSheetAsync(int companyId, CancellationToken ct)
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

            var vm = new BalanceSheetVm();

            foreach (var a in grouped)
            {
                var code = (a.AccountCode ?? "").Trim();
                var name = (a.AccountName ?? "").Trim();
                var type = (a.AccountType ?? "").Trim();
                var amount = GetNetBalance(a.TotalDebit, a.TotalCredit);

                if (amount == 0m)
                    continue;

                // SC accounts, account type: equity
                if (type.Equals("Equity", StringComparison.OrdinalIgnoreCase) && HasPrefix(code, "SC"))
                {
                    vm.ShareCapitalLine.Add(new BalanceSheetLineVm
                    {
                        AccountCode = code,
                        AccountName = name,
                        AccountType = type,
                        Amount = amount
                    });
                    vm.ShareCapitalTotal += amount;
                    continue;
                }
                // profitNLoss account
                if (type.Equals("Profit & Loss", StringComparison.OrdinalIgnoreCase) && HasPrefix(code, "PL"))
                {
                    vm.ProfitAndLossTotal += amount;
                    continue;
                }
                // Fixed Assets FA And Accumulated Depreciation PD
                if (type.Equals("Asset", StringComparison.OrdinalIgnoreCase) && HasPrefix(code, "FA"))
                {
                    vm.FixedAssetLines.Add(new BalanceSheetLineVm
                    {
                        AccountCode = code,
                        AccountName = name,
                        AccountType = type,
                        Amount = amount
                    });

                    vm.FixedAssetsFaTotal += amount;
                    continue;
                }
                if (type.Equals("Liabilities", StringComparison.OrdinalIgnoreCase) && HasPrefix(code, "PD"))
                {
                    vm.FixedAssetLines.Add(new BalanceSheetLineVm
                    {
                        AccountCode = code,
                        AccountName = name,
                        AccountType = type,
                        Amount = amount
                    });

                    vm.FixedAssetsPdTotal += amount;
                    continue;
                }
                // Current Assets (FA accounts not included)
                if (type.Equals("Asset", StringComparison.OrdinalIgnoreCase))
                {
                    vm.CurrentAssetLines.Add(new BalanceSheetLineVm
                    {
                        AccountCode = code,
                        AccountName = name,
                        AccountType = type,
                        Amount = amount
                    });

                    vm.CurrentAssetsTotal += amount;
                    continue;
                }
                // Get TD accounts
                if (type.Equals("Debtors", StringComparison.OrdinalIgnoreCase) || type.Equals("Debtor", StringComparison.OrdinalIgnoreCase))
                {
                    vm.TotalDebtors += amount;
                    vm.CurrentAssetsTotal += amount;
                    continue;
                }
                // CURRENT LIABILITIES = all other liabilities except PD
                if (type.Equals("Liabilities", StringComparison.OrdinalIgnoreCase))
                {
                    vm.CurrentLiabilityLines.Add(new BalanceSheetLineVm
                    {
                        AccountCode = code,
                        AccountName = name,
                        AccountType = type,
                        Amount = amount
                    });

                    vm.CurrentLiabilitiesTotal += amount;
                    continue;
                }
                // Get TC accounts
                if (type.Equals("Creditors", StringComparison.OrdinalIgnoreCase) || type.Equals("Creditor", StringComparison.OrdinalIgnoreCase))
                {
                    vm.TotalCreditors += amount;
                    vm.CurrentLiabilitiesTotal += amount;
                    continue;
                }
            }
            // calculated totals
            vm.EquityTotal = vm.ShareCapitalTotal + vm.ProfitAndLossTotal;
            vm.NetFixedAssets = vm.FixedAssetsFaTotal + vm.FixedAssetsPdTotal;
            vm.NetCurrentAssets = vm.CurrentAssetsTotal + vm.CurrentLiabilitiesTotal;
            vm.TotalAssetsLessLiabilities = vm.NetFixedAssets + vm.NetCurrentAssets;

            vm.FixedAssetLines = vm.FixedAssetLines
                .OrderBy(x => x.AccountCode)
                .ToList();

            vm.CurrentAssetLines = vm.CurrentAssetLines
                .OrderBy(x => x.AccountCode)
                .ToList();

            vm.CurrentLiabilityLines = vm.CurrentLiabilityLines
                .OrderBy(x => x.AccountCode)
                .ToList();

            return vm;
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
            var rows = await GetProfitAndLossAsync(companyId, ct);

            var sb = new StringBuilder();
            var csv = new CsvWriter(sb);

            csv.WriteRow("Code", "Description", "Amount", "Total");

            foreach (var row in rows)
            {
                if (row.RowType == "Spacer")
                {
                    csv.WriteRow();
                }
                else if (row.RowType == "Header")
                {
                    csv.WriteRow("", row.Description, "");
                }
                else
                {
                    csv.WriteRow(
                        row.Code ?? "",
                        row.Description,
                        row.Amount?.ToString("0.00", CultureInfo.InvariantCulture) ?? ""
                    );
                }
            }

            return sb.ToString();
        }

        public async Task<string> GetBalanceSheetCsvAsync(int companyId, CancellationToken ct)
        {
            var bs = await GetBalanceSheetAsync(companyId, ct);

            var sb = new StringBuilder();
            var csv = new CsvWriter(sb);

            csv.WriteRow("Section", "Code", "Description", "Amount");

            csv.WriteRow("Equity", "", "Share Capital", bs.ShareCapitalTotal.ToString("0.00", CultureInfo.InvariantCulture));
            csv.WriteRow("Equity", "", "Profit and Loss Account", bs.ProfitAndLossTotal.ToString("0.00", CultureInfo.InvariantCulture));
            csv.WriteRow("Equity", "", "Total Equity", bs.EquityTotal.ToString("0.00", CultureInfo.InvariantCulture));

            csv.WriteRow();
            csv.WriteRow("Represented by", "", "", "");

            foreach (var row in bs.FixedAssetLines)
            {
                csv.WriteRow("Fixed Assets", row.AccountCode, row.AccountName, row.Amount.ToString("0.00", CultureInfo.InvariantCulture));
            }
            csv.WriteRow("Fixed Assets", "", "Net Fixed Assets", bs.NetFixedAssets.ToString("0.00", CultureInfo.InvariantCulture));

            csv.WriteRow();
            foreach (var row in bs.CurrentAssetLines)
            {
                csv.WriteRow("Current Assets", row.AccountCode, row.AccountName, row.Amount.ToString("0.00", CultureInfo.InvariantCulture));
            }
            csv.WriteRow("Current Assets", "", "Total Current Assets", bs.CurrentAssetsTotal.ToString("0.00", CultureInfo.InvariantCulture));

            csv.WriteRow();

            foreach (var row in bs.CurrentLiabilityLines)
            {
                csv.WriteRow("Current Liabilities", row.AccountCode, row.AccountName, row.Amount.ToString("0.00", CultureInfo.InvariantCulture));
            }
            csv.WriteRow("Current Liabilities", "", "Total Current Liabilities", bs.CurrentLiabilitiesTotal.ToString("0.00", CultureInfo.InvariantCulture));

            csv.WriteRow("", "", "Net Current Assets / (Liabilities)", bs.NetCurrentAssets.ToString("0.00", CultureInfo.InvariantCulture));
            csv.WriteRow("", "", "Total Assets Less Liabilities", bs.TotalAssetsLessLiabilities.ToString("0.00", CultureInfo.InvariantCulture));

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

        private static bool HasPrefix(string code, string prefix)
        {
            return !string.IsNullOrWhiteSpace(code) &&
                   code.Trim().StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static decimal GetNetBalance(decimal debit, decimal credit)
        {
            return debit - credit;
        }
    }
}