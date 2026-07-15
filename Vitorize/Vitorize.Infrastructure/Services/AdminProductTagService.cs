using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Products;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services;

public sealed class AdminProductTagService(VitorizeDbContext db) : IAdminProductTagService
{
    public async Task<List<AdminProductTagDto>> GetAllAsync()
    {
        var tags = await db.ProductTags.AsNoTracking().Include(x => x.Products)
            .OrderByDescending(x => x.IsActive).ThenBy(x => x.Title).ToListAsync();
        return tags.Select(Map).ToList();
    }

    public async Task<AdminProductTagDto> CreateAsync(SaveProductTagRequestDto request)
    {
        var slug = NormalizeSlug(request.Slug);
        await EnsureUniqueAsync(request.Title, slug, null);
        var tag = new ProductTag
        {
            Id = Guid.NewGuid(), Title = request.Title.Trim(), Slug = slug,
            Aliases = NormalizeAliases(request.Aliases), IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };
        db.ProductTags.Add(tag);
        await db.SaveChangesAsync();
        return Map(tag);
    }

    public async Task<AdminProductTagDto> UpdateAsync(Guid id, SaveProductTagRequestDto request)
    {
        var tag = await db.ProductTags.Include(x => x.Products).FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new NotFoundException("برچسب یافت نشد.");
        var slug = NormalizeSlug(request.Slug);
        await EnsureUniqueAsync(request.Title, slug, id);
        tag.Title = request.Title.Trim();
        tag.Slug = slug;
        tag.Aliases = NormalizeAliases(request.Aliases);
        tag.IsActive = request.IsActive;
        tag.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Map(tag);
    }

    public async Task DeleteAsync(Guid id)
    {
        var tag = await db.ProductTags.Include(x => x.Products).FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new NotFoundException("برچسب یافت نشد.");
        if (tag.Products.Count != 0)
            throw new BusinessException("برچسب استفاده‌شده را غیرفعال کنید؛ حذف آن مجاز نیست.");
        db.ProductTags.Remove(tag);
        await db.SaveChangesAsync();
    }

    private async Task EnsureUniqueAsync(string title, string slug, Guid? id)
    {
        var normalizedTitle = title.Trim();
        if (await db.ProductTags.AnyAsync(x => x.Id != id && (x.Title == normalizedTitle || x.Slug == slug)))
            throw new BusinessException("عنوان یا اسلاگ این برچسب قبلاً ثبت شده است.");
    }

    private static string NormalizeSlug(string value) => value.Trim().ToLowerInvariant();
    private static string? NormalizeAliases(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var items = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(20);
        return string.Join(',', items);
    }

    private static AdminProductTagDto Map(ProductTag x) => new()
    {
        Id = x.Id, Title = x.Title, Slug = x.Slug, Aliases = x.Aliases,
        IsActive = x.IsActive, ProductCount = x.Products.Count,
        CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt
    };
}
