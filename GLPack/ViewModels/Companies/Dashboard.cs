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
    }
}
