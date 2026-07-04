using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Products;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class ProductService : IProductService
    {
        private readonly VitorizeDbContext _dbContext;

        private const byte GiftCodeStatusAvailable = 0;
        private const byte DeliveryTypeInstant = 1;
        private const byte DeliveryTypeManualTicket = 2;

        public ProductService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<PagedResult<ProductListItemDto>> GetProductsAsync(ProductFilterDto filter)
        {
            var page = filter.Page <= 0 ? 1 : filter.Page;
            var pageSize = filter.PageSize <= 0 ? 20 : filter.PageSize;
            pageSize = pageSize > 100 ? 100 : pageSize;

            var query = _dbContext.Products
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.Brand)
                .Include(x => x.ProductVariants)
                .Where(x =>
                    x.IsActive &&
                    !x.IsDeleted &&
                    x.Category.IsActive &&
                    !x.Category.IsDeleted);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.Trim();

                query = query.Where(x =>
                    x.Title.Contains(search) ||
                    x.Slug.Contains(search) ||
                    (x.ShortDescription != null && x.ShortDescription.Contains(search)));
            }

            if (filter.CategoryId.HasValue)
            {
                query = query.Where(x => x.CategoryId == filter.CategoryId.Value);
            }

            if (filter.BrandId.HasValue)
            {
                query = query.Where(x => x.BrandId == filter.BrandId.Value);
            }

            if (filter.IsFeatured.HasValue)
            {
                query = query.Where(x => x.IsFeatured == filter.IsFeatured.Value);
            }

            if (filter.MinPrice.HasValue)
            {
                var minPrice = filter.MinPrice.Value;

                query = query.Where(x =>
                    (x.DiscountPrice != null && x.DiscountPrice > 0 && x.DiscountPrice < x.BasePrice
                        ? x.DiscountPrice.Value
                        : x.BasePrice) >= minPrice);
            }

            if (filter.MaxPrice.HasValue)
            {
                var maxPrice = filter.MaxPrice.Value;

                query = query.Where(x =>
                    (x.DiscountPrice != null && x.DiscountPrice > 0 && x.DiscountPrice < x.BasePrice
                        ? x.DiscountPrice.Value
                        : x.BasePrice) <= maxPrice);
            }

            if (filter.HasDiscount.HasValue)
            {
                query = filter.HasDiscount.Value
                    ? query.Where(x =>
                        x.DiscountPrice != null &&
                        x.DiscountPrice > 0 &&
                        x.DiscountPrice < x.BasePrice)
                    : query.Where(x =>
                        x.DiscountPrice == null ||
                        x.DiscountPrice <= 0 ||
                        x.DiscountPrice >= x.BasePrice);
            }

            if (filter.InStock == true)
            {
                query = query.Where(x =>
                    x.DeliveryType == DeliveryTypeManualTicket ||
                    _dbContext.GiftCodes.Any(g =>
                        g.ProductId == x.Id &&
                        g.Status == GiftCodeStatusAvailable));
            }

            var totalCount = await query.CountAsync();

            query = (filter.Sort ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "newest" => query
                    .OrderByDescending(x => x.CreatedAt),
                "cheapest" => query
                    .OrderBy(x =>
                        x.DiscountPrice != null && x.DiscountPrice > 0 && x.DiscountPrice < x.BasePrice
                            ? x.DiscountPrice.Value
                            : x.BasePrice)
                    .ThenByDescending(x => x.CreatedAt),
                "expensive" => query
                    .OrderByDescending(x =>
                        x.DiscountPrice != null && x.DiscountPrice > 0 && x.DiscountPrice < x.BasePrice
                            ? x.DiscountPrice.Value
                            : x.BasePrice)
                    .ThenByDescending(x => x.CreatedAt),
                "discount" => query
                    .OrderByDescending(x =>
                        x.DiscountPrice != null && x.DiscountPrice > 0 && x.DiscountPrice < x.BasePrice && x.BasePrice > 0
                            ? (x.BasePrice - x.DiscountPrice.Value) / x.BasePrice
                            : 0)
                    .ThenByDescending(x => x.CreatedAt),
                _ => query
                    .OrderBy(x => x.SortOrder)
                    .ThenByDescending(x => x.CreatedAt)
            };

            var products = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ProductListItemDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Slug = x.Slug,
                    ShortDescription = x.ShortDescription,
                    ThumbnailImagePath = x.ThumbnailImagePath,
                    BasePrice = x.BasePrice,
                    DiscountPrice = x.DiscountPrice,
                    ProductType = x.ProductType,
                    DeliveryType = x.DeliveryType,
                    CurrencyType = x.CurrencyType,
                    RequiresVerification = x.RequiresVerification,
                    IsFeatured = x.IsFeatured,
                    CategoryTitle = x.Category.Title,
                    BrandTitle = x.Brand != null ? x.Brand.Title : null,
                    HasVariants = x.ProductVariants.Any(v => v.IsActive),
                    AvailableStock = x.DeliveryType == DeliveryTypeManualTicket
                        ? 999999
                        : _dbContext.GiftCodes.Count(g =>
                            g.ProductId == x.Id &&
                            g.Status == GiftCodeStatusAvailable),
                    AverageRating = _dbContext.ProductReviews
                        .Where(r =>
                            r.ProductId == x.Id &&
                            r.IsApproved &&
                            !r.IsDeleted)
                        .Select(r => (double?)r.Rating)
                        .Average() ?? 0,
                    ReviewCount = _dbContext.ProductReviews
                        .Count(r =>
                            r.ProductId == x.Id &&
                            r.IsApproved &&
                            !r.IsDeleted)
                })
                .ToListAsync();

            return new PagedResult<ProductListItemDto>
            {
                Items = products,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<ProductDetailDto> GetProductByIdAsync(Guid id)
        {
            var product = await BuildProductDetailQuery()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (product == null)
            {
                throw new NotFoundException("محصول یافت نشد.");
            }

            return product;
        }

        public async Task<ProductDetailDto> GetProductBySlugAsync(string slug)
        {
            var product = await BuildProductDetailQuery()
                .FirstOrDefaultAsync(x => x.Slug == slug);

            if (product == null)
            {
                throw new NotFoundException("محصول یافت نشد.");
            }

            return product;
        }

        public async Task<List<ProductListItemDto>> GetFeaturedProductsAsync(int count = 10)
        {
            count = count <= 0 ? 10 : count;
            count = count > 50 ? 50 : count;

            var result = await GetProductsAsync(new ProductFilterDto
            {
                IsFeatured = true,
                Page = 1,
                PageSize = count
            });

            return result.Items.ToList();
        }

        public async Task<List<ProductListItemDto>> GetRelatedProductsAsync(
            Guid productId,
            int count = 8)
        {
            count = count <= 0 ? 8 : count;
            count = count > 24 ? 24 : count;

            var source = await _dbContext.Products
                .AsNoTracking()
                .Where(x => x.Id == productId && x.IsActive && !x.IsDeleted)
                .Select(x => new { x.Id, x.CategoryId, x.BrandId })
                .FirstOrDefaultAsync();

            if (source == null)
                throw new NotFoundException("محصول یافت نشد.");

            return await _dbContext.Products
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.Brand)
                .Include(x => x.ProductVariants)
                .Where(x =>
                    x.Id != source.Id &&
                    x.IsActive &&
                    !x.IsDeleted &&
                    x.Category.IsActive &&
                    !x.Category.IsDeleted &&
                    (x.CategoryId == source.CategoryId ||
                     (source.BrandId != null && x.BrandId == source.BrandId)))
                .OrderByDescending(x => x.CategoryId == source.CategoryId)
                .ThenByDescending(x => x.IsFeatured)
                .ThenBy(x => x.SortOrder)
                .ThenByDescending(x => x.CreatedAt)
                .Take(count)
                .Select(x => new ProductListItemDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Slug = x.Slug,
                    ShortDescription = x.ShortDescription,
                    ThumbnailImagePath = x.ThumbnailImagePath,
                    BasePrice = x.BasePrice,
                    DiscountPrice = x.DiscountPrice,
                    ProductType = x.ProductType,
                    DeliveryType = x.DeliveryType,
                    CurrencyType = x.CurrencyType,
                    RequiresVerification = x.RequiresVerification,
                    IsFeatured = x.IsFeatured,
                    CategoryTitle = x.Category.Title,
                    BrandTitle = x.Brand != null ? x.Brand.Title : null,
                    HasVariants = x.ProductVariants.Any(v => v.IsActive),
                    AvailableStock = x.DeliveryType == DeliveryTypeManualTicket
                        ? 999999
                        : _dbContext.GiftCodes.Count(g =>
                            g.ProductId == x.Id &&
                            g.Status == GiftCodeStatusAvailable)
                })
                .ToListAsync();
        }

        public async Task<List<ProductLookupDto>> GetCategoriesAsync()
        {
            return await _dbContext.Categories
                .AsNoTracking()
                .Where(x => x.IsActive && !x.IsDeleted)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Title)
                .Select(x => new ProductLookupDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Slug = x.Slug,
                    ImagePath = x.ImagePath
                })
                .ToListAsync();
        }

        public async Task<List<ProductLookupDto>> GetBrandsAsync()
        {
            return await _dbContext.Brands
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Title)
                .Select(x => new ProductLookupDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Slug = x.Slug,
                    ImagePath = x.ImagePath
                })
                .ToListAsync();
        }

        private IQueryable<ProductDetailDto> BuildProductDetailQuery()
        {
            return _dbContext.Products
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    !x.IsDeleted &&
                    x.Category.IsActive &&
                    !x.Category.IsDeleted)
                .Select(x => new ProductDetailDto
                {
                    Id = x.Id,
                    CategoryId = x.CategoryId,
                    BrandId = x.BrandId,
                    Title = x.Title,
                    Slug = x.Slug,
                    ShortDescription = x.ShortDescription,
                    FullDescription = x.FullDescription,
                    ProductType = x.ProductType,
                    DeliveryType = x.DeliveryType,
                    BasePrice = x.BasePrice,
                    DiscountPrice = x.DiscountPrice,
                    CurrencyType = x.CurrencyType,
                    RequiresVerification = x.RequiresVerification,
                    RequiresSupportMessage = x.RequiresSupportMessage,
                    MinOrderQuantity = x.MinOrderQuantity,
                    MaxOrderQuantity = x.MaxOrderQuantity,
                    IsFeatured = x.IsFeatured,
                    SeoTitle = x.SeoTitle,
                    SeoDescription = x.SeoDescription,
                    ThumbnailImagePath = x.ThumbnailImagePath,
                    CategoryTitle = x.Category.Title,
                    BrandTitle = x.Brand != null ? x.Brand.Title : null,

                    Images = x.ProductImages
                        .OrderBy(i => i.SortOrder)
                        .Select(i => i.ImagePath)
                        .ToList(),

                    Tags = x.Tags
                        .Select(t => t.Title)
                        .ToList(),

                    Variants = x.ProductVariants
                        .Where(v => v.IsActive)
                        .OrderBy(v => v.SortOrder)
                        .Select(v => new ProductVariantDto
                        {
                            Id = v.Id,
                            Title = v.Title,
                            Sku = v.Sku,
                            Price = v.Price,
                            DiscountPrice = v.DiscountPrice,
                            Value = v.Value,
                            StockMode = v.StockMode,
                            IsDefault = v.IsDefault,
                            SortOrder = v.SortOrder,
                            AvailableStock = x.DeliveryType == DeliveryTypeManualTicket
                                ? 999999
                                : _dbContext.GiftCodes.Count(g =>
                                    g.ProductId == x.Id &&
                                    g.ProductVariantId == v.Id &&
                                    g.Status == GiftCodeStatusAvailable)
                        })
                        .ToList(),

                    AvailableStock = x.DeliveryType == DeliveryTypeManualTicket
                        ? 999999
                        : _dbContext.GiftCodes.Count(g =>
                            g.ProductId == x.Id &&
                            g.Status == GiftCodeStatusAvailable)
                });
        }
    }
}