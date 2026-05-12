using GLPack.Contracts;
using GLPack.DAL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GLPack.Controllers
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin")]
    public class LogsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public LogsController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet("logs")]
        public async Task<ActionResult<PagedResult<AdminLogDto>>> GetLogs(
            string? q = null,
            string? level = null,
            string? eventType = null,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 10, 200);

            var query = _db.AppLogs.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(level))
            {
                var levelValue = level.Trim();

                query = query.Where(x =>
                    x.Level.ToLower() == levelValue.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(eventType))
            {
                var eventTypeValue = eventType.Trim();

                query = query.Where(x =>
                    x.EventType.ToLower().Contains(eventTypeValue.ToLower()));
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var search = q.Trim().ToLower();

                query = query.Where(x =>
                    x.LogCode.ToLower().Contains(search) ||
                    x.LogMessage.ToLower().Contains(search) ||
                    x.SourceFile.ToLower().Contains(search) ||
                    x.SourceFunction.ToLower().Contains(search) ||
                    x.EventType.ToLower().Contains(search) ||
                    x.Level.ToLower().Contains(search));
            }

            var totalCount = await query.CountAsync(ct);

            var items = await query
                .OrderByDescending(x => x.TsUtc)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AdminLogDto
                {
                    Id = x.Id,
                    TsUtc = x.TsUtc,
                    CompanyId = x.CompanyId,
                    SourceFile = x.SourceFile,
                    SourceFunction = x.SourceFunction,
                    EventType = x.EventType,
                    Level = x.Level,
                    LogCode = x.LogCode,
                    LogMessage = x.LogMessage
                })
                .ToListAsync(ct);

            return Ok(new PagedResult<AdminLogDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }
    }
}
