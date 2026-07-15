using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Seo;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services;

public sealed class SeoService(VitorizeDbContext db) : ISeoService
{
    public async Task<SitemapPageDto> GetSitemapAsync(string kind, int page, int pageSize)
    {
        kind = kind.Trim().ToLowerInvariant();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50_000);

        IQueryable<SitemapItemDto> query = kind switch
        {
            "products" => db.Products.AsNoTracking()
                .Where(x => x.IsActive && !x.IsDeleted && x.Category.IsActive && !x.Category.IsDeleted)
                .Select(x => new SitemapItemDto { Path = "/product/" + x.Slug, LastModified = x.UpdatedAt ?? x.CreatedAt }),
            "categories" => db.Categories.AsNoTracking()
                .Where(x => x.IsActive && !x.IsDeleted)
                .Select(x => new SitemapItemDto { Path = "/category/" + x.Slug, LastModified = x.UpdatedAt ?? x.CreatedAt }),
            "brands" => db.Brands.AsNoTracking().Where(x => x.IsActive)
                .Select(x => new SitemapItemDto { Path = "/brand/" + x.Slug, LastModified = x.UpdatedAt ?? x.CreatedAt }),
            "blog" => db.BlogPosts.AsNoTracking().Where(x => x.IsPublished)
                .Select(x => new SitemapItemDto { Path = "/blog/" + x.Slug, LastModified = x.UpdatedAt ?? x.PublishedAt ?? x.CreatedAt }),
            "pages" => db.Pages.AsNoTracking().Where(x => x.IsPublished)
                .Select(x => new SitemapItemDto { Path = "/page/" + x.Slug, LastModified = x.UpdatedAt ?? x.CreatedAt }),
            _ => throw new BusinessException("نوع نقشه سایت معتبر نیست.")
        };

        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.Path).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new SitemapPageDto { Kind = kind, Page = page, PageSize = pageSize, TotalCount = total, Items = items };
    }

    public Task<LegacyRedirectDto?> ResolveRedirectAsync(string sourcePath)
    {
        var normalized = NormalizePath(sourcePath);
        return db.LegacyRedirects.AsNoTracking()
            .Where(x => x.IsActive && x.SourcePath == normalized)
            .Select(x => new LegacyRedirectDto
            {
                SourcePath = x.SourcePath,
                DestinationPath = x.DestinationPath,
                StatusCode = x.StatusCode
            }).FirstOrDefaultAsync();
    }

    private static string NormalizePath(string path)
    {
        var normalized = path.Split('?', '#')[0].Trim();
        if (!normalized.StartsWith('/')) normalized = "/" + normalized;
        return normalized.Length > 1 ? normalized.TrimEnd('/') : normalized;
    }
}
