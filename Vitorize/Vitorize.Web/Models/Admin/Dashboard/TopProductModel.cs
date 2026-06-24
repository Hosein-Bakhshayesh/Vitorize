namespace Vitorize.Web.Models.Admin.Dashboard
{
    public class TopProductModel
    {
        public Guid ProductId { get; set; }

        public string ProductTitle { get; set; } = string.Empty;

        public int TotalSold { get; set; }

        public decimal Revenue { get; set; }
    }
}