namespace Vitorize.Web.Models.Admin.Dashboard
{
    public class AdminDashboardModel
    {
        public DashboardSummaryModel Summary { get; set; } = new();
        public List<DashboardTopProductModel> TopProducts { get; set; } = new();
        public List<DashboardChartPointModel> SalesLast7Days { get; set; } = new();
        public List<DashboardChartPointModel> OrdersLast7Days { get; set; } = new();
        public List<DashboardStatusCountModel> OrderStatusCounts { get; set; } = new();
        public List<DashboardStatusCountModel> PaymentStatusCounts { get; set; } = new();
        public List<DashboardStatusCountModel> GiftCodeStatusCounts { get; set; } = new();
    }
    public class DashboardSummaryModel
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
    public class DashboardTopProductModel
    {
        public Guid ProductId { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public int TotalSold { get; set; }
        public decimal Revenue { get; set; }
    }
    public class DashboardChartPointModel { public DateTime Date { get; set; } public decimal Value { get; set; } }
    public class DashboardStatusCountModel { public byte Status { get; set; } public int Count { get; set; } }
}
