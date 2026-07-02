namespace GLPack.Services
{
    public interface ISqlScriptService
    {
        Task ApplyPendingScriptsAsync(CancellationToken ct = default);
    }
}
