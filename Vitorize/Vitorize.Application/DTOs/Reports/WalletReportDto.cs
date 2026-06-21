namespace Vitorize.Application.DTOs.Admin.Reports
{
    public class WalletReportDto
    {
        public decimal TotalCredit { get; set; }

        public decimal TotalDebit { get; set; }

        public int TransactionsCount { get; set; }

        public List<WalletTransactionTypeReportDto> ByType { get; set; } = new();
    }

    public class WalletTransactionTypeReportDto
    {
        public byte Type { get; set; }

        public int Count { get; set; }

        public decimal Amount { get; set; }
    }
}