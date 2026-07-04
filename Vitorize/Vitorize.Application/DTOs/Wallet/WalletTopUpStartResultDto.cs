namespace Vitorize.Application.DTOs.Wallet
{
    public class WalletTopUpStartResultDto
    {
        public Guid TopUpId { get; set; }

        public decimal Amount { get; set; }

        public string Gateway { get; set; } = string.Empty;

        public string? Authority { get; set; }

        public string? PaymentUrl { get; set; }
    }
}
