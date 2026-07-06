using GLPack.Contracts;
using GLPack.DAL;
using GLPack.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GLPack.Controllers
{
    [ApiController]
    [Route("api/companies/{companyId:int}/accounts")]
    public class AccountsController : ControllerBase
    {
        private readonly IAccountsService _svc;
        private readonly ApplicationDbContext _db;
        private readonly IAppLogger _logger;
        private readonly ICompanyAccessService _companyAccess;

        public AccountsController(
            IAccountsService svc,
            ApplicationDbContext db,
            IAppLogger logger,
            ICompanyAccessService companyAccess)
        {
            _svc = svc;
            _db = db;
            _logger = logger;
            _companyAccess = companyAccess;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<AccountDto>>> List(
            int companyId, string? q, string? accountType, int page = 1, int pageSize = 10, CancellationToken ct = default)
        {
            if (!await CanAccessCompanyAsync(companyId, ct)) return NotFound();

            PagedResult<AccountDto> result = await _svc.ListAsync(companyId, q, accountType, page, pageSize, ct);
            return Ok(result);
        }

        [HttpGet("{accountCode}")]
        public async Task<ActionResult<AccountDto>> Get(int companyId, string accountCode, CancellationToken ct)
        {
            if (!await CanAccessCompanyAsync(companyId, ct)) return NotFound();

            var item = await _svc.GetAsync(companyId, accountCode, ct);
            return item is null ? NotFound() : Ok(item);
        }

        [HttpPut("{accountCode}")]
        public async Task<IActionResult> Update(int companyId, string accountCode, [FromBody] AccountUpsertDto dto, CancellationToken ct)
        {
            if (!await CanAccessCompanyAsync(companyId, ct)) return NotFound();

            if (dto.CompanyId != companyId)
            {
                await LogRejectedAsync(
                    "ACCOUNTS_UPDATE_REJECTED",
                    $"Mismatched companyId. Route={companyId}, Payload={dto.CompanyId}.",
                    companyId,
                    nameof(Update),
                    ct);

                return BadRequest("Mismatched companyId.");
            }

            if (!string.Equals(dto.AccountCode, accountCode, StringComparison.Ordinal))
            {
                await LogRejectedAsync(
                    "ACCOUNTS_UPDATE_REJECTED",
                    $"Changing AccountCode is not allowed. Route='{accountCode}', Payload='{dto.AccountCode}'.",
                    companyId,
                    nameof(Update),
                    ct);

                return BadRequest("Changing AccountCode is not allowed.");
            }

            try
            {
                await _svc.UpdateAsync(companyId, accountCode, dto, ct);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete("{accountCode}")]
        public async Task<IActionResult> Delete(int companyId, string accountCode, CancellationToken ct)
        {
            if (!await CanAccessCompanyAsync(companyId, ct)) return NotFound();

            await _svc.DeleteAsync(companyId, accountCode, ct);
            return NoContent();
        }

        [HttpPost("from-prefix")]
        public async Task<ActionResult<AccountDto>> CreateFromPrefix(int companyId, [FromBody] AccountCreateFromPrefixDto dto,
            CancellationToken ct)
        {
            if (!await CanAccessCompanyAsync(companyId, ct)) return NotFound();

            if (dto.CompanyId != companyId)
            {
                await LogRejectedAsync(
                    "ACCOUNTS_CREATE_FROM_PREFIX_REJECTED",
                    $"Mismatched companyId. Route={companyId}, Payload={dto.CompanyId}.",
                    companyId,
                    nameof(CreateFromPrefix),
                    ct);

                return BadRequest("Mismatched companyId.");
            }

            try
            {
                AccountDto created = await _svc.CreateFromPrefixAsync(dto, ct);

                return CreatedAtAction(
                    nameof(Get),
                    new { companyId, accountCode = created.AccountCode },
                    created);
            }
            catch (InvalidOperationException ex)
            {
                return ValidationProblem(detail: ex.Message);
            }
        }

        [HttpPost("import")]
        public async Task<ActionResult<AccountImportResult>> Import(int companyId, [FromForm] IFormFile importFile, CancellationToken ct)
        {
            if (!await CanAccessCompanyAsync(companyId, ct)) return NotFound();

            try
            {
                AccountImportResult result = await _svc.ImportAsync(companyId, importFile, ct);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("prefix-rules")]
        public async Task<ActionResult<IReadOnlyList<AdminPrefixRuleDto>>> GetPrefixRules(int companyId, CancellationToken ct)
        {
            if (!await CanAccessCompanyAsync(companyId, ct)) return NotFound();

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

        private async Task LogRejectedAsync(
            string logCode,
            string message,
            int companyId,
            string sourceFunction,
            CancellationToken ct)
        {
            await _logger.LogAsync(
                eventType: "ERROR",
                level: "WARN",
                logCode: logCode,
                logMessage: message,
                companyId: companyId,
                sourceFile: nameof(AccountsController),
                sourceFunction: sourceFunction,
                ct: ct);
        }

        private Task<bool> CanAccessCompanyAsync(int companyId, CancellationToken ct)
        {
            return _companyAccess.CanAccessCompanyAsync(User, companyId, ct);
        }
    }
}
