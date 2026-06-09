namespace Vitorize.Application.DTOs.Admin.Users
{
    public class AdminUserDto
    {
        public Guid Id { get; set; }

        public string FullName { get; set; } = null!;

        public string Mobile { get; set; } = null!;

        public string? Email { get; set; }

        public byte Status { get; set; }

        public byte VerificationStatus { get; set; }

        public bool IsMobileConfirmed { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public decimal WalletBalance { get; set; }

        public int OrdersCount { get; set; }

        public int TicketsCount { get; set; }

        public List<string> Roles { get; set; } = new();
    }
}