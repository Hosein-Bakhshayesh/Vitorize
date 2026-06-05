namespace Vitorize.Application.DTOs.GiftCodes
{
    public class ReserveGiftCodeRequestDto
    {
        public Guid ProductId { get; set; }

        public Guid? ProductVariantId { get; set; }

        public Guid? OrderId { get; set; }

        public Guid? OrderItemId { get; set; }

        public int ReservationMinutes { get; set; } = 15;
    }
}