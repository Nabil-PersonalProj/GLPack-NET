using GLPack.Contracts;
using GLPack.Services;
using Microsoft.AspNetCore.Mvc;

namespace GLPack.Controllers
{

    [ApiController]
    [Route("api/companies")]
    public class CompaniesController : ControllerBase
    {

        private readonly ICompaniesService _svc;
        public CompaniesController(ICompaniesService svc) => _svc = svc;

        [HttpPost]
        public async Task<ActionResult<CompanyDto>> Create([FromBody] CompanyUpsertDto dto, CancellationToken ct)
        {
            var created = await _svc.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<CompanyDto>> Get(int id, CancellationToken ct)
        {
            var item = await _svc.GetAsync(id, ct);
            return item is null ? NotFound() : Ok(item);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CompanyDto>>> List(string? q, int page = 1, int pageSize = 50, CancellationToken ct = default)
            => Ok(await _svc.ListAsync(q, page, pageSize, ct));

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] CompanyUpsertDto dto, CancellationToken ct)
        {
            try { await _svc.UpdateAsync(id, dto, ct); return NoContent(); }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            try { await _svc.DeleteAsync(id, ct); return NoContent(); }
            catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
        }
    }
}
