namespace GLPack.Models
{
    public class CompanyUserAccess
    {
        public int CompanyId { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public Company Company { get; set; } = null!;
        public AppUser User { get; set; } = null!;
    }
}
