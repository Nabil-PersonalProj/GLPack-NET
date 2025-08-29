using System.Security.Principal;

namespace GLPack.Models
{
    public class Company
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";

        public ICollection<Account> Accounts { get; set; } = new List<Account>();
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();


    }
}

