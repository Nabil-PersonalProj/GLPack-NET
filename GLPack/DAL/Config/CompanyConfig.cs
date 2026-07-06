using GLPack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GLPack.DAL.Config
{
    public class CompanyConfig : IEntityTypeConfiguration<Company>
    {
        public void Configure(EntityTypeBuilder<Company> b)
        {
            b.ToTable("company");
            b.HasKey(x => x.Id);

            b.Property(x => x.Name)
             .IsRequired()
             .HasMaxLength(255);

            b.Property(x => x.Currency)
             .IsRequired()
             .HasMaxLength(3)
             .HasDefaultValue("SGD");

            b.Property(x => x.FinancialYearEnd)
             .IsRequired()
             .HasMaxLength(5)
             .HasDefaultValue("12-31");

            b.HasIndex(x => x.Name).IsUnique();
        }
    }
}
