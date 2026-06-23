namespace GLPack.Contracts
{
    public sealed class AdminLogDto
    {
        public long Id { get; init; }
        public DateTime TsUtc { get; init; }

        public int? CompanyId { get; init; }

        public string SourceFile { get; init; } = "";
        public string SourceFunction { get; init; } = "";

        public string EventType { get; init; } = "";
        public string Level { get; init; } = "";
        public string LogCode { get; init; } = "";
        public string LogMessage { get; init; } = "";
    }
    public sealed class AdminPrefixRuleDto
    {
        public string Prefix { get; init; } = "";
        public string AccountType { get; init; } = "";
    }
    public sealed class UpsertPrefixRuleRequest
    {
        public string Prefix { get; init; } = "";
        public string AccountType { get; init; } = "";
    }
    public sealed class AdminUserDto
    {
        public int Id { get; init; }
        public string Email { get; init; } = "";
        public bool IsAdmin { get; init; }
        public bool IsActive { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime? LastLoginAtUtc { get; init; }
    }

    public sealed class CreateAdminUserRequest
    {
        public string Email { get; init; } = "";
        public string Password { get; init; } = "";
        public bool IsAdmin { get; init; }
        public bool IsActive { get; init; } = true;
    }

    public sealed class UpdateAdminUserRequest
    {
        public string Email { get; init; } = "";
        public bool IsAdmin { get; init; }
        public bool IsActive { get; init; }
    }

    public sealed class ResetAdminUserPasswordRequest
    {
        public string Password { get; init; } = "";
    }
}
