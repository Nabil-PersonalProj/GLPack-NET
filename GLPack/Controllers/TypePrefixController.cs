using GLPack.Constants;
using GLPack.Contracts;
using GLPack.DAL;
using GLPack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GLPack.Controllers
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin")]
    public class TypePrefixController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public TypePrefixController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet("prefix-rules")]
        public async Task<ActionResult<IReadOnlyList<AdminPrefixRuleDto>>> GetPrefixRules(
        CancellationToken ct = default)
        {
            var rules = await _db.AccountTypePrefixes
                .AsNoTracking()
                .OrderBy(x => x.Prefix)
                .Select(x => new AdminPrefixRuleDto
                {
                    Prefix = x.Prefix,
                    AccountType = x.AccountType
                })
                .ToListAsync(ct);

            return Ok(rules);
        }

        [HttpPost("prefix-rules")]
        public async Task<ActionResult<AdminPrefixRuleDto>> CreatePrefixRule(
            UpsertPrefixRuleRequest request,
            CancellationToken ct = default)
        {
            var prefix = NormalizePrefix(request.Prefix);
            var accountType = AccountTypes.Normalize(request.AccountType);

            if (string.IsNullOrWhiteSpace(prefix))
                return BadRequest("Prefix is required.");

            if (string.IsNullOrWhiteSpace(accountType))
                return BadRequest("Invalid account type.");

            var exists = await _db.AccountTypePrefixes
                .AnyAsync(x => x.Prefix == prefix, ct);

            if (exists)
                return Conflict($"Prefix rule '{prefix}' already exists.");

            var rule = new AccountTypePrefix
            {
                Prefix = prefix,
                AccountType = accountType
            };

            _db.AccountTypePrefixes.Add(rule);
            await _db.SaveChangesAsync(ct);

            return CreatedAtAction(
                nameof(GetPrefixRules),
                new AdminPrefixRuleDto
                {
                    Prefix = rule.Prefix,
                    AccountType = rule.AccountType
                });
        }

        [HttpPut("prefix-rules/{prefix}")]
        public async Task<ActionResult<AdminPrefixRuleDto>> UpdatePrefixRule(
            string prefix,
            UpsertPrefixRuleRequest request,
            CancellationToken ct = default)
        {
            var normalizedPrefix = NormalizePrefix(prefix);
            var accountType = AccountTypes.Normalize(request.AccountType);

            if (string.IsNullOrWhiteSpace(normalizedPrefix))
                return BadRequest("Prefix is required.");

            if (string.IsNullOrWhiteSpace(accountType))
                return BadRequest("Invalid account type.");

            var rule = await _db.AccountTypePrefixes
                .FirstOrDefaultAsync(x => x.Prefix == normalizedPrefix, ct);

            if (rule is null)
                return NotFound($"Prefix rule '{normalizedPrefix}' was not found.");

            rule.AccountType = accountType;

            await _db.SaveChangesAsync(ct);

            return Ok(new AdminPrefixRuleDto
            {
                Prefix = rule.Prefix,
                AccountType = rule.AccountType
            });
        }

        [HttpDelete("prefix-rules/{prefix}")]
        public async Task<IActionResult> DeletePrefixRule(
            string prefix,
            CancellationToken ct = default)
        {
            var normalizedPrefix = NormalizePrefix(prefix);

            var rule = await _db.AccountTypePrefixes
                .FirstOrDefaultAsync(x => x.Prefix == normalizedPrefix, ct);

            if (rule is null)
                return NotFound($"Prefix rule '{normalizedPrefix}' was not found.");

            _db.AccountTypePrefixes.Remove(rule);
            await _db.SaveChangesAsync(ct);

            return NoContent();
        }

        [HttpGet("account-types")]
        public ActionResult<IReadOnlyList<string>> GetAccountTypes()
        {
            return Ok(AccountTypes.All);
        }

        private static string NormalizePrefix(string? value)
        {
            return (value ?? "").Trim().ToUpperInvariant();
        }
    }
}
