namespace Vitorize.Application.DTOs.Wallet
{
    public class WalletWithdrawRequestDto
    {
        public Guid UserId { get; set; }

        public decimal Amount { get; set; }

        public string? Description { get; set; }
    }
}