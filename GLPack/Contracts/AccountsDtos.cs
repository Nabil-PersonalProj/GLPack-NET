namespace GLPack.Contracts
{
    public class AccountUpsertDto
    {
        public required int CompanyId { get; init; }   // tenant id
        public required string AccountCode { get; init; } // e.g. "1000"
        public required string Name { get; init; }     // e.g. "Cash"
        public required string Type { get; init; }     // Asset/Liability/Equity/Income/Expense
        public bool IsActive { get; init; } = true;
    }

    public sealed class AccountDto : AccountUpsertDto
    {
        public int Id { get; init; }                  // DB surrogate key (if you use one)
        public DateTime CreatedAt { get; init; }
    }
}
