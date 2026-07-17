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
        private readonly IAppLogger _logger;
        private readonly ICompanyAccessService _companyAccess;

        public TransactionsController(
            ITransactionsService svc,
            IAppLogger logger,
            ICompanyAccessService companyAccess)
        {
            _svc = svc;
            _logger = logger;
            _companyAccess = companyAccess;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<Tx.TransactionDto>>> List(
            int companyId,
            int page = 1,
            int pageSize = 10,
            string? q = null,
            DateTime? from = null,
            DateTime? to = null,
            int? transactionNo = null,
            CancellationToken ct = default)
        {
            if (!await CanAccessCompanyAsync(companyId, ct)) return NotFound();

            (var items, int total) = await _svc.ListAsync(companyId, page, pageSize, q, transactionNo, from, to, ct);

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
            if (!await CanAccessCompanyAsync(companyId, ct)) return NotFound();

            Tx.TransactionDto? item = await _svc.GetAsync(companyId, transactionNo, ct);
            return item is null ? NotFound() : Ok(item);
        }

        [HttpGet("next-number")]
        public async Task<ActionResult<int>> GetNextNumber(int companyId, CancellationToken ct)
        {
            if (!await CanAccessCompanyAsync(companyId, ct)) return NotFound();

            return Ok(await _svc.GetNextTransactionNoAsync(companyId, ct));
        }

        [HttpPost]
        public async Task<ActionResult<Tx.TransactionDto>> Create(
            int companyId,
            [FromBody] Tx.TransactionCreateDto dto,
            CancellationToken ct)
        {
            if (!await CanAccessCompanyAsync(companyId, ct)) return NotFound();

            if (dto.CompanyId != companyId)
            {
                await LogRejectedAsync(
                    "TX_CREATE_REJECTED",
                    $"Mismatched companyId. Route={companyId}, Payload={dto.CompanyId}.",
                    companyId,
                    nameof(Create),
                    ct);

                return BadRequest("Mismatched companyId.");
            }

            try
            {
                Tx.TransactionDto created = await _svc.CreateAsync(dto, ct);
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
            if (!await CanAccessCompanyAsync(companyId, ct)) return NotFound();

            if (dto.CompanyId != companyId)
            {
                await LogRejectedAsync(
                    "TX_UPDATE_REJECTED",
                    $"Mismatched companyId. Route={companyId}, Payload={dto.CompanyId}.",
                    companyId,
                    nameof(Update),
                    ct);

                return BadRequest("Mismatched companyId.");
            }

            if (dto.TransactionNo != transactionNo)
            {
                await LogRejectedAsync(
                    "TX_UPDATE_REJECTED",
                    $"Changing TransactionNo is not allowed. Route={transactionNo}, Payload={dto.TransactionNo}.",
                    companyId,
                    nameof(Update),
                    ct);

                return BadRequest("Changing TransactionNo is not allowed.");
            }

            try
            {
                Tx.TransactionDto updated = await _svc.UpdateAsync(companyId, transactionNo, dto, ct);
                return Ok(updated);
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
            if (!await CanAccessCompanyAsync(companyId, ct)) return NotFound();

            await _svc.DeleteAsync(companyId, transactionNo, ct);
            return NoContent();
        }

        private Task<bool> CanAccessCompanyAsync(int companyId, CancellationToken ct)
        {
            return _companyAccess.CanAccessCompanyAsync(User, companyId, ct);
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
                sourceFile: nameof(TransactionsController),
                sourceFunction: sourceFunction,
                ct: ct);
        }
    }
}
