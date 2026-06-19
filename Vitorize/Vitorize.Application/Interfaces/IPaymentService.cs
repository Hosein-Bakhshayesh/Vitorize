using Vitorize.Application.DTOs.Payments;

namespace Vitorize.Application.Interfaces
{
    public interface IPaymentService
    {
        Task<PaymentStartResultDto> StartPaymentAsync(Guid userId, Guid orderId);

        Task<PaymentVerifyResultDto> VerifyMockPaymentAsync(Guid userId, Guid paymentId);

        Task<PaymentVerifyResultDto> PayWithWalletAsync(Guid userId, Guid orderId);

        Task<PaymentVerifyResultDto> VerifyZarinpalPaymentAsync(
            string authority,
            string status);
    }
}