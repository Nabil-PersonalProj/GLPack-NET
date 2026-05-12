using GLPack.Contracts;
using GLPack.DAL;
using GLPack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GLPack.Controllers
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin")]
    public class AdminUserController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly PasswordHasher<AppUser> _passwordHasher = new();

        public AdminUserController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet("users")]
        public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> GetUsers(
            CancellationToken ct = default)
        {
            var users = await _db.AppUsers
                .AsNoTracking()
                .OrderBy(x => x.Email)
                .Select(x => new AdminUserDto
                {
                    Id = x.Id,
                    Email = x.Email,
                    IsAdmin = x.IsAdmin,
                    IsActive = x.IsActive,
                    CreatedAtUtc = x.CreatedAtUtc,
                    LastLoginAtUtc = x.LastLoginAtUtc
                })
                .ToListAsync(ct);

            return Ok(users);
        }

        [HttpPost("users")]
        public async Task<ActionResult<AdminUserDto>> CreateUser(
            CreateAdminUserRequest request,
            CancellationToken ct = default)
        {
            var email = NormalizeEmail(request.Email);

            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Email is required.");

            if (string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Password is required.");

            if (request.Password.Length < 6)
                return BadRequest("Password must be at least 6 characters.");

            var exists = await _db.AppUsers.AnyAsync(x => x.Email == email, ct);

            if (exists)
                return Conflict($"User '{email}' already exists.");

            var user = new AppUser
            {
                Email = email,
                IsAdmin = request.IsAdmin,
                IsActive = request.IsActive,
                CreatedAtUtc = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            _db.AppUsers.Add(user);
            await _db.SaveChangesAsync(ct);

            return Ok(ToAdminUserDto(user));
        }

        [HttpPut("users/{id:int}")]
        public async Task<ActionResult<AdminUserDto>> UpdateUser(
            int id,
            UpdateAdminUserRequest request,
            CancellationToken ct = default)
        {
            var currentUserId = GetCurrentUserId();
            var email = NormalizeEmail(request.Email);

            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Email is required.");

            var user = await _db.AppUsers.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (user is null)
                return NotFound("User was not found.");

            var emailTaken = await _db.AppUsers
                .AnyAsync(x => x.Id != id && x.Email == email, ct);

            if (emailTaken)
                return Conflict($"Email '{email}' is already used by another user.");

            if (currentUserId == id)
            {
                if (!request.IsAdmin)
                    return BadRequest("You cannot remove admin access from your own account.");

                if (!request.IsActive)
                    return BadRequest("You cannot deactivate your own account.");
            }

            user.Email = email;
            user.IsAdmin = request.IsAdmin;
            user.IsActive = request.IsActive;

            await _db.SaveChangesAsync(ct);

            return Ok(ToAdminUserDto(user));
        }

        [HttpPost("users/{id:int}/reset-password")]
        public async Task<IActionResult> ResetUserPassword(
            int id,
            ResetAdminUserPasswordRequest request,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Password is required.");

            if (request.Password.Length < 6)
                return BadRequest("Password must be at least 6 characters.");

            var user = await _db.AppUsers.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (user is null)
                return NotFound("User was not found.");

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        [HttpDelete("users/{id:int}")]
        public async Task<IActionResult> DeleteUser(
            int id,
            CancellationToken ct = default)
        {
            var currentUserId = GetCurrentUserId();

            if (currentUserId == id)
                return BadRequest("You cannot delete your own account.");

            var user = await _db.AppUsers.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (user is null)
                return NotFound("User was not found.");

            _db.AppUsers.Remove(user);
            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        private int? GetCurrentUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (int.TryParse(raw, out var id))
                return id;

            return null;
        }

        private static string NormalizeEmail(string? value)
        {
            return (value ?? "").Trim().ToLowerInvariant();
        }

        private static AdminUserDto ToAdminUserDto(AppUser user)
        {
            return new AdminUserDto
            {
                Id = user.Id,
                Email = user.Email,
                IsAdmin = user.IsAdmin,
                IsActive = user.IsActive,
                CreatedAtUtc = user.CreatedAtUtc,
                LastLoginAtUtc = user.LastLoginAtUtc
            };
        }
    }
}
