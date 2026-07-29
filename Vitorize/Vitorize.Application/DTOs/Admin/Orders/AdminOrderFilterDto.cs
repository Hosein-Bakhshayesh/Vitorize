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

        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int? PageNumber { get; set; }
        public int PageSize { get; set; } = 25;
        public string? SortBy { get; set; }
        public string? SortDirection { get; set; }
    }
}
