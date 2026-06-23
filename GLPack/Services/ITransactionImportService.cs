using Microsoft.AspNetCore.Http;

namespace GLPack.Services
{
    public sealed record SkippedImportLine(int LineNumber, string Reason, string Text);

    public sealed record TransactionImportResult(
        int ImportedLines,
        int ImportedLinesWithErrors,
        IReadOnlyList<SkippedImportLine> SkippedLines);

    public interface ITransactionImportService
    {
        Task<TransactionImportResult> ImportCsvAsync(int companyId, IFormFile csvFile, CancellationToken ct);
    }
}
