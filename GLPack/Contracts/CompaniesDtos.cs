namespace GLPack.Contracts
{
    public class CompanyUpsertDto
    {
        public required string Name { get; init; }
    }

    public sealed class CompanyDto : CompanyUpsertDto
    {
        public int Id { get; init; }   // matches your Company model (Id, Name)
    }
}
