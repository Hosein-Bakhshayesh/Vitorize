namespace Vitorize.Application.DTOs.Admin.Products;

public sealed class AdminProductLookupDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}
