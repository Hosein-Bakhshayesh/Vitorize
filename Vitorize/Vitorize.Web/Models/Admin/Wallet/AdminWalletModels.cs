using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Admin.Wallet
{
    public class WalletModel
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string UserMobile { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class WalletTransactionModel
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

    public class WalletChargeRequestModel
    {
        [Required] public Guid UserId { get; set; }
        [Range(1, double.MaxValue)] public decimal Amount { get; set; }
        [MaxLength(1000)] public string? Description { get; set; }
    }

    public class WalletWithdrawRequestModel : WalletChargeRequestModel { }
}
