namespace Vitorize.Application.Interfaces
{
    public interface IZarinpalGatewayService
    {
        Task<(bool Success, string Authority, string PaymentUrl)> CreatePaymentAsync(
            decimal amount,
            Vitorize.Shared.Enums.CurrencyType currency,
            string description,
            string? mobile = null,
            string? email = null,
            string? orderId = null);

        Task<Vitorize.Application.Models.Payments.ZarinpalVerificationResult> VerifyPaymentAsync(
            string authority,
            decimal amount);

        Task<string> BuildPaymentUrlAsync(string authority);
    }
}
