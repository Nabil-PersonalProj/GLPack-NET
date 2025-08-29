namespace GLPack.Models
{
    public class Account
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string Code { get; set; } = "";   // e.g., "CASH", "PL1"
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";   // Asset, Liability, Equity, Sales, Cost of Sale, Expense, P&L, etc.
    
        public Company Company { get; set; } = null!;
        public ICollection<TransactionEntry> Entries { get; set; } = new List<TransactionEntry>();
    }
}
