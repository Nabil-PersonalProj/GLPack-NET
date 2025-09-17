using GLPack.Contracts;
using GLPack.Services;
using Microsoft.AspNetCore.Mvc;

namespace GLPack.Controllers
{
    [ApiController]
    [Route("api/companies/{companyId:int}/accounts")]
    public class AccountsController : ControllerBase
    {
        private readonly IAccountsService _svc;
        public AccountsController(IAccountsService svc) => _svc = svc;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AccountDto>>> List(
            int companyId, string? q, int page = 1, int pageSize = 50, CancellationToken ct = default)
        {
            var items = await _svc.ListAsync(companyId, q, page, pageSize, ct);
            return Ok(items);
        }

        [HttpGet("{accountCode}")]
        public async Task<ActionResult<AccountDto>> Get(int companyId, string accountCode, CancellationToken ct)
        {
            var item = await _svc.GetAsync(companyId, accountCode, ct);
            return item is null ? NotFound() : Ok(item);
        }

        [HttpPost]
        public async Task<ActionResult<AccountDto>> Create(int companyId, [FromBody] AccountUpsertDto dto, CancellationToken ct)
        {
            if (dto.CompanyId != companyId) return BadRequest("Mismatched companyId.");

            try
            {
                var created = await _svc.CreateAsync(dto, ct);
                return CreatedAtAction(nameof(Get), new { companyId, accountCode = created.AccountCode }, created);
            }
            catch (InvalidOperationException ex)
            {
                return ValidationProblem(detail: ex.Message);
            }
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

        // Optional hard delete
        [HttpDelete("{accountCode}")]
        public async Task<IActionResult> Delete(int companyId, string accountCode, CancellationToken ct)
        {
            await _svc.DeleteAsync(companyId, accountCode, ct);
            return NoContent();
        }
    }
}
