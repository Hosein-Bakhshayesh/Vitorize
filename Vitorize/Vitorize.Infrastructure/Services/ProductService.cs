using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Vitorize.Application.DTOs.Products;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;
using Vitorize.Shared.Storefront;

namespace Vitorize.Infrastructure.Services
{
    public class ProductService : IProductService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly IHtmlContentSanitizer _htmlSanitizer;

        private const byte GiftCodeStatusAvailable = 0;
        private const byte DeliveryTypeInstant = 1;
        // Unlimited is an inventory policy; it is compared as a mode, never as a quantity.
        private const byte StockModeUnlimited = (byte)ProductVariantStockMode.Unlimited;
        // Best-selling counts paid orders only, matching the admin dashboard's metric.
        private const byte PaymentStatusPaid = (byte)PaymentStatus.Paid;

        public ProductService(VitorizeDbContext dbContext, IHtmlContentSanitizer htmlSanitizer)
        {
            _dbContext = dbContext;
            _htmlSanitizer = htmlSanitizer;
        }

        public async Task<PagedResult<ProductListItemDto>> GetProductsAsync(ProductFilterDto filter)
        {
            filter ??= new ProductFilterDto();
            var page = filter.Page <= 0 ? 1 : filter.Page;
            var pageSize = filter.PageSize <= 0 ? 20 : filter.PageSize;
            pageSize = pageSize > 100 ? 100 : pageSize;

            if (filter.MinPrice is < 0 || filter.MaxPrice is < 0 ||
                (filter.MinPrice.HasValue && filter.MaxPrice.HasValue && filter.MinPrice > filter.MaxPrice))
                throw new BusinessException("بازه قیمت معتبر نیست.");

            if (filter.MinDiscountPercent is < 0 or > 100)
                throw new BusinessException("حداقل درصد تخفیف معتبر نیست.");

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

                // EF sizes each Contains parameter to the matched column width, so a search term longer
                // than the narrowest searched column (ProductTag.Title, nvarchar(100)) raises a
                // "String or binary data would be truncated" SqlException and a 500. A ~250-char term is
                // a valid short URL in production, so cap the term - anything longer than the longest
                // tag/title cannot yield a meaningful match anyway.
                const int maxSearchLength = 100;
                if (search.Length > maxSearchLength)
                    search = search[..maxSearchLength];

                query = query.Where(x =>
                    x.Title.Contains(search) ||
                    x.Slug.Contains(search) ||
                    (x.ShortDescription != null && x.ShortDescription.Contains(search)) ||
                    x.Tags.Any(t => t.IsActive &&
                        (t.Title.Contains(search) || t.Slug.Contains(search) ||
                         (t.Aliases != null && t.Aliases.Contains(search)))));
            }

            if (filter.CategoryId.HasValue)
            {
                // A product belongs to a category when it has a membership row OR when that
                // category is its primary one. The primary is a membership by definition, so
                // counting it here is the same single rule - not a second source of truth. It also
                // means a product created outside the admin service (a SQL seed, a data import,
                // legacy tooling) can never silently disappear from its own category listing
                // because no join row was written for it.
                var categoryId = filter.CategoryId.Value;
                query = query.Where(x =>
                    x.CategoryId == categoryId ||
                    x.ProductCategories.Any(pc => pc.CategoryId == categoryId));
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
                // The same availability expression the "availability" sort ranks by - one truth for
                // "in stock". The previous shape predated the V0022 availability model: it passed
                // every manual-delivery product regardless of variant stock, ignored Unlimited, and
                // let ForceOutOfStock products through the "only available" filter.
                query = query.Where(x =>
                    !x.ForceOutOfStock &&
                    ((x.DeliveryType != DeliveryTypeInstant &&
                      x.ProductVariants.Any(v => v.IsActive && v.StockMode == StockModeUnlimited)) ||
                     (x.DeliveryType == DeliveryTypeInstant
                         ? _dbContext.GiftCodes.Count(g => g.ProductId == x.Id && g.Status == GiftCodeStatusAvailable)
                         : x.ProductVariants.Where(v => v.IsActive).Sum(v => (int?)v.StockQuantity) ?? 0) > 0));
            }

            var productTypes = filter.ProductTypes?
                .Distinct()
                .Where(type => Enum.IsDefined(typeof(ProductType), type))
                .ToArray();
            if (productTypes is { Length: > 0 })
                query = query.Where(x => productTypes.Contains(x.ProductType));

            if (filter.DeliveryType.HasValue)
                query = query.Where(x => x.DeliveryType == filter.DeliveryType.Value);

