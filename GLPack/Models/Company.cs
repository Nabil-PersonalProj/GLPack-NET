namespace GLPack.Models
{
    public class Company
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Currency { get; set; } = "SGD";
        public string FinancialYearEnd { get; set; } = "12-31";

        public ICollection<Account> Accounts { get; set; } = new List<Account>();
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
