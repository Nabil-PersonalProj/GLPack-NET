using GLPack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GLPack.DAL.Config
{
    public class TransactionConfig : IEntityTypeConfiguration<Transaction>
    {
        public void Configure(EntityTypeBuilder<Transaction> b)
        {
            b.ToTable("transaction");
            b.HasKey(x => x.Id);

            b.Property(x => x.TransactionNo).IsRequired();
            b.Property(x => x.Date).IsRequired();
            b.Property(x => x.Description).HasMaxLength(500);

            // enforce uniqueness and expose as alternate key
            b.HasIndex(x => new { x.CompanyId, x.TransactionNo }).IsUnique();
            b.HasAlternateKey(x => new { x.CompanyId, x.TransactionNo });

            b.HasOne(x => x.Company)
             .WithMany(c => c.Transactions)
             .HasForeignKey(x => x.CompanyId)
             .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
