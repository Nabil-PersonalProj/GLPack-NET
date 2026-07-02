using GLPack.DAL;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;

namespace GLPack.Services
{
    public sealed class SqlScriptService : ISqlScriptService
    {
        private const string CreateHistoryTableSql = """
            CREATE TABLE IF NOT EXISTS app_sql_script_history (
                script_name text NOT NULL,
                script_hash text NOT NULL,
                applied_at_utc timestamp with time zone NOT NULL DEFAULT now(),
                CONSTRAINT "PK_app_sql_script_history" PRIMARY KEY (script_name)
            );
            """;

        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<SqlScriptService> _logger;

        public SqlScriptService(
            IDbContextFactory<ApplicationDbContext> dbContextFactory,
            IWebHostEnvironment environment,
            ILogger<SqlScriptService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _environment = environment;
            _logger = logger;
        }

        public async Task ApplyPendingScriptsAsync(CancellationToken ct = default)
        {
            string scriptDirectory = ResolveScriptDirectory();

            if (!Directory.Exists(scriptDirectory))
            {
                _logger.LogInformation("SQL script directory was not found: {ScriptDirectory}", scriptDirectory);
                return;
            }

            string[] scriptPaths = Directory
                .GetFiles(scriptDirectory, "*.sql", SearchOption.TopDirectoryOnly)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (scriptPaths.Length == 0)
            {
                _logger.LogInformation("No SQL scripts found in {ScriptDirectory}", scriptDirectory);
                return;
            }

            await using ApplicationDbContext db = await _dbContextFactory.CreateDbContextAsync(ct);
            DbConnection connection = db.Database.GetDbConnection();
            await connection.OpenAsync(ct);

            await using DbTransaction transaction = await connection.BeginTransactionAsync(ct);

            try
            {
                await ExecuteNonQueryAsync(connection, transaction, CreateHistoryTableSql, ct);

                foreach (string scriptPath in scriptPaths)
                {
                    await ApplyScriptIfPendingAsync(connection, transaction, scriptPath, ct);
                }

                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }

        private async Task ApplyScriptIfPendingAsync(
            DbConnection connection,
            DbTransaction transaction,
            string scriptPath,
            CancellationToken ct)
        {
            string scriptName = Path.GetFileName(scriptPath);
            string scriptText = await File.ReadAllTextAsync(scriptPath, ct);
            string scriptHash = ComputeSha256(scriptText);

            object? existingHash = await ExecuteScalarAsync(
                connection,
                transaction,
                "SELECT script_hash FROM app_sql_script_history WHERE script_name = @scriptName;",
                ct,
                ("scriptName", scriptName));

            if (existingHash is string appliedHash)
            {
                if (!string.Equals(appliedHash, scriptHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"SQL script '{scriptName}' was already applied with a different hash. Create a new script instead of editing an applied script.");
                }

                _logger.LogInformation("SQL script already applied: {ScriptName}", scriptName);
                return;
            }

            if (!string.IsNullOrWhiteSpace(scriptText))
            {
                _logger.LogInformation("Applying SQL script: {ScriptName}", scriptName);
                await ExecuteNonQueryAsync(connection, transaction, scriptText, ct);
            }

            await ExecuteNonQueryAsync(
                connection,
                transaction,
                """
                INSERT INTO app_sql_script_history (script_name, script_hash, applied_at_utc)
                VALUES (@scriptName, @scriptHash, now());
                """,
                ct,
                ("scriptName", scriptName),
                ("scriptHash", scriptHash));
        }

        private string ResolveScriptDirectory()
        {
            string contentRootPath = Path.Combine(_environment.ContentRootPath, "Sql");

            if (Directory.Exists(contentRootPath))
            {
                return contentRootPath;
            }

            return Path.Combine(AppContext.BaseDirectory, "Sql");
        }

        private static async Task<int> ExecuteNonQueryAsync(
            DbConnection connection,
            DbTransaction transaction,
            string commandText,
            CancellationToken ct,
            params (string Name, object Value)[] parameters)
        {
            await using DbCommand command = CreateCommand(connection, transaction, commandText, parameters);
            return await command.ExecuteNonQueryAsync(ct);
        }

        private static async Task<object?> ExecuteScalarAsync(
            DbConnection connection,
            DbTransaction transaction,
            string commandText,
            CancellationToken ct,
            params (string Name, object Value)[] parameters)
        {
            await using DbCommand command = CreateCommand(connection, transaction, commandText, parameters);
            return await command.ExecuteScalarAsync(ct);
        }

        private static DbCommand CreateCommand(
            DbConnection connection,
            DbTransaction transaction,
            string commandText,
            params (string Name, object Value)[] parameters)
        {
            DbCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = commandText;
            command.CommandTimeout = 300;

            foreach ((string name, object value) in parameters)
            {
                DbParameter parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }

            return command;
        }

        private static string ComputeSha256(string value)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes);
        }
    }
}
