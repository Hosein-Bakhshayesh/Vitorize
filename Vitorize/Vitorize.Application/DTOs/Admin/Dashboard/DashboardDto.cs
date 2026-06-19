namespace Vitorize.Application.DTOs.Admin.Dashboard
{
    public class DashboardDto
    {
        public DashboardSummaryDto Summary { get; set; } = new();

        public List<TopProductDto> TopProducts { get; set; } = new();

        public List<DashboardChartPointDto> SalesLast7Days { get; set; } = new();

        public List<DashboardChartPointDto> OrdersLast7Days { get; set; } = new();

        public List<DashboardStatusCountDto> OrderStatusCounts { get; set; } = new();

        public List<DashboardStatusCountDto> PaymentStatusCounts { get; set; } = new();

        public List<DashboardStatusCountDto> GiftCodeStatusCounts { get; set; } = new();
    }

    public class DashboardSummaryDto
    {
        public int TotalUsers { get; set; }

        public int NewUsersToday { get; set; }

        public int TotalOrders { get; set; }

        public int OrdersToday { get; set; }

        public decimal RevenueToday { get; set; }

        public decimal RevenueThisMonth { get; set; }

        public decimal TotalWalletBalance { get; set; }

        public int PendingTickets { get; set; }

        public int PendingVerifications { get; set; }

        public int UnreadNotifications { get; set; }

        public int AvailableGiftCodes { get; set; }

        public int ReservedGiftCodes { get; set; }

        public int SoldGiftCodes { get; set; }
    }

    public class TopProductDto
    {
        public Guid ProductId { get; set; }

        public string ProductTitle { get; set; } = null!;

        public int TotalSold { get; set; }

        public decimal Revenue { get; set; }
    }

    public class DashboardChartPointDto
    {
        public DateTime Date { get; set; }

        public decimal Value { get; set; }
    }

    public class DashboardStatusCountDto
    {
        public byte Status { get; set; }

        public int Count { get; set; }
    }
}