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
    /// Admin blog management. Article HTML is sanitised on save with the same
    /// <see cref="IHtmlContentSanitizer"/> the product rich-content and CMS page paths use; the
    /// storefront sanitises again on read as defence in depth.
    ///
    /// Publication is explicit: a new article is a draft until an administrator publishes it, and
    /// <c>PublishedAt</c> is stamped by this service on the first transition to published so a client
    /// cannot backdate an article. Unpublishing keeps the original date, so republishing does not
    /// rewrite history.
    /// </summary>
    public class AdminBlogService : IAdminBlogService
    {
        private const int MaximumTitleLength = 200;
        private const int MaximumSummaryLength = 500;

        private readonly VitorizeDbContext _dbContext;
        private readonly IHtmlContentSanitizer _htmlSanitizer;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUser;

        public AdminBlogService(
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

        public async Task<List<AdminBlogPostListItemDto>> GetAllAsync() =>
            await _dbContext.BlogPosts
                .AsNoTracking()
                .OrderByDescending(x => x.PublishedAt ?? x.CreatedAt)
                .ThenBy(x => x.Title)
                .Select(x => new AdminBlogPostListItemDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Slug = x.Slug,
                    IsPublished = x.IsPublished,
                    PublishedAt = x.PublishedAt,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync();

        public async Task<AdminBlogPostDto> GetByIdAsync(Guid id)
        {
            var post = await _dbContext.BlogPosts
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new AdminBlogPostDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Slug = x.Slug,
                    Summary = x.Summary,
                    ContentHtml = x.ContentHtml,
                    CoverImagePath = x.CoverImagePath,
                    CoverImageAltText = x.CoverImageAltText,
                    SeoTitle = x.SeoTitle,
                    SeoDescription = x.SeoDescription,
                    FocusKeyword = x.FocusKeyword,
                    IsPublished = x.IsPublished,
                    PublishedAt = x.PublishedAt,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .FirstOrDefaultAsync();

            return post ?? throw new NotFoundException("مطلب وبلاگ یافت نشد.");
        }

        public async Task<AdminBlogPostDto> CreateAsync(CreateBlogPostRequestDto request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var title = NormalizeTitle(request.Title);
            var slug = NormalizeSlug(request.Slug);
            await EnsureSlugIsFreeAsync(slug, null);

            var now = DateTime.UtcNow;
            var post = new BlogPost
            {
                Id = Guid.NewGuid(),
                Title = title,
                Slug = slug,
                Summary = NormalizeSummary(request.Summary),
                ContentHtml = SanitizeBody(request.ContentHtml),
                CoverImagePath = Trimmed(request.CoverImagePath),
                CoverImageAltText = Trimmed(request.CoverImageAltText),
                SeoTitle = Trimmed(request.SeoTitle),
                SeoDescription = Trimmed(request.SeoDescription),
                FocusKeyword = Trimmed(request.FocusKeyword),
                IsPublished = request.IsPublished,
                PublishedAt = request.IsPublished ? now : null,
                CreatedAt = now
            };

            await _dbContext.BlogPosts.AddAsync(post);
            await _dbContext.SaveChangesAsync();
            await LogAsync(post, request.IsPublished ? "BlogPostCreatedPublished" : "BlogPostCreatedDraft");

            return await GetByIdAsync(post.Id);
        }

        public async Task<AdminBlogPostDto> UpdateAsync(Guid id, UpdateBlogPostRequestDto request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var post = await _dbContext.BlogPosts.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("مطلب وبلاگ یافت نشد.");

            var slug = NormalizeSlug(request.Slug);
            await EnsureSlugIsFreeAsync(slug, id);

            post.Title = NormalizeTitle(request.Title);
            post.Slug = slug;
            post.Summary = NormalizeSummary(request.Summary);
            post.ContentHtml = SanitizeBody(request.ContentHtml);
            post.CoverImagePath = Trimmed(request.CoverImagePath);
            post.CoverImageAltText = Trimmed(request.CoverImageAltText);
            post.SeoTitle = Trimmed(request.SeoTitle);
            post.SeoDescription = Trimmed(request.SeoDescription);
            post.FocusKeyword = Trimmed(request.FocusKeyword);
            ApplyPublicationState(post, request.IsPublished);
            post.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            await LogAsync(post, "BlogPostUpdated");

            return await GetByIdAsync(post.Id);
        }

        public async Task<AdminBlogPostDto> SetPublishedAsync(Guid id, bool isPublished)
        {
            var post = await _dbContext.BlogPosts.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("مطلب وبلاگ یافت نشد.");

            if (isPublished && string.IsNullOrWhiteSpace(post.ContentHtml))
                throw new BusinessException("برای انتشار، متن مطلب نمی‌تواند خالی باشد.");

            ApplyPublicationState(post, isPublished);
            post.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            await LogAsync(post, isPublished ? "BlogPostPublished" : "BlogPostUnpublished");

            return await GetByIdAsync(post.Id);
        }

        public async Task DeleteAsync(Guid id)
        {
            var post = await _dbContext.BlogPosts.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("مطلب وبلاگ یافت نشد.");

            _dbContext.BlogPosts.Remove(post);
            await _dbContext.SaveChangesAsync();
            await LogAsync(post, "BlogPostDeleted");
        }

        /// <summary>
        /// Publication transitions. The first publish stamps the date; later unpublish/republish
        /// cycles keep the original so the article's public history is not rewritten.
        /// </summary>
        private static void ApplyPublicationState(BlogPost post, bool isPublished)
        {
            if (isPublished && post.PublishedAt is null)
                post.PublishedAt = DateTime.UtcNow;

            post.IsPublished = isPublished;
        }

        private async Task EnsureSlugIsFreeAsync(string slug, Guid? excludingId)
        {
            var taken = await _dbContext.BlogPosts
                .AnyAsync(x => x.Slug == slug && (excludingId == null || x.Id != excludingId));

            if (taken)
                throw new BusinessException($"نامک «{slug}» قبلاً برای مطلب دیگری استفاده شده است.");
        }

        private static string NormalizeTitle(string? value)
        {
            var title = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
                throw new BusinessException("عنوان مطلب الزامی است.");
            if (title.Length > MaximumTitleLength)
                throw new BusinessException($"عنوان مطلب نمی‌تواند بیشتر از {MaximumTitleLength} نویسه باشد.");
            return title;
        }

        /// <summary>
        /// Blog slugs reuse the CMS normalisation so URLs behave identically across content types,
        /// including the reserved-slug guard that stops a post shadowing a system page route.
        /// </summary>
        private static string NormalizeSlug(string? value)
        {
            var slug = PageSlugRules.NormalizeForCustomPage(value);
            if (string.IsNullOrWhiteSpace(slug))
                throw new BusinessException("نامک مطلب الزامی است.");
            return slug;
        }

        private static string? NormalizeSummary(string? value)
        {
            var summary = Trimmed(value);
            if (summary is not null && summary.Length > MaximumSummaryLength)
                throw new BusinessException($"خلاصه نمی‌تواند بیشتر از {MaximumSummaryLength} نویسه باشد.");
            return summary;
        }

        /// <summary>
        /// Sanitises the article body. The sanitizer returns null for empty input while the column is
        /// NOT NULL, so an empty draft must persist as an empty string rather than a null.
        /// </summary>
        private string SanitizeBody(string? html) =>
            _htmlSanitizer.Sanitize(html ?? string.Empty) ?? string.Empty;

        private static string? Trimmed(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        /// <summary>Records the action without dumping the article body into the audit trail.</summary>
        private Task LogAsync(BlogPost post, string action) =>
            _auditService.LogAsync(
                _currentUser.UserId ?? Guid.Empty,
                action,
                nameof(BlogPost),
                post.Id.ToString(),
                $"slug:{post.Slug}; title:{post.Title}; published:{post.IsPublished}",
                _currentUser.IpAddress,
                _currentUser.UserAgent);
    }
}
