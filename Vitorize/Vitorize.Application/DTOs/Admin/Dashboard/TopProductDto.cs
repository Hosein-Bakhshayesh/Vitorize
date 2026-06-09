namespace Vitorize.Application.DTOs.Admin.Dashboard
{
    public class TopProductDto
    {
        public Guid ProductId { get; set; }

        public string ProductTitle { get; set; } = null!;

        public int TotalSold { get; set; }

        public decimal Revenue { get; set; }
    }
}