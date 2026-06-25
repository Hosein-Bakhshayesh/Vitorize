namespace Vitorize.Web.Models.Admin.Reports
{
    public class ReportDateRangeModel
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public class SalesReportModel
    {
        public decimal TotalSales { get; set; }
        public int TotalOrders { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int CompletedOrders { get; set; }
        public int CancelledOrders { get; set; }
        public List<ReportRowModel> Rows { get; set; } = new();
        public List<ReportRowModel> DailySales { get; set; } = new();
    }

    public class PaymentsReportModel
    {
        public decimal TotalAmount { get; set; }
        public int TotalPayments { get; set; }
        public int PaidCount { get; set; }
        public int FailedCount { get; set; }
        public List<ReportRowModel> Rows { get; set; } = new();
    }

    public class WalletReportModel
    {
        public decimal TotalCredit { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal CurrentBalance { get; set; }
        public int TransactionCount { get; set; }
    }

    public class CouponsReportModel
    {
        public int TotalCoupons { get; set; }
        public int ActiveCoupons { get; set; }
        public int TotalUsage { get; set; }
        public decimal TotalDiscount { get; set; }
    }

    public class GiftCodesReportModel
    {
        public int TotalCodes { get; set; }
        public int AvailableCodes { get; set; }
        public int SoldCodes { get; set; }
        public int DeliveredCodes { get; set; }
        public int DisabledCodes { get; set; }
    }

    public class UsersReportModel
    {
        public int TotalUsers { get; set; }
        public int NewUsers { get; set; }
        public int VerifiedUsers { get; set; }
        public int ActiveUsers { get; set; }
    }

    public class ReportRowModel
    {
        public string Label { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
        public decimal Amount { get; set; }
        public int Count { get; set; }
    }
}
