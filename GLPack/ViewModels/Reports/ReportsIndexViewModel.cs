namespace GLPack.ViewModels.Reports
{
    public sealed class ReportsIndexViewModel
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;

        public List<TrialBalanceRow> TrialBalanceRows { get; set; } = new();
        public decimal TrialBalanceTotalDebit { get; set; }
        public decimal TrialBalanceTotalCredit { get; set; }
        public List<ProfitLossRowVm> ProfitLossRows { get; set; } = new();
        public BalanceSheetVm BalanceSheet { get; set; } = new();
    }

    public sealed class TrialBalanceRow
    {
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
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

    public sealed class ProfitLossRowVm
    {
        public string RowType { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal? Amount { get; set; }
    }

    public sealed class BalanceSheetVm
    {
        public decimal ShareCapitalTotal { get; set; }
        public List<BalanceSheetLineVm> ShareCapitalLine { get; set; } = new();
        public decimal ProfitAndLossTotal { get; set; }
        public decimal EquityTotal { get; set; }

        public List<BalanceSheetLineVm> FixedAssetLines { get; set; } = new();
        public decimal FixedAssetsFaTotal { get; set; }
        public decimal FixedAssetsPdTotal { get; set; }
        public decimal NetFixedAssets { get; set; }

        public List<BalanceSheetLineVm> CurrentAssetLines { get; set; } = new();
        public decimal TotalDebtors { get; set; }
        public decimal CurrentAssetsTotal { get; set; }

        public List<BalanceSheetLineVm> CurrentLiabilityLines { get; set; } = new();
        public decimal TotalCreditors { get; set; }
        public decimal CurrentLiabilitiesTotal { get; set; }

        public decimal NetCurrentAssets { get; set; }
        public decimal TotalAssetsLessLiabilities { get; set; }
    }

    public sealed class BalanceSheetLineVm
    {
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public sealed class AccountReportTotal
    {
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
    }


}
