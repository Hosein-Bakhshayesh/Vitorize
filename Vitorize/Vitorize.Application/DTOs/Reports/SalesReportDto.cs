namespace Vitorize.Application.DTOs.Admin.Reports
{
    public class SalesReportDto
    {
        public decimal TotalRevenue { get; set; }

        public int TotalOrders { get; set; }

        public int PaidOrders { get; set; }

        public decimal AverageOrderValue { get; set; }

        public List<SalesReportDailyDto> DailySales { get; set; } = new();

        public List<SalesReportProductDto> TopProducts { get; set; } = new();
    }

    public class SalesReportDailyDto
    {
        public DateTime Date { get; set; }

        public int OrdersCount { get; set; }

        public decimal Revenue { get; set; }
    }

    public class SalesReportProductDto
    {
        public Guid ProductId { get; set; }

        public string ProductTitle { get; set; } = null!;

        public int QuantitySold { get; set; }

        public decimal Revenue { get; set; }
    }
}