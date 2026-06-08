namespace Vitorize.Web.Models.Storefront
{
    public class StorePaymentVerifyResultModel
    {
        public Guid PaymentId { get; set; }

        public Guid OrderId { get; set; }

        public bool IsSuccess { get; set; }

        public string? ReferenceNumber { get; set; }

        public string? Message { get; set; }
    }
}