namespace Vitorize.Application.DTOs.Admin.GiftCodes
{
    public class GiftCodeBatchDto
    {
        public Guid Id { get; set; }

        public string BatchTitle { get; set; } = string.Empty;

        public string? SourceName { get; set; }

        public int TotalCodes { get; set; }

        public int AvailableCodes { get; set; }

        public DateTime ImportedAt { get; set; }
    }
}