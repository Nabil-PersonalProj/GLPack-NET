using GLPack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GLPack.DAL.Config
{
    public class AccountConfig : IEntityTypeConfiguration<Account>
    {
        public void Configure(EntityTypeBuilder<Account> b)
        {
            b.ToTable("account");
            b.HasKey(x => x.Id);

            b.Property(x => x.Code).IsRequired().HasMaxLength(50);
            b.Property(x => x.Name).IsRequired().HasMaxLength(255);
            b.Property(x => x.Type).IsRequired().HasMaxLength(50);

            // Code unique within a company
            b.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
            // expose it as an alternate key so dependents can reference it
            b.HasAlternateKey(x => new { x.CompanyId, x.Code });

            b.HasOne(x => x.Company)
             .WithMany(c => c.Accounts)
             .HasForeignKey(x => x.CompanyId)
             .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
