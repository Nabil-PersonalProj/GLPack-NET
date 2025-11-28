namespace GLPack.ViewModels.Reports
{
    public sealed class ReportsIndexViewModel
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;

        public List<TrialBalanceRow> TrialBalanceRows { get; set; } = new();
        public decimal TrialBalanceTotalDebit { get; set; }
        public decimal TrialBalanceTotalCredit { get; set; }

        public List<ProfitLossSection> ProfitLossSections { get; set; } = new();
        public decimal NetProfit { get; set; }
    }

    public sealed class TrialBalanceRow
    {
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    public sealed class ProfitLossSection
    {
        public string Title { get; set; } = string.Empty;
        public List<ProfitLossLine> Lines { get; set; } = new();
        public decimal Total { get; set; }
    }

    public sealed class ProfitLossLine
    {
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
