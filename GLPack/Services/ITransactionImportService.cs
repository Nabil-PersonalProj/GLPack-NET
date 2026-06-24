namespace GLPack.Services
{
    public sealed record SkippedImportLine(int LineNumber, string Reason, string Text);

    public sealed record TransactionImportResult(
        int ImportedLines,
        int ImportedLinesWithErrors,
        IReadOnlyList<SkippedImportLine> SkippedLines);

    public interface ITransactionImportService
    {
        Task<TransactionImportResult> ImportAsync(int companyId, IFormFile importFile, CancellationToken ct);
        Task<TransactionImportResult> ImportCsvAsync(int companyId, IFormFile csvFile, CancellationToken ct);
        Task<TransactionImportResult> ImportDbfAsync(int companyId, IFormFile dbfFile, CancellationToken ct);
    }
}
