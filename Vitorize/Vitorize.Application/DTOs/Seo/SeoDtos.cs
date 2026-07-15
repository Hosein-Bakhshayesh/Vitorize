namespace Vitorize.Application.DTOs.Seo;

public sealed class SitemapItemDto
{
    public string Path { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
}

public sealed class SitemapPageDto
{
    public string Kind { get; set; } = string.Empty;
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<SitemapItemDto> Items { get; set; } = new();
}

public sealed class LegacyRedirectDto
{
    public string SourcePath { get; set; } = string.Empty;
    public string? DestinationPath { get; set; }
    public short StatusCode { get; set; }
}
