using GLPack.Contracts;
using GLPack.DAL;
using GLPack.Models;
using Microsoft.EntityFrameworkCore;

namespace GLPack.Services
{
    public sealed class CompaniesService : ICompaniesService
    {
        private readonly ApplicationDbContext _db;
        public CompaniesService(ApplicationDbContext db) => _db = db;

        public async Task<CompanyDto> CreateAsync(CompanyUpsertDto dto, CancellationToken ct)
        {
            var entity = new Company { Name = dto.Name };
            _db.Companies.Add(entity);
            await _db.SaveChangesAsync(ct);
            return new CompanyDto { Id = entity.Id, Name = entity.Name };
        }

        public async Task<CompanyDto?> GetAsync(int id, CancellationToken ct)
        {
            var c = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            return c is null ? null : new CompanyDto { Id = c.Id, Name = c.Name };
        }

        public async Task<IReadOnlyList<CompanyDto>> ListAsync(string? q, int page, int pageSize, CancellationToken ct)
        {
            var query = _db.Companies.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(c => c.Name.Contains(q));
            return await query
                .OrderBy(c => c.Name)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(c => new CompanyDto { Id = c.Id, Name = c.Name })
                .ToListAsync(ct);
        }

        public async Task UpdateAsync(int id, CompanyUpsertDto dto, CancellationToken ct)
        {
            var c = await _db.Companies.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null) throw new KeyNotFoundException("Company not found.");
            c.Name = dto.Name;
            await _db.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct)
        {
            var c = await _db.Companies.Include(x => x.Accounts).Include(x => x.Transactions)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null) return;

            // Choose policy: restrict if data exists (safer), or cascade (auto-remove children).
            if (c.Accounts.Any() || c.Transactions.Any())
                throw new InvalidOperationException("Company has data; delete accounts/transactions first.");

            _db.Companies.Remove(c);
            await _db.SaveChangesAsync(ct);
        }
    }
}
