namespace GLPack.Models
{
    public class TransactionEntry
    {
        public int Id { get; set; }

        // add CompanyId so we can use composite FKs
        public int CompanyId { get; set; }

        // FK → Transaction via (CompanyId, TransactionNo)
        public int TransactionNo { get; set; }
        public Transaction Transaction { get; set; } = null!;

        // FK → Account via (CompanyId, AccountCode)
        public string AccountCode { get; set; } = "";
        public Account Account { get; set; } = null!;

        public string? LineDescription { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }
}
