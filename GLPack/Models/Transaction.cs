namespace GLPack.Models
{
    public class Transaction
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }
        public Company Company { get; set; } = null!;

        public int TransactionNo { get; set; }
        public DateTime Date { get; set; }
        public string? Description { get; set; }

        public ICollection<TransactionEntry> Items { get; set; } = new List<TransactionEntry>();
    }
}
