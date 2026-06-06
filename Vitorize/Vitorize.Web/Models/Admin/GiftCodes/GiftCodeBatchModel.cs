namespace Vitorize.Web.Models.Admin.GiftCodes
{
    public class GiftCodeBatchModel
    {
        public Guid Id { get; set; }

        public string BatchTitle { get; set; } = string.Empty;

        public string? SourceName { get; set; }

        public int TotalCodes { get; set; }

        public int AvailableCodes { get; set; }

        public DateTime ImportedAt { get; set; }

        public int SoldCodes => TotalCodes - AvailableCodes;
    }
}