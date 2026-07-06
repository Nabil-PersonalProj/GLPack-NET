using System.ComponentModel.DataAnnotations;

namespace GLPack.ViewModels.Companies
{
    public sealed class CompanySettingsViewModel
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = "";

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = "";

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "SGD";

        [Required]
        [RegularExpression(@"^\d{2}-\d{2}$", ErrorMessage = "Use MM-DD format, for example 12-31.")]
        public string FinancialYearEnd { get; set; } = "12-31";

        public List<CompanyAccessUserRow> AccessUsers { get; set; } = new();
        public List<CompanyAccessUserOption> AvailableAccessUsers { get; set; } = new();
    }

    public sealed class CompanyAccessUserRow
    {
        public int UserId { get; set; }
        public string Email { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime? LastLoginAtUtc { get; set; }
    }

    public sealed class CompanyAccessUserOption
    {
        public int UserId { get; set; }
        public string Email { get; set; } = "";
        public bool IsActive { get; set; }
    }

    public sealed class CompanyAccessAddRequest
    {
        public int UserId { get; set; }
    }

    public sealed class CompanyAccessRemoveRequest
    {
        public int UserId { get; set; }
    }
}
