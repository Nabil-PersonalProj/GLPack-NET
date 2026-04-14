using GLPack.Contracts;
using GLPack.Services;
using Microsoft.AspNetCore.Mvc;
using Tx = GLPack.Contracts.TransactionsDtos;

namespace GLPack.Controllers
{
    [ApiController]
    [Route("api/companies/{companyId:int}/transactions")]
    public class TransactionsController : ControllerBase
    {
        private readonly ITransactionsService _svc;
        public TransactionsController(ITransactionsService svc) => _svc = svc;

        [HttpGet]
        public async Task<ActionResult<PagedResult<Tx.TransactionDto>>> List(
            int companyId,
            int page = 1,
            int pageSize = 10,
            string? q = null,
            DateTime? from = null,
            DateTime? to = null,
            CancellationToken ct = default)
        {
            var (items, total) = await _svc.ListAsync(companyId, page, pageSize, q, from, to, ct);

            return Ok(new PagedResult<Tx.TransactionDto>
            {
                Items = items,
                Page = page < 1 ? 1 : page,
                PageSize = pageSize < 1 ? 10 : pageSize,
                TotalCount = total
            });
        }

        [HttpGet("{transactionNo:int}")]
        public async Task<ActionResult<Tx.TransactionDto>> Get(int companyId, int transactionNo, CancellationToken ct)
        {
            var item = await _svc.GetAsync(companyId, transactionNo, ct);
            return item is null ? NotFound() : Ok(item);
        }

        [HttpPost]
        public async Task<ActionResult<Tx.TransactionDto>> Create(
            int companyId,
            [FromBody] Tx.TransactionCreateDto dto,
            CancellationToken ct)
        {
            if (dto.CompanyId != companyId) return BadRequest("Mismatched companyId.");

            try
            {
                var created = await _svc.CreateAsync(dto, ct);
                return CreatedAtAction(nameof(Get), new { companyId, transactionNo = created.TransactionNo }, created);
            }
            catch (InvalidOperationException ex)
            {
                return ValidationProblem(detail: ex.Message);
            }
        }

        [HttpPut("{transactionNo:int}")]
        public async Task<IActionResult> Update(
            int companyId,
            int transactionNo,
            [FromBody] Tx.TransactionCreateDto dto,
            CancellationToken ct)
        {
            if (dto.CompanyId != companyId) return BadRequest("Mismatched companyId.");
            if (dto.TransactionNo != transactionNo) return BadRequest("Changing TransactionNo is not allowed.");

            try
            {
                await _svc.UpdateAsync(companyId, transactionNo, dto, ct);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return ValidationProblem(detail: ex.Message);
            }
        }

        [HttpDelete("{transactionNo:int}")]
        public async Task<IActionResult> Delete(int companyId, int transactionNo, CancellationToken ct)
        {
            await _svc.DeleteAsync(companyId, transactionNo, ct);
            return NoContent();
        }
    }
}