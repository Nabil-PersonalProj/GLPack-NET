using GLPack.DAL;
using GLPack.Models;
using GLPack.Services;
using GLPack.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GLPack.Controllers
{
    public class UserAccountController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IAppLogger _logger;
        private readonly PasswordHasher<AppUser> _passwordHasher = new();

        public UserAccountController(ApplicationDbContext db, IAppLogger logger)
        {
            _db = db;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _db.AppUsers
                .SingleOrDefaultAsync(u => u.Email == model.Email && u.IsActive, ct);

            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(model);
            }

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
            if (result == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(model);
            }

            user.LastLoginAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email),
                new Claim("IsAdmin", user.IsAdmin ? "True" : "False")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal);

            await _logger.LogAsync(
                eventType: "AUTH",
                level: "INFO",
                logCode: "LOGIN_SUCCESS",
                logMessage: $"User {user.Email} logged in.",
                companyId: null,
                sourceFile: nameof(UserAccountController),
                sourceFunction: nameof(Login),
                ct: ct);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            var email = User.Identity?.Name ?? "(unknown)";

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            await _logger.LogAsync(
                eventType: "AUTH",
                level: "INFO",
                logCode: "LOGOUT",
                logMessage: $"User {email} logged out.",
                companyId: null,
                sourceFile: nameof(UserAccountController),
                sourceFunction: nameof(Logout),
                ct: ct);

            return RedirectToAction("Login", "UserAccount");
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
