namespace Vitorize.Application.DTOs.Wallet
{
    public class WalletDto
    {
        public Guid WalletId { get; set; }

        public Guid UserId { get; set; }

        public decimal Balance { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}