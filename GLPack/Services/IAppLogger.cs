namespace GLPack.Services
{
    public interface IAppLogger
    {
        Task LogAsync(
            string eventType,
            string level,
            string logCode,
            string logMessage,
            int? companyId = null,
            string? sourceFile = null,
            string? sourceFunction = null,
            CancellationToken ct = default
            );
    }
}
