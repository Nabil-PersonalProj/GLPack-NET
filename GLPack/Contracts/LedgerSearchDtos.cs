namespace GLPack.Contracts
{
    public class LedgerSearchDtos
    {
        public sealed class LedgerSearchResultDto
        {
            public required string ResultType { get; init; } // "Account" | "Transaction"
            public required string PrimaryText { get; init; }
            public string? SecondaryText { get; init; }

            public string? AccountCode { get; init; }
            public int? TransactionNo { get; init; }
        }

        public sealed class LedgerRowDto
        {
            public DateTime Date { get; init; }
            public int TransactionNo { get; init; }
            public string? TransactionDescription { get; init; }

            public string AccountCode { get; init; } = "";
            public string AccountName { get; init; } = "";

            public string? LineDescription { get; init; }

            public decimal Debit { get; init; }
            public decimal Credit { get; init; }
        }
    }
}
