using Vitorize.Application.DTOs.Checkout;

namespace Vitorize.Application.Interfaces
{
    public interface ICheckoutService
    {
        Task<CheckoutResultDto> CheckoutAsync(
            Guid userId,
            CheckoutRequestDto request);
    }
}