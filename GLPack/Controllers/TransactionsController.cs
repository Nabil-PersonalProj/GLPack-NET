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

        [HttpPost]
        public async Task<ActionResult<Tx.TransactionDto>> Create(
            int companyId, [FromBody] Tx.TransactionCreateDto dto, CancellationToken ct)
        {
            if (companyId != dto.CompanyId) return BadRequest("Mismatched companyId.");
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

        [HttpGet("{transactionNo:int}")]
        public async Task<ActionResult<Tx.TransactionDto>> Get(int companyId, int transactionNo, CancellationToken ct)
        {
            var tr = await _svc.GetAsync(companyId, transactionNo, ct);
            return tr is null ? NotFound() : Ok(tr);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Tx.TransactionDto>>> List(
            int companyId, int page = 1, int pageSize = 50, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
        {
            var (items, total) = await _svc.ListAsync(companyId, page, pageSize, from, to, ct);
            Response.Headers.Append("X-Total-Count", total.ToString());
            return Ok(items);
        }

        [HttpPut("{transactionNo:int}")]
        public async Task<ActionResult<Tx.TransactionDto>> Update(
        int companyId, int transactionNo, [FromBody] Tx.TransactionCreateDto dto, CancellationToken ct)
        {
            try
            {
                var updated = await _svc.UpdateAsync(companyId, transactionNo, dto, ct);
                return Ok(updated);
            }
            catch (InvalidOperationException ex) { return ValidationProblem(detail: ex.Message); }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        [HttpDelete("{transactionNo:int}")]
        public async Task<IActionResult> Delete(int companyId, int transactionNo, CancellationToken ct)
        {
            await _svc.DeleteAsync(companyId, transactionNo, ct);
            return NoContent();
        }
    }
}
