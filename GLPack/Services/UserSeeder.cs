using GLPack.DAL;
using GLPack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GLPack.Services
{
    public static class UserSeeder
    {
        public static async Task SeedDefaultUsersAsync(ApplicationDbContext db)
        {
            var hasher = new PasswordHasher<AppUser>();

            await EnsureUserAsync(db, hasher,
                email: "muhammadnabil.hakeem@gmail.com",
                password: "super",
                isAdmin: true,
                isActive: true);

            await EnsureUserAsync(db, hasher,
                email: "davidchew12345@gmail.com",
                password: "1234",
                isAdmin: false,
                isActive: true);
        }

        private static async Task EnsureUserAsync(
            ApplicationDbContext db,
            PasswordHasher<AppUser> hasher,
            string email,
            string password,
            bool isAdmin,
            bool isActive)
        {
            var existing = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == email);
            if (existing != null) return;

            var user = new AppUser
            {
                Email = email,
                IsAdmin = isAdmin,
                IsActive = isActive,
                CreatedAtUtc = DateTime.UtcNow,
                LastLoginAtUtc = null
            };

            user.PasswordHash = hasher.HashPassword(user, password);

            db.AppUsers.Add(user);
            await db.SaveChangesAsync();
        }
    }
}
