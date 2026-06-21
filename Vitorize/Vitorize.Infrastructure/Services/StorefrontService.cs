using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Storefront;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class StorefrontService : IStorefrontService
    {
        private readonly VitorizeDbContext _dbContext;

        public StorefrontService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<HomeDto> GetHomeAsync()
        {
            var now = DateTime.UtcNow;

            var banners = await _dbContext.Banners
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    (x.StartsAt == null || x.StartsAt <= now) &&
                    (x.EndsAt == null || x.EndsAt >= now))
                .OrderBy(x => x.SortOrder)
                .Select(x => new BannerDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    ImagePath = x.ImagePath,
                    MobileImagePath = x.MobileImagePath,
                    LinkUrl = x.LinkUrl,
                    Position = x.Position,
                    SortOrder = x.SortOrder
                })
                .ToListAsync();

            var categories = await _dbContext.Categories
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    !x.IsDeleted &&
                    x.ParentId == null)
                .OrderBy(x => x.SortOrder)
                .Take(12)
                .Select(x => new StorefrontCategoryDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Slug = x.Slug,
                    Icon = x.Icon,
                    ImagePath = x.ImagePath
                })
                .ToListAsync();

            var brands = await _dbContext.Brands
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Title)
                .Take(12)
                .Select(x => new StorefrontBrandDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Slug = x.Slug,
                    ImagePath = x.ImagePath
                })
                .ToListAsync();

            var featuredProducts = await _dbContext.Products
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    !x.IsDeleted &&
                    x.IsFeatured)
                .OrderBy(x => x.SortOrder)
                .ThenByDescending(x => x.CreatedAt)
                .Take(12)
                .Select(x => new StorefrontProductDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Slug = x.Slug,
                    ThumbnailImagePath = x.ThumbnailImagePath,
                    BasePrice = x.BasePrice,
                    DiscountPrice = x.DiscountPrice,
                    IsFeatured = x.IsFeatured,
                    RequiresVerification = x.RequiresVerification
                })
                .ToListAsync();

            var latestBlogPosts = await _dbContext.BlogPosts
                .AsNoTracking()
                .Where(x => x.IsPublished)
                .OrderByDescending(x => x.PublishedAt ?? x.CreatedAt)
                .Take(6)
                .Select(x => new StorefrontBlogPostDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Slug = x.Slug,
                    Summary = x.Summary,
                    CoverImagePath = x.CoverImagePath,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            var faqs = await _dbContext.Faqs
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .Take(6)
                .Select(x => new FaqDto
                {
                    Id = x.Id,
                    Question = x.Question,
                    Answer = x.Answer,
                    SortOrder = x.SortOrder
                })
                .ToListAsync();

            return new HomeDto
            {
                Banners = banners,
                Categories = categories,
                Brands = brands,
                FeaturedProducts = featuredProducts,
                LatestBlogPosts = latestBlogPosts,
                Faqs = faqs
            };
        }

        public async Task<List<FaqDto>> GetFaqsAsync()
        {
            return await _dbContext.Faqs
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .Select(x => new FaqDto
                {
                    Id = x.Id,
                    Question = x.Question,
                    Answer = x.Answer,
                    SortOrder = x.SortOrder
                })
                .ToListAsync();
        }

        public async Task<PageDto> GetPageBySlugAsync(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                throw new BusinessException("آدرس صفحه معتبر نیست.");

            var normalizedSlug = slug.Trim();

            var page = await _dbContext.Pages
                .AsNoTracking()
                .Where(x =>
                    x.IsPublished &&
                    x.Slug == normalizedSlug)
                .Select(x => new PageDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Slug = x.Slug,
                    ContentHtml = x.ContentHtml,
                    SeoTitle = x.SeoTitle,
                    SeoDescription = x.SeoDescription
                })
                .FirstOrDefaultAsync();

            if (page == null)
                throw new NotFoundException("صفحه یافت نشد.");

            return page;
        }

        public async Task<List<StorefrontBlogPostDto>> GetBlogPostsAsync()
        {
            return await _dbContext.BlogPosts
                .AsNoTracking()
                .Where(x => x.IsPublished)
                .OrderByDescending(x => x.PublishedAt ?? x.CreatedAt)
                .Select(x => new StorefrontBlogPostDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Slug = x.Slug,
                    Summary = x.Summary,
                    CoverImagePath = x.CoverImagePath,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<BlogPostDto> GetBlogPostBySlugAsync(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                throw new BusinessException("آدرس مطلب معتبر نیست.");

            var normalizedSlug = slug.Trim();

            var post = await _dbContext.BlogPosts
                .AsNoTracking()
                .Where(x =>
                    x.IsPublished &&
                    x.Slug == normalizedSlug)
                .Select(x => new BlogPostDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Slug = x.Slug,
                    Summary = x.Summary,
                    ContentHtml = x.ContentHtml,
                    CoverImagePath = x.CoverImagePath,
                    SeoTitle = x.SeoTitle,
                    SeoDescription = x.SeoDescription,
                    CreatedAt = x.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (post == null)
                throw new NotFoundException("مطلب یافت نشد.");

            return post;
        }
    }
}