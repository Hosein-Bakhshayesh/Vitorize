namespace Vitorize.Application.Interfaces
{
    public interface IZarinpalGatewayService
    {
        Task<(bool Success, string Authority, string PaymentUrl)> CreatePaymentAsync(
            decimal amount,
            string description,
            string? mobile = null,
            string? email = null,
            string? orderId = null);

        Task<(bool Success, long RefId)> VerifyPaymentAsync(
            string authority,
            decimal amount);

        Task<string> BuildPaymentUrlAsync(string authority);
    }
}