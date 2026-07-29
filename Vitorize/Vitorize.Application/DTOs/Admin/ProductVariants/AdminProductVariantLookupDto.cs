namespace Vitorize.Application.DTOs.Admin.ProductVariants;

/// <summary>
/// Bounded projection used by selectors. It deliberately excludes pricing,
/// inventory, and other operational variant data.
/// </summary>
public sealed class AdminProductVariantLookupDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Sku { get; set; }
}
