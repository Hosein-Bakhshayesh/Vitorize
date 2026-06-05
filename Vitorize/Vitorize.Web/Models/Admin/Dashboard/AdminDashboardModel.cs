namespace Vitorize.Web.Models.Admin.Dashboard
{
    public class AdminDashboardModel
    {
        public int TotalUsers { get; set; }
        public int TotalProducts { get; set; }
        public int TotalOrders { get; set; }
        public int PendingOrders { get; set; }
        public int CompletedOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public int ActiveCoupons { get; set; }
        public int AvailableGiftCodes { get; set; }

        public List<DashboardRecentOrderModel> RecentOrders { get; set; } = new();
    }

    public class DashboardRecentOrderModel
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal FinalAmount { get; set; }
        public byte Status { get; set; }
        public byte PaymentStatus { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}