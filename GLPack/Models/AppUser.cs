namespace GLPack.Models
{
    public class AppUser
    {
        public int Id { get; set; }

        public string Email { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public bool IsAdmin { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? LastLoginAtUtc { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
