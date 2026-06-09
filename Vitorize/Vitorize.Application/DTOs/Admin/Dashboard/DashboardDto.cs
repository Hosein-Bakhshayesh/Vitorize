namespace Vitorize.Application.DTOs.Admin.Dashboard
{
    public class DashboardDto
    {
        public DashboardSummaryDto Summary { get; set; }
            = new();

        public List<TopProductDto> TopProducts { get; set; }
            = new();
    }
}