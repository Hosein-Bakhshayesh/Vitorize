namespace Vitorize.Application.DTOs.Admin.Products;

/// <summary>
/// A guarded, all-or-nothing catalogue operation requested from the admin product grid.
/// Operation is intentionally a string so the public request stays backwards-compatible and
/// unknown actions can be rejected explicitly by the application service.
/// </summary>
public sealed class BulkProductUpdateRequestDto
{
    public List<Guid> Ids { get; set; } = [];
    public string Operation { get; set; } = string.Empty;
    public decimal? Value { get; set; }
}

public sealed class BulkProductUpdateResultDto
{
    public int UpdatedProductCount { get; set; }
    public int UpdatedVariantCount { get; set; }
    public int SkippedGiftCodeProductCount { get; set; }
}
