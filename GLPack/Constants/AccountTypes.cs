namespace GLPack.Constants
{
    public static class AccountTypes
    {
        public const string Asset = "Asset";
        public const string Liabilities = "Liabilities";
        public const string Expense = "Expense";
        public const string Equity = "Equity";
        public const string ProfitAndLoss = "Profit & Loss";
        public const string Sales = "Sales";
        public const string CostOfSale = "Cost of Sale";
        public const string Debtors = "Debtors";
        public const string Creditors = "Creditors";

        public static readonly IReadOnlyList<string> All =
        [
            Asset,
            Liabilities,
            Expense,
            Equity,
            ProfitAndLoss,
            Sales,
            CostOfSale,
            Debtors,
            Creditors,
        ];

        public static string Normalize(string? value)
        {
            var trimmed = (value ?? "").Trim();

            return All.FirstOrDefault(x =>
                x.Equals(trimmed, StringComparison.OrdinalIgnoreCase)) ?? "";
        }
    }
}
