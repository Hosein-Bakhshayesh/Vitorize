namespace Vitorize.Application.DTOs.Reviews;

public class ProductReviewEligibilityDto
{
    public bool CanCreateReview { get; set; }
    public bool IsBuyer { get; set; }
    public bool HasExistingReview { get; set; }
    public string? Message { get; set; }
}
