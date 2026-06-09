namespace Vitorize.Application.DTOs.Wallet
{
    public class WalletTransactionDto
    {
        public Guid Id { get; set; }

        public Guid WalletId { get; set; }

        public Guid UserId { get; set; }

        public byte Type { get; set; }

        public decimal Amount { get; set; }

        public decimal BalanceAfter { get; set; }

        public byte? ReferenceType { get; set; }

        public Guid? ReferenceId { get; set; }

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}