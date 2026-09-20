namespace Vitorize.Web.Models.Admin.Reports
{
    public class ReportDateRangeModel
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public class SalesReportModel
    {
        public decimal TotalRevenue { get; set; }
        public decimal TotalVatAmount { get; set; }
        public int TotalOrders { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int PaidOrders { get; set; }
        public List<SalesReportDailyModel> DailySales { get; set; } = new();
        public List<SalesReportProductModel> TopProducts { get; set; } = new();
    }

    public class PaymentsReportModel
    {
        public decimal PaidAmount { get; set; }
        public int TotalPayments { get; set; }
        public int PaidPayments { get; set; }
        public int FailedPayments { get; set; }
        public List<PaymentGatewayReportModel> ByGateway { get; set; } = new();
        public List<PaymentStatusReportModel> ByStatus { get; set; } = new();
    }

    public class WalletReportModel
    {
        public decimal TotalCredit { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal CurrentBalance { get; set; }
        public int TransactionsCount { get; set; }
        public List<WalletTransactionTypeReportModel> ByType { get; set; } = new();
    }

    public class CouponsReportModel
    {
        public int TotalCoupons { get; set; }
        public int ActiveCoupons { get; set; }
        public int TotalUsages { get; set; }
        public decimal TotalDiscount { get; set; }
        public List<CouponUsageReportModel> TopCoupons { get; set; } = new();
    }

    public class GiftCodesReportModel
    {
        public int TotalCodes { get; set; }
        public List<GiftCodeStatusReportModel> ByStatus { get; set; } = new();
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

    public class SalesReportDailyModel { public DateTime Date { get; set; } public int OrdersCount { get; set; } public decimal Revenue { get; set; } }
    public class SalesReportProductModel { public Guid ProductId { get; set; } public string ProductTitle { get; set; } = string.Empty; public int QuantitySold { get; set; } public decimal Revenue { get; set; } }
    public class PaymentGatewayReportModel { public string Gateway { get; set; } = string.Empty; public int Count { get; set; } public decimal Amount { get; set; } }
    public class PaymentStatusReportModel { public byte Status { get; set; } public int Count { get; set; } public decimal Amount { get; set; } }
    public class WalletTransactionTypeReportModel { public byte Type { get; set; } public int Count { get; set; } public decimal Amount { get; set; } }
    public class CouponUsageReportModel { public Guid CouponId { get; set; } public string Code { get; set; } = string.Empty; public string Title { get; set; } = string.Empty; public int UsageCount { get; set; } }
    public class GiftCodeStatusReportModel { public byte Status { get; set; } public int Count { get; set; } }
}
