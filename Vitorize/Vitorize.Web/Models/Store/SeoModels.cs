namespace Vitorize.Web.Models.Store;

public sealed class SeoSitemapItemModel
{
    public string Path { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
}

public sealed class SeoSitemapPageModel
{
    public string Kind { get; set; } = string.Empty;
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<SeoSitemapItemModel> Items { get; set; } = new();
}

public sealed class LegacyRedirectModel
{
    public string SourcePath { get; set; } = string.Empty;
    public string? DestinationPath { get; set; }
    public short StatusCode { get; set; }
}
