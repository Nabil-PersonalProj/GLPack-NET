using GLPack.DAL;
using GLPack.Models;

namespace GLPack.Services
{
    public sealed class AppLogger : IAppLogger
    {
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
            var row = new AppLog
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
                _db.AppLogs.Add(row);
                await _db.SaveChangesAsync(ct);
            }
            catch
            {
                // continue
            }
        }

    }
}
