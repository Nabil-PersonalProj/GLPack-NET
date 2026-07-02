using GLPack.DAL;
using GLPack.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GLPack.Services
{
    public sealed class AppLogger : IAppLogger
    {
        private static readonly SemaphoreSlim RetentionCleanupLock = new SemaphoreSlim(1, 1);
        private static DateTime? lastRetentionCleanupUtc;

        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AppLogger(IDbContextFactory<ApplicationDbContext> dbContextFactory, IHttpContextAccessor httpContextAccessor)
        {
            _dbContextFactory = dbContextFactory;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(
            string eventType,
            string level,
            string logCode,
            string logMessage,
            int? companyId = null,
            int? userId = null,
            string? userEmail = null,
            string? sourceFile = null,
            string? sourceFunction = null,
            CancellationToken ct = default)
        {
            AppLog row = new AppLog
            {
                TsUtc = DateTime.UtcNow,
                CompanyId = companyId,
                UserId = userId ?? GetCurrentUserId(),
                UserEmail = userEmail ?? GetCurrentUserEmail(),
                SourceFile = sourceFile ?? "",
                SourceFunction = sourceFunction ?? "",
                EventType = eventType,
                Level = level,
                LogCode = logCode,
                LogMessage = logMessage
            };

            try
            {
                await using ApplicationDbContext db = await _dbContextFactory.CreateDbContextAsync(ct);
                await DeleteExpiredLogsIfDueAsync(row.TsUtc, ct);
                db.AppLogs.Add(row);
                await db.SaveChangesAsync(ct);
            }
            catch
            {
                // Logging should never block the user action that triggered it.
            }
        }

        private int? GetCurrentUserId()
        {
            string? raw = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out int id) ? id : null;
        }

        private string GetCurrentUserEmail()
        {
            return _httpContextAccessor.HttpContext?.User.Identity?.Name ?? "";
        }

        private async Task DeleteExpiredLogsIfDueAsync(DateTime nowUtc, CancellationToken ct)
        {
            if (lastRetentionCleanupUtc.HasValue &&
                nowUtc - lastRetentionCleanupUtc.Value < TimeSpan.FromDays(1))
            {
                return;
            }

            if (!await RetentionCleanupLock.WaitAsync(0, ct))
            {
                return;
            }

            try
            {
                if (lastRetentionCleanupUtc.HasValue &&
                    nowUtc - lastRetentionCleanupUtc.Value < TimeSpan.FromDays(1))
                {
                    return;
                }

                DateTime retentionCutoffUtc = nowUtc.AddMonths(-1);

                await using ApplicationDbContext db = await _dbContextFactory.CreateDbContextAsync(ct);

                await db.AppLogs
                    .Where(log => log.TsUtc < retentionCutoffUtc)
                    .ExecuteDeleteAsync(ct);

                lastRetentionCleanupUtc = nowUtc;
            }
            finally
            {
                RetentionCleanupLock.Release();
            }
        }
    }
}
