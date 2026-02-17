using GLPack.Services;
using Microsoft.AspNetCore.Mvc;
using static GLPack.Contracts.LedgerSearchDtos;

namespace GLPack.Controllers
{
    [ApiController]
    [Route("api/companies/{companyId:int}/search")]
    public class LedgerSearchController : ControllerBase
    {
        private readonly ILedgerSearchService _svc;
        public LedgerSearchController(ILedgerSearchService svc) => _svc = svc;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<LedgerRowDto>>> Search(
            int companyId,
            string? q,
            string? accountCode,
            int? transactionNo,
            DateTime? from = null,
            DateTime? to = null,
            int page = 1,
            int pageSize = 100,
            CancellationToken ct = default)
        {
            var results = await _svc.SearchAsync(companyId, q, accountCode, transactionNo, from, to, page, pageSize, ct);
            return Ok(results);
        }
    }
}
