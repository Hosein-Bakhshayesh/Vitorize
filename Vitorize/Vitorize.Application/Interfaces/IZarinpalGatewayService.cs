namespace Vitorize.Application.Interfaces
{
    public interface IZarinpalGatewayService
    {
        Task<(bool Success,
            string Authority,
            string PaymentUrl)>
            CreatePaymentAsync(
                decimal amount,
                string description);

        Task<(bool Success,
            long RefId)>
            VerifyPaymentAsync(
                string authority,
                decimal amount);
    }
}