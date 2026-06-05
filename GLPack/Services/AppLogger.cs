using GLPack.DAL;
using GLPack.Models;
using Microsoft.EntityFrameworkCore;

namespace GLPack.Services
{
    public sealed class AppLogger : IAppLogger
    {
        private static readonly SemaphoreSlim RetentionCleanupLock = new SemaphoreSlim(1, 1);
        private static DateTime? lastRetentionCleanupUtc;

        private readonly ApplicationDbContext _db;

        public AppLogger(ApplicationDbContext db) => _db = db;

        public async Task LogAsync(
        string eventType,
        string level,
        string logCode,
        string logMessage,
        int? companyId = null,
        string? sourceFile = null,
        string? sourceFunction = null,
        CancellationToken ct = default)
        {
            // Always write UTC for timestamptz

            /* example usage:
             * await _appLogger.LogAsync(
                eventType: "AUDIT",
                level: "INFO",
                logCode: "TX_CREATE_OK",
                logMessage: $"Created transaction {dto.TransactionNo}",
                companyId: dto.CompanyId,
                sourceFile: nameof(TransactionsService),
                sourceFunction: nameof(CreateAsync),
                ct: ct
                );
             * */
            AppLog row = new AppLog
            {
                TsUtc = DateTime.UtcNow,
                CompanyId = companyId,
                SourceFile = sourceFile ?? "",
                SourceFunction = sourceFunction ?? "",
                EventType = eventType,
                Level = level,
                LogCode = logCode,
                LogMessage = logMessage
            };

            try
            {
                await DeleteExpiredLogsIfDueAsync(row.TsUtc, ct);
                _db.AppLogs.Add(row);
                await _db.SaveChangesAsync(ct);
            }
            catch
            {
                // continue
            }
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

                await _db.AppLogs
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
