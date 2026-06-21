namespace Vitorize.Application.DTOs.Admin.Reports
{
    public class PaymentsReportDto
    {
        public int TotalPayments { get; set; }

        public int PaidPayments { get; set; }

        public int FailedPayments { get; set; }

        public decimal PaidAmount { get; set; }

        public List<PaymentGatewayReportDto> ByGateway { get; set; } = new();

        public List<PaymentStatusReportDto> ByStatus { get; set; } = new();
    }

    public class PaymentGatewayReportDto
    {
        public string Gateway { get; set; } = null!;

        public int Count { get; set; }

        public decimal Amount { get; set; }
    }

    public class PaymentStatusReportDto
    {
        public byte Status { get; set; }

        public int Count { get; set; }

        public decimal Amount { get; set; }
    }
}