using Vitorize.Application.DTOs.Wallet;

namespace Vitorize.Application.Interfaces
{
    public interface IWalletTopUpService
    {
        Task<WalletTopUpStartResultDto> StartAsync(Guid userId, WalletTopUpRequestDto request);

        Task<WalletTopUpVerifyResultDto> VerifyMockAsync(Guid userId, Guid topUpId);

        Task<bool> IsTopUpAuthorityAsync(string authority);

        Task<WalletTopUpVerifyResultDto> VerifyZarinpalAsync(string authority, string status);
    }
}
