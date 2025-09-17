using GLPack.Contracts;
using Tx = GLPack.Contracts.TransactionsDtos;

namespace GLPack.Services
{
    public interface ITransactionsService
    {
        Task<Tx.TransactionDto> CreateAsync(Tx.TransactionCreateDto dto, CancellationToken ct);
        Task<Tx.TransactionDto?> GetAsync(int companyId, int transactionNo, CancellationToken ct);
        Task<(IReadOnlyList<Tx.TransactionDto> Items, int Total)> ListAsync(
            int companyId, int page, int pageSize, DateTime? from, DateTime? to, CancellationToken ct);
        Task<Tx.TransactionDto> UpdateAsync(int companyId, int transactionNo, Tx.TransactionCreateDto dto, CancellationToken ct);
        Task DeleteAsync(int companyId, int transactionNo, CancellationToken ct);
    }
}
