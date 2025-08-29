using GLPack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GLPack.DAL.Config
{
    public class TransactionEntryConfig : IEntityTypeConfiguration<TransactionEntry>
    {
        public void Configure(EntityTypeBuilder<TransactionEntry> b)
        {
            b.ToTable("transaction_item");
            b.HasKey(x => x.Id);

            // column names
            b.Property(x => x.CompanyId).HasColumnName("company_id");
            b.Property(x => x.TransactionNo).HasColumnName("transaction_no");
            b.Property(x => x.AccountCode).HasColumnName("account_code");

            b.Property(x => x.LineDescription).HasMaxLength(500);
            b.Property(x => x.Debit).HasColumnName("debit").HasColumnType("numeric(18,2)");
            b.Property(x => x.Credit).HasColumnName("credit").HasColumnType("numeric(18,2)");

            // (CompanyId, TransactionNo) -> Transaction(CompanyId, TransactionNo)
            b.HasOne(x => x.Transaction)
             .WithMany(t => t.Items)
             .HasForeignKey(x => new { x.CompanyId, x.TransactionNo })
             .HasPrincipalKey(t => new { t.CompanyId, t.TransactionNo })
             .OnDelete(DeleteBehavior.Cascade);

            // (CompanyId, AccountCode) -> Account(CompanyId, Code)
            b.HasOne(x => x.Account)
             .WithMany(a => a.Entries)
             .HasForeignKey(x => new { x.CompanyId, x.AccountCode })
             .HasPrincipalKey(a => new { a.CompanyId, a.Code })
             .OnDelete(DeleteBehavior.Restrict);

            // integrity checks
            b.ToTable(t =>
            {
                t.HasCheckConstraint("ck_entry_nonneg", "(debit >= 0 AND credit >= 0)");
                t.HasCheckConstraint("ck_entry_one_side",
                    "((debit = 0 AND credit > 0) OR (credit = 0 AND debit > 0))");
            });
        }
    }
}
