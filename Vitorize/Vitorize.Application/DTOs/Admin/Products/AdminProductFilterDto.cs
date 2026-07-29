namespace Vitorize.Application.DTOs.Admin.Products;

public sealed class AdminProductFilterDto
{
    public string? Search { get; set; }
    public byte? ProductType { get; set; }
    public Guid? CategoryId { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsFeatured { get; set; }
    public string? StockState { get; set; }
    public int Page { get; set; } = 1;
    public int? PageNumber { get; set; }
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; }
}
