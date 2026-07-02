using GLPack.Contracts;
using GLPack.DAL;
using GLPack.Models;
using GLPack.Services;
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
        private readonly IAppLogger _logger;

        public AdminUserController(ApplicationDbContext db, IAppLogger logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet("users")]
        public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> GetUsers(
            CancellationToken ct = default)
        {
            List<AdminUserDto> users = await _db.AppUsers
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
            string email = NormalizeEmail(request.Email);

            if (string.IsNullOrWhiteSpace(email))
            {
                await LogAdminUserRejectedAsync(
                    "ADMIN_USER_CREATE_REJECTED",
                    "Email is required.",
                    nameof(CreateUser),
                    ct);

                return BadRequest("Email is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                await LogAdminUserRejectedAsync(
                    "ADMIN_USER_CREATE_REJECTED",
                    $"Password is required for user {email}.",
                    nameof(CreateUser),
                    ct);

                return BadRequest("Password is required.");
            }

            if (request.Password.Length < 6)
            {
                await LogAdminUserRejectedAsync(
                    "ADMIN_USER_CREATE_REJECTED",
                    $"Password was too short for user {email}.",
                    nameof(CreateUser),
                    ct);

                return BadRequest("Password must be at least 6 characters.");
            }

            bool exists = await _db.AppUsers.AnyAsync(x => x.Email == email, ct);

            if (exists)
            {
                await LogAdminUserRejectedAsync(
                    "ADMIN_USER_CREATE_REJECTED",
                    $"User '{email}' already exists.",
                    nameof(CreateUser),
                    ct);

                return Conflict($"User '{email}' already exists.");
            }

            AppUser user = new AppUser
            {
                Email = email,
                IsAdmin = request.IsAdmin,
                IsActive = request.IsActive,
                CreatedAtUtc = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            try
            {
                _db.AppUsers.Add(user);
                await _db.SaveChangesAsync(ct);

                try
                {
                    await _logger.LogAsync(
                        eventType: "AUDIT",
                        level: "INFO",
                        logCode: "ADMIN_USER_CREATE_OK",
                        logMessage: $"Admin created user {user.Email}.",
                        companyId: null,
                        sourceFile: nameof(AdminUserController),
                        sourceFunction: nameof(CreateUser),
                        ct: ct);
                }
                catch
                {
                    // Do not fail the user creation just because logging failed.
                }

                return Ok(ToAdminUserDto(user));
            }
            catch (Exception ex)
            {
                try
                {
                    await _logger.LogAsync(
                        eventType: "ERROR",
                        level: "ERROR",
                        logCode: "ADMIN_USER_CREATE_FAILED",
                        logMessage: $"Failed to create user {email}. Error: {ex.Message}",
                        companyId: null,
                        sourceFile: nameof(AdminUserController),
                        sourceFunction: nameof(CreateUser),
                        ct: ct);
                }
                catch
                {
                    // Avoid masking the original SaveChanges error.
                }

                throw;
            }
        }

        [HttpPut("users/{id:int}")]
        public async Task<ActionResult<AdminUserDto>> UpdateUser(
            int id,
            UpdateAdminUserRequest request,
            CancellationToken ct = default)
        {
            int? currentUserId = GetCurrentUserId();
            string email = NormalizeEmail(request.Email);

            if (string.IsNullOrWhiteSpace(email))
            {
                await LogAdminUserRejectedAsync(
                    "ADMIN_USER_UPDATE_REJECTED",
                    $"Email is required for user id {id}.",
                    nameof(UpdateUser),
                    ct);

                return BadRequest("Email is required.");
            }

            AppUser? user = await _db.AppUsers.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (user is null)
            {
                await LogAdminUserRejectedAsync(
                    "ADMIN_USER_UPDATE_REJECTED",
                    $"User id {id} was not found.",
                    nameof(UpdateUser),
                    ct);

                return NotFound("User was not found.");
            }

            bool emailTaken = await _db.AppUsers
                .AnyAsync(x => x.Id != id && x.Email == email, ct);

            if (emailTaken)
            {
                await LogAdminUserRejectedAsync(
                    "ADMIN_USER_UPDATE_REJECTED",
                    $"Email '{email}' is already used by another user.",
                    nameof(UpdateUser),
                    ct);

                return Conflict($"Email '{email}' is already used by another user.");
            }

            if (currentUserId == id)
            {
                if (!request.IsAdmin)
                {
                    await LogAdminUserRejectedAsync(
                        "ADMIN_USER_UPDATE_REJECTED",
                        "Current admin attempted to remove admin access from their own account.",
                        nameof(UpdateUser),
                        ct);

                    return BadRequest("You cannot remove admin access from your own account.");
                }

                if (!request.IsActive)
                {
                    await LogAdminUserRejectedAsync(
                        "ADMIN_USER_UPDATE_REJECTED",
                        "Current admin attempted to deactivate their own account.",
                        nameof(UpdateUser),
                        ct);

                    return BadRequest("You cannot deactivate your own account.");
                }
            }

            user.Email = email;
            user.IsAdmin = request.IsAdmin;
            user.IsActive = request.IsActive;

            try
            {
                await _db.SaveChangesAsync(ct);
                try
                {
                    await _logger.LogAsync(
                        eventType: "AUDIT",
                        level: "INFO",
                        logCode: "ADMIN_USER_UPDATE_OK",
                        logMessage: $"Admin update user {user.Email}.",
                        companyId: null,
                        sourceFile: nameof(AdminUserController),
                        sourceFunction: nameof(UpdateUser),
                        ct: ct);
                }
                catch
                {
                    // Do not fail the user creation just because logging failed.
                }

                return Ok(ToAdminUserDto(user));
            }
            catch (Exception ex)
            {
                try
                {
                    await _logger.LogAsync(
                        eventType: "ERROR",
                        level: "ERROR",
                        logCode: "ADMIN_USER_UPDATE_FAILED",
                        logMessage: $"Failed to update user {email}. Error: {ex.Message}",
                        companyId: null,
                        sourceFile: nameof(AdminUserController),
                        sourceFunction: nameof(UpdateUser),
                        ct: ct);
                }
                catch
                {
                    // Avoid masking the original SaveChanges error.
                }

                throw;

            }
        }

        [HttpPost("users/{id:int}/reset-password")]
        public async Task<IActionResult> ResetUserPassword(
            int id,
            ResetAdminUserPasswordRequest request,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                await LogAdminUserRejectedAsync(
                    "ADMIN_USER_RESET_PASSWORD_REJECTED",
                    $"Password is required for user id {id}.",
                    nameof(ResetUserPassword),
                    ct);

                return BadRequest("Password is required.");
            }

            if (request.Password.Length < 6)
            {
                await LogAdminUserRejectedAsync(
                    "ADMIN_USER_RESET_PASSWORD_REJECTED",
                    $"Password was too short for user id {id}.",
                    nameof(ResetUserPassword),
                    ct);

                return BadRequest("Password must be at least 6 characters.");
            }

            AppUser? user = await _db.AppUsers.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (user is null)
            {
                await LogAdminUserRejectedAsync(
                    "ADMIN_USER_RESET_PASSWORD_REJECTED",
                    $"User id {id} was not found.",
                    nameof(ResetUserPassword),
                    ct);

                return NotFound("User was not found.");
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            try
            {
                await _db.SaveChangesAsync(ct);
                try
                {
                    await _logger.LogAsync(
                        eventType: "AUDIT",
                        level: "INFO",
                        logCode: "ADMIN_USER_RESET_PASSWORD_OK",
                        logMessage: $"Admin reset password for user {user.Email}.",
                        companyId: null,
                        sourceFile: nameof(AdminUserController),
                        sourceFunction: nameof(ResetUserPassword),
                        ct: ct);
                }
                catch
                {
                    // Do not fail the user creation just because logging failed.
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                try
                {
                    await _logger.LogAsync(
                        eventType: "ERROR",
                        level: "ERROR",
                        logCode: "ADMIN_USER_RESET_PASSWORD_FAILED",
                        logMessage: $"Failed to reset password for user {user.Email}. Error: {ex.Message}",
                        companyId: null,
                        sourceFile: nameof(AdminUserController),
                        sourceFunction: nameof(ResetUserPassword),
                        ct: ct);
                }
                catch
                {
                    // Avoid masking the original SaveChanges error.
                }

                throw;

            }
        }

        [HttpDelete("users/{id:int}")]
        public async Task<IActionResult> DeleteUser(
            int id,
            CancellationToken ct = default)
        {
            int? currentUserId = GetCurrentUserId();

            if (currentUserId == id)
            {
                await LogAdminUserRejectedAsync(
                    "ADMIN_USER_DELETE_REJECTED",
                    "Current admin attempted to delete their own account.",
                    nameof(DeleteUser),
                    ct);

                return BadRequest("You cannot delete your own account.");
            }

            AppUser? user = await _db.AppUsers.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (user is null)
            {
                await LogAdminUserRejectedAsync(
                    "ADMIN_USER_DELETE_REJECTED",
                    $"User id {id} was not found.",
                    nameof(DeleteUser),
                    ct);

                return NotFound("User was not found.");
            }

            try
            {
                _db.AppUsers.Remove(user);
                await _db.SaveChangesAsync(ct);
                try
                {
                    await _logger.LogAsync(
                        eventType: "AUDIT",
                        level: "INFO",
                        logCode: "ADMIN_USER_DELETE_OK",
                        logMessage: $"Admin delete user {user.Email}.",
                        companyId: null,
                        sourceFile: nameof(AdminUserController),
                        sourceFunction: nameof(DeleteUser),
                        ct: ct);
                }
                catch
                {
                    // Do not fail the user creation just because logging failed.
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                try
                {
                    await _logger.LogAsync(
                        eventType: "ERROR",
                        level: "ERROR",
                        logCode: "ADMIN_USER_DELETE_FAILED",
                        logMessage: $"Failed to delete user {user.Email}. Error: {ex.Message}",
                        companyId: null,
                        sourceFile: nameof(AdminUserController),
                        sourceFunction: nameof(DeleteUser),
                        ct: ct);
                }
                catch
                {
                    // Avoid masking the original SaveChanges error.
                }

                throw;
            }
        }


        private int? GetCurrentUserId()
        {
            string? raw = User.FindFirstValue(ClaimTypes.NameIdentifier);

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

        private async Task LogAdminUserRejectedAsync(
            string logCode,
            string message,
            string sourceFunction,
            CancellationToken ct)
        {
            await _logger.LogAsync(
                eventType: "ERROR",
                level: "WARN",
                logCode: logCode,
                logMessage: message,
                companyId: null,
                sourceFile: nameof(AdminUserController),
                sourceFunction: sourceFunction,
                ct: ct);
        }
    }
}
