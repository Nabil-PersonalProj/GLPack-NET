using Microsoft.AspNetCore.Http;

namespace GLPack.Services
{
    public interface ITransactionImportService
    {
        Task<int> ImportCsvAsync(int companyId, IFormFile csvFile, CancellationToken ct);
    }
}
