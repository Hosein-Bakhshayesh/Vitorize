using Vitorize.Shared.Common;

namespace Vitorize.Application.DTOs.Reviews
{
    public class ProductReviewListResultDto
    {
        public ProductReviewSummaryDto Summary { get; set; } = new();

        public PagedResult<ProductReviewDto> Reviews { get; set; } = new();
    }
}
