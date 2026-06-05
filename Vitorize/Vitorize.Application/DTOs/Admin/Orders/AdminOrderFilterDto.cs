namespace Vitorize.Application.DTOs.Admin.Orders
{
    public class AdminOrderFilterDto
    {
        public string? OrderNumber { get; set; }

        public Guid? UserId { get; set; }

        public byte? Status { get; set; }

        public byte? PaymentStatus { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }
    }
}