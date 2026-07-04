namespace Vitorize.Application.DTOs.Wallet
{
    public class WalletTopUpVerifyResultDto
    {
        public Guid TopUpId { get; set; }

        public bool IsPaid { get; set; }

        public string? ReferenceNumber { get; set; }

        public decimal Amount { get; set; }

        public decimal Balance { get; set; }
    }
}