            if (filter.RequiresVerification.HasValue)
                query = query.Where(x => x.RequiresVerification == filter.RequiresVerification.Value);

            if (filter.MinDiscountPercent is > 0)
            {
                var minimumDiscount = filter.MinDiscountPercent.Value / 100m;
                query = query.Where(x =>
                    x.DiscountPrice != null && x.DiscountPrice > 0 && x.DiscountPrice < x.BasePrice && x.BasePrice > 0 &&
                    (x.BasePrice - x.DiscountPrice.Value) / x.BasePrice >= minimumDiscount);
            }

            var totalCount = await query.CountAsync();

            // A customer's explicit choice always wins. Only when they have not asked for an order
            // does the administrator's saved storefront default decide it. The setting is read
            // straight from the database rather than through a cache, so a saved change is in
            // effect on the very next listing request without recycling anything.
            var requestedSort = (filter.Sort ?? string.Empty).Trim();
            var effectiveSort = requestedSort.Length > 0
                ? requestedSort.ToLowerInvariant()
                : StorefrontProductSortModes.ToQueryKey(
                    await _dbContext.Settings.AsNoTracking()
                        .Where(x => x.Key == StorefrontProductSortModes.SettingKey)
                        .Select(x => x.Value)
                        .FirstOrDefaultAsync());

            query = effectiveSort switch
            {
                // Available first, unavailable after, using the same canonical inputs the list
                // projection carries: an override wins outright, unlimited inventory is available
                // regardless of quantity, Instant draws on its gift-code pool and every other
                // delivery mode on managed per-variant stock.
                "availability" => query
                    .OrderByDescending(x =>
                        !x.ForceOutOfStock &&
                        ((x.DeliveryType != DeliveryTypeInstant &&
                          x.ProductVariants.Any(v => v.IsActive && v.StockMode == StockModeUnlimited)) ||
                         (x.DeliveryType == DeliveryTypeInstant
                             ? _dbContext.GiftCodes.Count(g => g.ProductId == x.Id && g.Status == GiftCodeStatusAvailable)
                             : x.ProductVariants.Where(v => v.IsActive).Sum(v => (int?)v.StockQuantity) ?? 0) > 0))
                    .ThenBy(x => x.SortOrder)
                    .ThenByDescending(x => x.CreatedAt)
                    .ThenBy(x => x.Id),
                // The same paid-order quantity the admin dashboard reports. Products that have never
                // sold fall to the back and keep the ordinary default order among themselves.
                "bestselling" => query
                    .OrderByDescending(x => _dbContext.OrderItems
                        .Where(oi => oi.ProductId == x.Id && oi.Order.PaymentStatus == PaymentStatusPaid)
                        .Sum(oi => (int?)oi.Quantity) ?? 0)
                    .ThenBy(x => x.SortOrder)
                    .ThenByDescending(x => x.CreatedAt)
                    .ThenBy(x => x.Id),
                "oldest" => query
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Id),
                "newest" => query
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenBy(x => x.Id),
                "cheapest" => query
                    .OrderBy(x =>
                        x.DiscountPrice != null && x.DiscountPrice > 0 && x.DiscountPrice < x.BasePrice
                            ? x.DiscountPrice.Value
                            : x.BasePrice)
                    .ThenByDescending(x => x.CreatedAt)
                    .ThenBy(x => x.Id),
                "expensive" => query
                    .OrderByDescending(x =>
                        x.DiscountPrice != null && x.DiscountPrice > 0 && x.DiscountPrice < x.BasePrice
                            ? x.DiscountPrice.Value
                            : x.BasePrice)
                    .ThenByDescending(x => x.CreatedAt)
                    .ThenBy(x => x.Id),
                "discount" => query
                    .OrderByDescending(x =>
                        x.DiscountPrice != null && x.DiscountPrice > 0 && x.DiscountPrice < x.BasePrice && x.BasePrice > 0
                            ? (x.BasePrice - x.DiscountPrice.Value) / x.BasePrice
                            : 0)
                    .ThenByDescending(x => x.CreatedAt)
                    .ThenBy(x => x.Id),
                _ => query
                    .OrderBy(x => x.SortOrder)
                    .ThenByDescending(x => x.CreatedAt)
                    .ThenBy(x => x.Id)
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
                    // Availability rule (see ProductAvailabilityRules): Instant is gift-code driven;
                    // every non-Instant mode uses managed per-variant stock.
                    AvailableStock = x.DeliveryType == DeliveryTypeInstant
                        ? _dbContext.GiftCodes.Count(g =>
                            g.ProductId == x.Id &&
                            g.Status == GiftCodeStatusAvailable)
                        : x.ProductVariants.Where(v => v.IsActive).Sum(v => (int?)v.StockQuantity) ?? 0,
                    // Carried so the caller can apply ProductAvailabilityRules rather than
                    // re-deriving availability from a bare number.
                    ForceOutOfStock = x.ForceOutOfStock,
                    IsUnlimitedStock = x.DeliveryType != DeliveryTypeInstant &&
                        x.ProductVariants.Any(v => v.IsActive && v.StockMode == StockModeUnlimited),
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

