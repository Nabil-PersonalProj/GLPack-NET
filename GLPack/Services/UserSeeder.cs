using GLPack.DAL;
using GLPack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GLPack.Services
{
    public static class UserSeeder
    {
        public static async Task SeedNabilAsync(ApplicationDbContext db)
        {
            await db.Database.MigrateAsync();

            const string email = "muhammad.nabilhakeem@gmail.com";
            const string password = "super";

            var existing = await db.AppUsers
                .SingleOrDefaultAsync(u => u.Email == email);

            var hasher = new PasswordHasher<AppUser>();

            if (existing == null)
            {
                var admin = new AppUser
                {
                    Email = email,
                    IsAdmin = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    IsActive = true
                };

                admin.PasswordHash = hasher.HashPassword(admin, password);

                db.AppUsers.Add(admin);
            }
            else if (string.IsNullOrEmpty(existing.PasswordHash))
            {
                // row exists but no password yet → fix it
                existing.IsAdmin = true;
                existing.IsActive = true;
                existing.PasswordHash = hasher.HashPassword(existing, password);
            }

            await db.SaveChangesAsync();
        }
    }
}
