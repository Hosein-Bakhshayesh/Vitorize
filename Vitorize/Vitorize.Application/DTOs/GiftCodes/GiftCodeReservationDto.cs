namespace Vitorize.Application.DTOs.GiftCodes
{
    public class GiftCodeReservationDto
    {
        public Guid Id { get; set; }

        public Guid GiftCodeId { get; set; }

        public Guid ProductId { get; set; }

        public Guid? ProductVariantId { get; set; }

        public Guid UserId { get; set; }

        public byte Status { get; set; }

        public DateTime ReservedAt { get; set; }

        public DateTime ExpiresAt { get; set; }

        public string? MaskedCode { get; set; }
    }
}