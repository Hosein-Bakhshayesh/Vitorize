namespace Vitorize.Application.DTOs.Admin.Coupons;

public sealed class AdminCouponFilterDto
{
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public string? State { get; set; }
    public int Page { get; set; } = 1;
    public int? PageNumber { get; set; }
    public int PageSize { get; set; } = 25;
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}
