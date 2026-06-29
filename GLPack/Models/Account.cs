namespace GLPack.Models
{
    public class Account
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";

        public Company Company { get; set; } = null!;
        public ICollection<TransactionEntry> Entries { get; set; } = new List<TransactionEntry>();
    }
    public class AccountTypePrefix
    {
        public string Prefix { get; set; } = "";
        public string AccountType { get; set; } = "";
    }
}
