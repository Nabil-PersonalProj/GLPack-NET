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

        public AccountsController(IAccountsService svc, ApplicationDbContext db)
        {
            _svc = svc;
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<AccountDto>>> List(
            int companyId, string? q, int page = 1, int pageSize = 10, CancellationToken ct = default)
        {
            PagedResult<AccountDto> result = await _svc.ListAsync(companyId, q, page, pageSize, ct);
            return Ok(result);
        }

        [HttpGet("{accountCode}")]
        public async Task<ActionResult<AccountDto>> Get(int companyId, string accountCode, CancellationToken ct)
        {
            var item = await _svc.GetAsync(companyId, accountCode, ct);
            return item is null ? NotFound() : Ok(item);
        }

        [HttpPut("{accountCode}")]
        public async Task<IActionResult> Update(int companyId, string accountCode, [FromBody] AccountUpsertDto dto, CancellationToken ct)
        {
            if (dto.CompanyId != companyId) return BadRequest("Mismatched companyId.");
            if (!string.Equals(dto.AccountCode, accountCode, StringComparison.Ordinal))
                return BadRequest("Changing AccountCode is not allowed.");

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
            await _svc.DeleteAsync(companyId, accountCode, ct);
            return NoContent();
        }

        [HttpPost("from-prefix")]
        public async Task<ActionResult<AccountDto>> CreateFromPrefix(int companyId, [FromBody] AccountCreateFromPrefixDto dto,
            CancellationToken ct)
        {
            if (dto.CompanyId != companyId)
                return BadRequest("Mismatched companyId.");

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

        [HttpGet("prefix-rules")]
        public async Task<ActionResult<IReadOnlyList<AdminPrefixRuleDto>>> GetPrefixRules(int companyId, CancellationToken ct)
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
    }
}