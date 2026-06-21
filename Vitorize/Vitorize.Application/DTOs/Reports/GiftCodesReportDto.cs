namespace Vitorize.Application.DTOs.Admin.Reports
{
    public class GiftCodesReportDto
    {
        public int TotalCodes { get; set; }

        public List<GiftCodeStatusReportDto> ByStatus { get; set; } = new();

        public List<GiftCodeProductReportDto> ByProduct { get; set; } = new();
    }

    public class GiftCodeStatusReportDto
    {
        public byte Status { get; set; }

        public int Count { get; set; }
    }

    public class GiftCodeProductReportDto
    {
        public Guid ProductId { get; set; }

        public string ProductTitle { get; set; } = null!;

        public int TotalCodes { get; set; }

        public int AvailableCodes { get; set; }

        public int SoldCodes { get; set; }
    }
}