namespace GLPack.Contracts
{
    public class CompanyUpsertDto
    {
        public required string Name { get; init; }
        public string Currency { get; init; } = "SGD";
        public string FinancialYearEnd { get; init; } = "12-31";
    }

    public sealed class CompanyDto : CompanyUpsertDto
    {
        public int Id { get; init; }
    }
}