            await HydrateSafeMetadataAsync(product);
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

            await HydrateSafeMetadataAsync(product);
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
                .Select(x => new
                {
                    x.Id, x.CategoryId, x.BrandId,
                    // Affinity is measured against every category the product belongs to, not just
                    // its primary one.
                    CategoryIds = x.ProductCategories.Select(pc => pc.CategoryId).ToList(),
                    TagIds = x.Tags.Where(t => t.IsActive).Select(t => t.Id).ToList()
                })
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
                     x.ProductCategories.Any(pc => source.CategoryIds.Contains(pc.CategoryId)) ||
                     (source.BrandId != null && x.BrandId == source.BrandId) ||
                     x.Tags.Any(t => t.IsActive && source.TagIds.Contains(t.Id))))
                .OrderByDescending(x => x.ProductCategories.Any(pc => source.CategoryIds.Contains(pc.CategoryId)))
                .ThenByDescending(x => x.Tags.Count(t => t.IsActive && source.TagIds.Contains(t.Id)))
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
                    AvailableStock = x.DeliveryType == DeliveryTypeInstant
                        ? _dbContext.GiftCodes.Count(g =>
                            g.ProductId == x.Id &&
                            g.Status == GiftCodeStatusAvailable)
                        : x.ProductVariants.Where(v => v.IsActive).Sum(v => (int?)v.StockQuantity) ?? 0,
                    ForceOutOfStock = x.ForceOutOfStock,
                    IsUnlimitedStock = x.DeliveryType != DeliveryTypeInstant &&
                        x.ProductVariants.Any(v => v.IsActive && v.StockMode == StockModeUnlimited)
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
                    Icon = x.Icon,
                    ImagePath = x.ImagePath,
                    ImageAltText = x.ImageAltText,
                    Description = x.Description,
                    SeoTitle = x.SeoTitle,
                    SeoDescription = x.SeoDescription,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
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
                    ImagePath = x.ImagePath,
                    ImageAltText = x.ImageAltText,
                    Description = x.Description,
                    SeoTitle = x.SeoTitle,
                    SeoDescription = x.SeoDescription,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<ProductLookupDto> GetCategoryBySlugAsync(string slug)
        {
            var item = await _dbContext.Categories.AsNoTracking()
                .Where(x => x.IsActive && !x.IsDeleted && x.Slug == slug)
                .Select(x => new ProductLookupDto
                {
                    Id = x.Id, Title = x.Title, Slug = x.Slug, ImagePath = x.ImagePath,
                    ImageAltText = x.ImageAltText, Description = x.Description,
                    SeoTitle = x.SeoTitle, SeoDescription = x.SeoDescription,
                    CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt
                }).FirstOrDefaultAsync();
            return item ?? throw new NotFoundException("دسته‌بندی یافت نشد.");
        }

        public async Task<ProductLookupDto> GetBrandBySlugAsync(string slug)
        {
            var item = await _dbContext.Brands.AsNoTracking()
                .Where(x => x.IsActive && x.Slug == slug)
                .Select(x => new ProductLookupDto
                {
                    Id = x.Id, Title = x.Title, Slug = x.Slug, ImagePath = x.ImagePath,
                    ImageAltText = x.ImageAltText, Description = x.Description,
                    SeoTitle = x.SeoTitle, SeoDescription = x.SeoDescription,
                    CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt
                }).FirstOrDefaultAsync();
            return item ?? throw new NotFoundException("برند یافت نشد.");
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
                    KycRequirementMode = x.KycRequirementMode,
                    KycThresholdAmount = x.KycThresholdAmount,
                    KycPolicyVersionId = x.KycPolicyVersionId,
                    RequiresSupportMessage = x.RequiresSupportMessage,
                    MinOrderQuantity = x.MinOrderQuantity,
                    MaxOrderQuantity = x.MaxOrderQuantity,
                    IsFeatured = x.IsFeatured,
                    SeoTitle = x.SeoTitle,
                    SeoDescription = x.SeoDescription,
                    FocusKeyword = x.FocusKeyword,
                    ThumbnailImagePath = x.ThumbnailImagePath,
                    ThumbnailAltText = x.ThumbnailAltText,
                    CategoryTitle = x.Category.Title,
                    CategorySlug = x.Category.Slug,
                    BrandTitle = x.Brand != null ? x.Brand.Title : null,
                    BrandSlug = x.Brand != null ? x.Brand.Slug : null,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,

                    Images = x.ProductImages
                        .OrderBy(i => i.SortOrder)
                        .Select(i => i.ImagePath)
                        .ToList(),

                    ImageItems = x.ProductImages
                        .OrderBy(i => i.SortOrder)
                        .Select(i => new ProductImageMetadataDto
                        {
                            ImagePath = i.ImagePath,
                            AltText = i.AltText,
                            SortOrder = i.SortOrder
                        }).ToList(),

                    Tags = x.Tags
                        .Where(t => t.IsActive)
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
                            // Per-variant availability: gift codes for Instant, managed stock otherwise.
                            AvailableStock = x.DeliveryType == DeliveryTypeInstant
                                ? _dbContext.GiftCodes.Count(g =>
                                    g.ProductId == x.Id &&
                                    g.ProductVariantId == v.Id &&
                                    g.Status == GiftCodeStatusAvailable)
                                : v.StockQuantity,
                            IsUnlimitedStock = x.DeliveryType != DeliveryTypeInstant &&
                                v.StockMode == StockModeUnlimited,
                            ForceOutOfStock = x.ForceOutOfStock
                        })
                        .ToList(),

                    // Only this product's entries, active only, in administrator order.
                    Faqs = x.Faqs
                        .Where(f => f.IsActive)
                        .OrderBy(f => f.SortOrder).ThenBy(f => f.CreatedAt)
                        .Select(f => new Vitorize.Application.DTOs.Storefront.FaqDto
                        {
                            Id = f.Id, Question = f.Question, Answer = f.Answer, SortOrder = f.SortOrder
                        }).ToList(),

                    Features = x.ProductFeatures
                        .Where(f => f.IsActive)
                        .OrderBy(f => f.SortOrder).ThenBy(f => f.Id)
                        .Select(f => new ProductFeatureDto
                        {
                            Id = f.Id, Title = f.Title, Value = f.Value, IconKey = f.IconKey,
                            SortOrder = f.SortOrder, IsActive = f.IsActive
                        }).ToList(),

                    InputFields = x.ProductInputFields
                        .Where(f => f.IsActive)
                        .OrderBy(f => f.SortOrder).ThenBy(f => f.Id)
                        .Select(f => new ProductInputFieldDto
                        {
                            Id = f.Id, Key = f.Key, Label = f.Label, Description = f.Description,
                            Placeholder = f.Placeholder, FieldType = f.FieldType, IsRequired = f.IsRequired,
                            DefaultValue = f.DefaultValue, MinLength = f.MinLength, MaxLength = f.MaxLength,
                            ValidationMessage = f.ValidationMessage, IsSensitive = f.IsSensitive,
                            RequiresConfirmation = f.RequiresConfirmation, DisplayStage = f.DisplayStage,
                            SortOrder = f.SortOrder, IsActive = f.IsActive
                        }).ToList(),

                    AvailableStock = x.DeliveryType == DeliveryTypeInstant
                        ? _dbContext.GiftCodes.Count(g =>
                            g.ProductId == x.Id &&
                            g.Status == GiftCodeStatusAvailable)
                        : x.ProductVariants.Where(v => v.IsActive).Sum(v => (int?)v.StockQuantity) ?? 0,
                    ForceOutOfStock = x.ForceOutOfStock,
                    IsUnlimitedStock = x.DeliveryType != DeliveryTypeInstant &&
                        x.ProductVariants.Any(v => v.IsActive && v.StockMode == StockModeUnlimited)
                });
        }

        private async Task HydrateSafeMetadataAsync(ProductDetailDto product)
        {
            product.FullDescription = _htmlSanitizer.Sanitize(product.FullDescription);
            var options = await _dbContext.ProductInputFields.AsNoTracking()
                .Where(x => x.ProductId == product.Id && x.IsActive)
                .Select(x => new { x.Id, x.OptionsJson })
                .ToListAsync();
            foreach (var field in product.InputFields)
            {
                var json = options.FirstOrDefault(x => x.Id == field.Id)?.OptionsJson;
                if (string.IsNullOrWhiteSpace(json)) continue;
                try { field.Options = JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
                catch { field.Options = new(); }
            }
        }
    }
}
