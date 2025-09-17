namespace GLPack.Contracts
{
    public class TransactionsDtos
    {
        public sealed class TransactionEntryDto
        {
            public required string AccountCode { get; init; }
            public required decimal Amount { get; init; }       // always positive
            public required string DrCr { get; init; }          // "DR" or "CR"
            public string? Memo { get; init; }
        }

        public sealed class TransactionCreateDto
        {
            public required int CompanyId { get; init; }
            public required int TransactionNo { get; init; } // your chosen identifier
            public DateTime TxnDate { get; init; }
            public string? Description { get; init; }
            public required List<TransactionEntryDto> Entries { get; init; }
        }

        public sealed class TransactionDto
        {
            public required int CompanyId { get; init; }
            public required int TransactionNo { get; init; }
            public DateTime TxnDate { get; init; }
            public string? Description { get; init; }
            public IReadOnlyList<TransactionEntryDto> Entries { get; init; } = [];
        }
    }
}
