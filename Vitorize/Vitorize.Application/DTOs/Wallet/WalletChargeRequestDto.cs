namespace Vitorize.Application.DTOs.Wallet
{
    public class WalletChargeRequestDto
    {
        public Guid UserId { get; set; }

        public decimal Amount { get; set; }

        public string? Description { get; set; }
    }
}