using GLPack.Constants;
using GLPack.Contracts;
using GLPack.DAL;
using GLPack.Models;
using GLPack.Services;
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
        private readonly IAppLogger _logger;

        public TypePrefixController(ApplicationDbContext db, IAppLogger logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet("prefix-rules")]
        public async Task<ActionResult<IReadOnlyList<AdminPrefixRuleDto>>> GetPrefixRules(
        CancellationToken ct = default)
        {
            List<AdminPrefixRuleDto> rules = await _db.AccountTypePrefixes
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
            string prefix = NormalizePrefix(request.Prefix);
            string accountType = AccountTypes.Normalize(request.AccountType);

            if (string.IsNullOrWhiteSpace(prefix))
            {
                await LogPrefixRuleRejectedAsync(
                    "PREFIX_RULE_CREATE_REJECTED",
                    "Prefix is required.",
                    prefix,
                    accountType,
                    nameof(CreatePrefixRule),
                    ct);

                return BadRequest("Prefix is required.");
            }

            if (string.IsNullOrWhiteSpace(accountType))
            {
                await LogPrefixRuleRejectedAsync(
                    "PREFIX_RULE_CREATE_REJECTED",
                    "Invalid account type.",
                    prefix,
                    request.AccountType,
                    nameof(CreatePrefixRule),
                    ct);

                return BadRequest("Invalid account type.");
            }

            bool exists = await _db.AccountTypePrefixes
                .AnyAsync(x => x.Prefix == prefix, ct);

            if (exists)
            {
                await LogPrefixRuleRejectedAsync(
                    "PREFIX_RULE_CREATE_REJECTED",
                    $"Prefix rule '{prefix}' already exists.",
                    prefix,
                    accountType,
                    nameof(CreatePrefixRule),
                    ct);

                return Conflict($"Prefix rule '{prefix}' already exists.");
            }

            AccountTypePrefix rule = new AccountTypePrefix
            {
                Prefix = prefix,
                AccountType = accountType
            };

            _db.AccountTypePrefixes.Add(rule);
            await _db.SaveChangesAsync(ct);

            await _logger.LogAsync(
                eventType: "AUDIT",
                level: "INFO",
                logCode: "PREFIX_RULE_CREATE_OK",
                logMessage: $"Created prefix rule {rule.Prefix} -> {rule.AccountType}.",
                sourceFile: nameof(TypePrefixController),
                sourceFunction: nameof(CreatePrefixRule),
                ct: ct);

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
            string normalizedPrefix = NormalizePrefix(prefix);
            string accountType = AccountTypes.Normalize(request.AccountType);

            if (string.IsNullOrWhiteSpace(normalizedPrefix))
            {
                await LogPrefixRuleRejectedAsync(
                    "PREFIX_RULE_UPDATE_REJECTED",
                    "Prefix is required.",
                    normalizedPrefix,
                    accountType,
                    nameof(UpdatePrefixRule),
                    ct);

                return BadRequest("Prefix is required.");
            }

            if (string.IsNullOrWhiteSpace(accountType))
            {
                await LogPrefixRuleRejectedAsync(
                    "PREFIX_RULE_UPDATE_REJECTED",
                    "Invalid account type.",
                    normalizedPrefix,
                    request.AccountType,
                    nameof(UpdatePrefixRule),
                    ct);

                return BadRequest("Invalid account type.");
            }

            AccountTypePrefix? rule = await _db.AccountTypePrefixes
                .FirstOrDefaultAsync(x => x.Prefix == normalizedPrefix, ct);

            if (rule is null)
            {
                await LogPrefixRuleRejectedAsync(
                    "PREFIX_RULE_UPDATE_REJECTED",
                    $"Prefix rule '{normalizedPrefix}' was not found.",
                    normalizedPrefix,
                    accountType,
                    nameof(UpdatePrefixRule),
                    ct);

                return NotFound($"Prefix rule '{normalizedPrefix}' was not found.");
            }

            string oldAccountType = rule.AccountType;
            rule.AccountType = accountType;

            await _db.SaveChangesAsync(ct);

            await _logger.LogAsync(
                eventType: "AUDIT",
                level: "INFO",
                logCode: "PREFIX_RULE_UPDATE_OK",
                logMessage: $"Updated prefix rule {rule.Prefix}: {oldAccountType} -> {rule.AccountType}.",
                sourceFile: nameof(TypePrefixController),
                sourceFunction: nameof(UpdatePrefixRule),
                ct: ct);

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
            string normalizedPrefix = NormalizePrefix(prefix);

            AccountTypePrefix? rule = await _db.AccountTypePrefixes
                .FirstOrDefaultAsync(x => x.Prefix == normalizedPrefix, ct);

            if (rule is null)
            {
                await LogPrefixRuleRejectedAsync(
                    "PREFIX_RULE_DELETE_REJECTED",
                    $"Prefix rule '{normalizedPrefix}' was not found.",
                    normalizedPrefix,
                    "",
                    nameof(DeletePrefixRule),
                    ct);

                return NotFound($"Prefix rule '{normalizedPrefix}' was not found.");
            }

            string oldAccountType = rule.AccountType;
            _db.AccountTypePrefixes.Remove(rule);
            await _db.SaveChangesAsync(ct);

            await _logger.LogAsync(
                eventType: "AUDIT",
                level: "INFO",
                logCode: "PREFIX_RULE_DELETE_OK",
                logMessage: $"Deleted prefix rule {normalizedPrefix} -> {oldAccountType}.",
                sourceFile: nameof(TypePrefixController),
                sourceFunction: nameof(DeletePrefixRule),
                ct: ct);

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

        private async Task LogPrefixRuleRejectedAsync(
            string logCode,
            string reason,
            string prefix,
            string? accountType,
            string sourceFunction,
            CancellationToken ct)
        {
            await _logger.LogAsync(
                eventType: "ERROR",
                level: "WARN",
                logCode: logCode,
                logMessage: $"{reason} Prefix='{prefix}', AccountType='{accountType ?? ""}'.",
                sourceFile: nameof(TypePrefixController),
                sourceFunction: sourceFunction,
                ct: ct);
        }
    }
}
