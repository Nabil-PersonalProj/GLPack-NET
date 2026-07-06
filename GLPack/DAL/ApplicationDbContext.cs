using GLPack.Models;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace GLPack.DAL
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Company> Companies => Set<Company>();
        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<TransactionEntry> TransactionEntries => Set<TransactionEntry>();
        public DbSet<GLPack.Models.AppLog> AppLogs => Set<GLPack.Models.AppLog>();
        public DbSet<AppUser> AppUsers => Set<AppUser>();
        public DbSet<AccountTypePrefix> AccountTypePrefixes => Set<AccountTypePrefix>();
        public DbSet<CompanyUserAccess> CompanyUserAccesses => Set<CompanyUserAccess>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }
    }
}
