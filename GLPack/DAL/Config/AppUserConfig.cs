using GLPack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GLPack.DAL.Config
{
    public class AppUserConfig : IEntityTypeConfiguration<AppUser>
    {
        public void Configure(EntityTypeBuilder<AppUser> b)
        {
            b.ToTable("app_user");
            b.HasKey(x => x.Id);

            b.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(255);

            b.Property(x => x.PasswordHash)
                .IsRequired();

            b.Property(x => x.IsAdmin)
                .IsRequired();

            b.Property(x => x.CreatedAtUtc)
                .IsRequired();

            b.Property(x => x.IsActive)
                .IsRequired();

            b.HasIndex(x => x.Email)
                .IsUnique();  // one account per email
        }
    }
}
