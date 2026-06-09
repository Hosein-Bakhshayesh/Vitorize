using Vitorize.Application.DTOs.Wallet;

namespace Vitorize.Application.Interfaces
{
    public interface IWalletService
    {
        Task<WalletDto> GetMyWalletAsync(Guid userId);

        Task<List<WalletTransactionDto>> GetMyTransactionsAsync(Guid userId);

        Task<WalletDto> GetUserWalletAsync(Guid userId);

        Task<List<WalletTransactionDto>> GetUserTransactionsAsync(Guid userId);

        Task<WalletDto> CreditAsync(
            Guid userId,
            decimal amount,
            byte? referenceType,
            Guid? referenceId,
            string? description);

        Task<WalletDto> DebitAsync(
            Guid userId,
            decimal amount,
            byte? referenceType,
            Guid? referenceId,
            string? description);

        Task<WalletDto> AdminChargeAsync(WalletChargeRequestDto request);

        Task<WalletDto> AdminWithdrawAsync(WalletWithdrawRequestDto request);
    }
}