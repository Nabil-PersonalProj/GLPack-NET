using GLPack.ViewModels.Reports;

namespace GLPack.ViewModels.Companies
{
    public sealed class DashboardViewModel
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = "";

        public List<TrialBalanceRow> TrialBalanceRows { get; set; } = new();
        public decimal TrialBalanceTotalDebit { get; set; }
        public decimal TrialBalanceTotalCredit { get; set; }

        public int CurrentErrorCount { get; set; }
        public List<DashboardErrorRow> CurrentErrors { get; set; } = new();
        public List<DashboardRecentTransactionRow> RecentTransactions { get; set; } = new();
    }

    public sealed class DashboardErrorRow
    {
        public int TransactionNo { get; set; }
        public DateTime Date { get; set; }
        public string AccountCode { get; set; } = "";
        public string AccountName { get; set; } = "";
        public string? Memo { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string Issue { get; set; } = "";
    }

    public sealed class DashboardRecentTransactionRow
    {
        public int TransactionNo { get; set; }
        public DateTime Date { get; set; }
        public string? Description { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public bool HasErrors { get; set; }
    }
}
