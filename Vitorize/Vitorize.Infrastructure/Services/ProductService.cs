using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Products;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;
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

        public async Task<List<ProductListItemDto>> GetProductsAsync(ProductFilterDto filter)
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

            var products = await query
                .OrderBy(x => x.SortOrder)
                .ThenByDescending(x => x.CreatedAt)
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
                            g.Status == GiftCodeStatusAvailable)
                })
                .ToListAsync();

            return products;
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

            return await GetProductsAsync(new ProductFilterDto
            {
                IsFeatured = true,
                Page = 1,
                PageSize = count
            });
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