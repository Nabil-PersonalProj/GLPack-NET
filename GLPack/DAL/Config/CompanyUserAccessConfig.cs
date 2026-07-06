using GLPack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GLPack.DAL.Config
{
    public class CompanyUserAccessConfig : IEntityTypeConfiguration<CompanyUserAccess>
    {
        public void Configure(EntityTypeBuilder<CompanyUserAccess> b)
        {
            b.ToTable("company_user_access");
            b.HasKey(x => new { x.CompanyId, x.UserId });

            b.HasIndex(x => x.UserId)
             .HasDatabaseName("IX_company_user_access_user_id");

            b.HasOne(x => x.Company)
             .WithMany()
             .HasForeignKey(x => x.CompanyId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.User)
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
