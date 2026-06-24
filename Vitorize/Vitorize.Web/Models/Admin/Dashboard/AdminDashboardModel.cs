namespace Vitorize.Web.Models.Admin.Dashboard
{
    public class AdminDashboardModel
    {
        public DashboardSummaryModel Summary { get; set; } = new();

        public List<TopProductModel> TopProducts { get; set; } = new();

        public List<DashboardChartPointModel> SalesLast7Days { get; set; } = new();

        public List<DashboardChartPointModel> OrdersLast7Days { get; set; } = new();

        public List<DashboardStatusCountModel> OrderStatusCounts { get; set; } = new();

        public List<DashboardStatusCountModel> PaymentStatusCounts { get; set; } = new();

        public List<DashboardStatusCountModel> GiftCodeStatusCounts { get; set; } = new();
    }
}