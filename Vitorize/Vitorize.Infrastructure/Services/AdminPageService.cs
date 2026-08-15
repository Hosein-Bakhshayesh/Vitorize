using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Admin.Content;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    /// <summary>
    /// FIX-14 Admin CMS page management. Rich content is sanitised on save with the same
    /// <see cref="IHtmlContentSanitizer"/> the product rich-content path uses; the storefront
    /// sanitises again on read as defence in depth.
    /// </summary>
    public class AdminPageService : IAdminPageService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly IHtmlContentSanitizer _htmlSanitizer;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUser;

        public AdminPageService(
            VitorizeDbContext dbContext,
            IHtmlContentSanitizer htmlSanitizer,
            IAuditService auditService,
            ICurrentUserService currentUser)
        {
            _dbContext = dbContext;
            _htmlSanitizer = htmlSanitizer;
            _auditService = auditService;
            _currentUser = currentUser;
        }

        public async Task<List<AdminPageListItemDto>> GetAllAsync()
        {
            return await _dbContext.Pages
                .AsNoTracking()
                .OrderByDescending(x => x.IsSystem)
                .ThenBy(x => x.Title)
                .Select(x => new AdminPageListItemDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Slug = x.Slug,
                    IsSystem = x.IsSystem,
                    IsPublished = x.IsPublished,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<AdminPageDto> GetByIdAsync(Guid id)
        {
            var page = await _dbContext.Pages
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => Map(x))
                .FirstOrDefaultAsync();

            if (page == null)
                throw new NotFoundException("صفحه یافت نشد.");

            return page;
        }

        public async Task<AdminPageDto> CreateAsync(CreatePageRequestDto request)
        {
            var title = NormalizeTitle(request.Title);
            // Client-created pages are always custom: reserved slugs (including the system slugs)
            // are refused so a second conflicting "about" identity can never be created.
            var slug = PageSlugRules.NormalizeForCustomPage(request.Slug);
            await EnsureSlugIsFreeAsync(slug, null);

            var page = new Page
            {
                Id = Guid.NewGuid(),
                Title = title,
                Slug = slug,
                ContentHtml = _htmlSanitizer.Sanitize(request.ContentHtml) ?? string.Empty,
                SeoTitle = NormalizeNullable(request.SeoTitle),
                SeoDescription = NormalizeNullable(request.SeoDescription),
                IsSystem = false,
                IsPublished = request.IsPublished,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.Pages.AddAsync(page);
            await _dbContext.SaveChangesAsync();

            if (page.IsPublished)
                await LogAsync("PagePublished", page);

            return await GetByIdAsync(page.Id);
        }

        public async Task<AdminPageDto> UpdateAsync(Guid id, UpdatePageRequestDto request)
        {
            var page = await _dbContext.Pages.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("صفحه یافت نشد.");

            var title = NormalizeTitle(request.Title);

            if (!page.IsSystem)
            {
                var slug = PageSlugRules.NormalizeForCustomPage(request.Slug);
                if (!string.Equals(slug, page.Slug, StringComparison.OrdinalIgnoreCase))
                {
                    await EnsureSlugIsFreeAsync(slug, page.Id);
                    page.Slug = slug;
                }
            }
            // System pages keep their seeded slug: it is the canonical route identity.

            var wasPublished = page.IsPublished;
            page.Title = title;
            page.ContentHtml = _htmlSanitizer.Sanitize(request.ContentHtml) ?? string.Empty;
            page.SeoTitle = NormalizeNullable(request.SeoTitle);
            page.SeoDescription = NormalizeNullable(request.SeoDescription);
            page.IsPublished = request.IsPublished;
            page.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            if (wasPublished != page.IsPublished)
                await LogAsync(page.IsPublished ? "PagePublished" : "PageUnpublished", page);

            return await GetByIdAsync(page.Id);
        }

        public async Task<AdminPageDto> SetPublishedAsync(Guid id, bool isPublished)
        {
            var page = await _dbContext.Pages.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("صفحه یافت نشد.");

            if (page.IsPublished != isPublished)
            {
                page.IsPublished = isPublished;
                page.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                await LogAsync(isPublished ? "PagePublished" : "PageUnpublished", page);
            }

            return await GetByIdAsync(page.Id);
        }

        public async Task DeleteAsync(Guid id)
        {
            var page = await _dbContext.Pages.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("صفحه یافت نشد.");

            // Enforced on the server, not merely by hiding the Admin button.
            if (page.IsSystem)
                throw new BusinessException("صفحه‌های سیستمی قابل حذف نیستند. برای پنهان کردن، آن را از انتشار خارج کنید.");

            _dbContext.Pages.Remove(page);
            await _dbContext.SaveChangesAsync();
            await LogAsync("PageDeleted", page);
        }

        private async Task EnsureSlugIsFreeAsync(string slug, Guid? excludingId)
        {
            // Friendly validation ahead of the UX_Pages_Slug unique index. The column collation is
            // case-insensitive, so EF.Functions-free comparison here matches the database behaviour.
            var taken = await _dbContext.Pages
                .AsNoTracking()
                .AnyAsync(x => x.Slug == slug && (excludingId == null || x.Id != excludingId));

            if (taken)
                throw new BusinessException($"صفحه‌ای با نشانی «{slug}» از قبل وجود دارد.");
        }

        /// <summary>Audit records the action and identity only; page HTML is never written to the log.</summary>
        private Task LogAsync(string actionType, Page page) =>
            _auditService.LogAsync(
                _currentUser.UserId,
                actionType,
                nameof(Page),
                page.Id.ToString(),
                $"slug={page.Slug}; published={page.IsPublished}",
                _currentUser.IpAddress,
                _currentUser.UserAgent);

        private static AdminPageDto Map(Page x) => new()
        {
            Id = x.Id,
            Title = x.Title,
            Slug = x.Slug,
            ContentHtml = x.ContentHtml,
            SeoTitle = x.SeoTitle,
            SeoDescription = x.SeoDescription,
            IsSystem = x.IsSystem,
            IsPublished = x.IsPublished,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        };

        private static string NormalizeTitle(string? value)
        {
            var title = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
                throw new BusinessException("عنوان صفحه الزامی است.");
            if (title.Length > 250)
                throw new BusinessException("عنوان صفحه نمی‌تواند بیشتر از ۲۵۰ نویسه باشد.");
            return title;
        }

        private static string? NormalizeNullable(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
