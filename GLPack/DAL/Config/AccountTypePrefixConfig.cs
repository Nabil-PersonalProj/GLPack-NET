using GLPack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GLPack.DAL.Config
{
    public class AccountTypePrefixConfig : IEntityTypeConfiguration<AccountTypePrefix>
    {
        public void Configure(EntityTypeBuilder<AccountTypePrefix> b)
        {
            b.ToTable("account_type_prefix");

            b.HasKey(x => x.Prefix);

            b.Property(x => x.Prefix)
                .IsRequired()
                .HasMaxLength(10);

            b.Property(x => x.AccountType)
                .IsRequired()
                .HasMaxLength(50);
        }
    }
}
