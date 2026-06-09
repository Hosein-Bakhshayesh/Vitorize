namespace Vitorize.Application.DTOs.Admin.Dashboard
{
    public class DashboardSummaryDto
    {
        public int TotalUsers { get; set; }

        public int NewUsersToday { get; set; }

        public int TotalOrders { get; set; }

        public int OrdersToday { get; set; }

        public decimal RevenueToday { get; set; }

        public decimal RevenueThisMonth { get; set; }

        public int PendingTickets { get; set; }

        public int PendingVerifications { get; set; }

        public int UnreadNotifications { get; set; }

        public int AvailableGiftCodes { get; set; }
    }
}