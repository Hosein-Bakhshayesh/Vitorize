using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Admin.Products;
using Vitorize.Application.DTOs.Products;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class AdminProductService : IAdminProductService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly IHtmlContentSanitizer _htmlSanitizer;

        public AdminProductService(VitorizeDbContext dbContext, IHtmlContentSanitizer htmlSanitizer)
        {
            _dbContext = dbContext;
            _htmlSanitizer = htmlSanitizer;
        }

        public async Task<List<AdminProductDto>> GetAllAsync()
        {
            return await _dbContext.Products
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.SortOrder)
                .ThenByDescending(x => x.CreatedAt)
                .Select(x => new AdminProductDto
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
                    FinalPrice =
                        x.DiscountPrice.HasValue &&
                        x.DiscountPrice.Value > 0 &&
                        x.DiscountPrice.Value < x.BasePrice
                            ? x.DiscountPrice.Value
                            : x.BasePrice,
                    CurrencyType = x.CurrencyType,
                    RequiresVerification = x.RequiresVerification,
                    RequiresSupportMessage = x.RequiresSupportMessage,
                    MinOrderQuantity = x.MinOrderQuantity,
                    MaxOrderQuantity = x.MaxOrderQuantity,
                    IsFeatured = x.IsFeatured,
                    IsActive = x.IsActive,
                    SeoTitle = x.SeoTitle,
                    SeoDescription = x.SeoDescription,
                    ThumbnailImagePath = x.ThumbnailImagePath,
                    SortOrder = x.SortOrder,
                    CategoryTitle = x.Category.Title,
                    BrandTitle = x.Brand != null ? x.Brand.Title : null,
                    AvailableStock = x.GiftCodes.Count(c =>
                        c.Status == (byte)GiftCodeStatus.Available),
                    HasVariants = x.ProductVariants.Any(),
                    CreatedAt = x.CreatedAt,
                    Features = x.ProductFeatures.OrderBy(f => f.SortOrder).ThenBy(f => f.Id).Select(f => new ProductFeatureDto
                    {
                        Id = f.Id, Title = f.Title, Value = f.Value, IconKey = f.IconKey,
                        SortOrder = f.SortOrder, IsActive = f.IsActive
                    }).ToList(),
                    InputFields = x.ProductInputFields.OrderBy(f => f.SortOrder).ThenBy(f => f.Id).Select(f => new ProductInputFieldDto
                    {
                        Id = f.Id, Key = f.Key, Label = f.Label, Description = f.Description,
                        Placeholder = f.Placeholder, FieldType = f.FieldType, IsRequired = f.IsRequired,
                        DefaultValue = f.DefaultValue, MinLength = f.MinLength,
                        MaxLength = f.MaxLength, ValidationPattern = f.ValidationPattern,
                        ValidationMessage = f.ValidationMessage, IsSensitive = f.IsSensitive,
                        RequiresConfirmation = f.RequiresConfirmation, DisplayStage = f.DisplayStage,
                        SortOrder = f.SortOrder, IsActive = f.IsActive
                    }).ToList()
                })
                .ToListAsync();
        }

        public async Task<AdminProductDto> GetByIdAsync(Guid id)
        {
            var product = await _dbContext.Products
                .AsNoTracking()
                .Where(x => x.Id == id && !x.IsDeleted)
                .Select(x => new AdminProductDto
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
                    FinalPrice =
                        x.DiscountPrice.HasValue &&
                        x.DiscountPrice.Value > 0 &&
                        x.DiscountPrice.Value < x.BasePrice
                            ? x.DiscountPrice.Value
                            : x.BasePrice,
                    CurrencyType = x.CurrencyType,
                    RequiresVerification = x.RequiresVerification,
                    RequiresSupportMessage = x.RequiresSupportMessage,
                    MinOrderQuantity = x.MinOrderQuantity,
                    MaxOrderQuantity = x.MaxOrderQuantity,
                    IsFeatured = x.IsFeatured,
                    IsActive = x.IsActive,
                    SeoTitle = x.SeoTitle,
                    SeoDescription = x.SeoDescription,
                    ThumbnailImagePath = x.ThumbnailImagePath,
                    SortOrder = x.SortOrder,
                    CategoryTitle = x.Category.Title,
                    BrandTitle = x.Brand != null ? x.Brand.Title : null,
                    AvailableStock = x.GiftCodes.Count(c =>
                        c.Status == (byte)GiftCodeStatus.Available),
                    HasVariants = x.ProductVariants.Any(),
                    CreatedAt = x.CreatedAt,
                    Features = x.ProductFeatures.OrderBy(f => f.SortOrder).ThenBy(f => f.Id).Select(f => new ProductFeatureDto
                    {
                        Id = f.Id, Title = f.Title, Value = f.Value, IconKey = f.IconKey,
                        SortOrder = f.SortOrder, IsActive = f.IsActive
                    }).ToList(),
                    InputFields = x.ProductInputFields.OrderBy(f => f.SortOrder).ThenBy(f => f.Id).Select(f => new ProductInputFieldDto
                    {
                        Id = f.Id, Key = f.Key, Label = f.Label, Description = f.Description,
                        Placeholder = f.Placeholder, FieldType = f.FieldType, IsRequired = f.IsRequired,
                        DefaultValue = f.DefaultValue, MinLength = f.MinLength,
                        MaxLength = f.MaxLength, ValidationPattern = f.ValidationPattern,
                        ValidationMessage = f.ValidationMessage, IsSensitive = f.IsSensitive,
                        RequiresConfirmation = f.RequiresConfirmation, DisplayStage = f.DisplayStage,
                        SortOrder = f.SortOrder, IsActive = f.IsActive
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (product == null)
                throw new NotFoundException("محصول یافت نشد.");

            var optionRows = await _dbContext.ProductInputFields.AsNoTracking()
                .Where(x => x.ProductId == id).Select(x => new { x.Id, x.OptionsJson }).ToListAsync();
            foreach (var field in product.InputFields)
                field.Options = ParseOptions(optionRows.FirstOrDefault(x => x.Id == field.Id)?.OptionsJson);

            return product;
        }

        public async Task<AdminProductDto> CreateAsync(CreateProductRequestDto request)
        {
            NormalizeRequest(request);

            await ValidateAsync(request, null);

            var product = new Product
            {
                Id = Guid.NewGuid(),
                CategoryId = request.CategoryId,
                BrandId = request.BrandId,
                Title = request.Title.Trim(),
                Slug = request.Slug.Trim().ToLowerInvariant(),
                ShortDescription = NormalizeNullable(request.ShortDescription),
                FullDescription = _htmlSanitizer.Sanitize(request.FullDescription),
                ProductType = request.ProductType,
                DeliveryType = request.DeliveryType,
                BasePrice = request.BasePrice,
                DiscountPrice = NormalizeDiscountPrice(request.DiscountPrice),
                CurrencyType = request.CurrencyType,
                RequiresVerification = request.RequiresVerification,
                RequiresSupportMessage = request.RequiresSupportMessage,
                MinOrderQuantity = request.MinOrderQuantity,
                MaxOrderQuantity = request.MaxOrderQuantity,
                IsFeatured = request.IsFeatured,
                IsActive = request.IsActive,
                SeoTitle = NormalizeNullable(request.SeoTitle),
                SeoDescription = NormalizeNullable(request.SeoDescription),
                ThumbnailImagePath = NormalizeNullable(request.ThumbnailImagePath),
                SortOrder = request.SortOrder,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            await _dbContext.Products.AddAsync(product);
            await SyncMetadataAsync(product, request.Features, request.InputFields);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return await GetByIdAsync(product.Id);
        }

        public async Task<AdminProductDto> UpdateAsync(
            Guid id,
            UpdateProductRequestDto request)
        {
            NormalizeRequest(request);

            var product = await _dbContext.Products
                .Include(x => x.ProductFeatures)
                .Include(x => x.ProductInputFields)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (product == null)
                throw new NotFoundException("محصول یافت نشد.");

            await ValidateAsync(request, id);

            product.CategoryId = request.CategoryId;
            product.BrandId = request.BrandId;
            product.Title = request.Title.Trim();
            product.Slug = request.Slug.Trim().ToLowerInvariant();
            product.ShortDescription = NormalizeNullable(request.ShortDescription);
            product.FullDescription = _htmlSanitizer.Sanitize(request.FullDescription);
            product.ProductType = request.ProductType;
            product.DeliveryType = request.DeliveryType;
            product.BasePrice = request.BasePrice;
            product.DiscountPrice = NormalizeDiscountPrice(request.DiscountPrice);
            product.CurrencyType = request.CurrencyType;
            product.RequiresVerification = request.RequiresVerification;
            product.RequiresSupportMessage = request.RequiresSupportMessage;
            product.MinOrderQuantity = request.MinOrderQuantity;
            product.MaxOrderQuantity = request.MaxOrderQuantity;
            product.IsFeatured = request.IsFeatured;
            product.IsActive = request.IsActive;
            product.SeoTitle = NormalizeNullable(request.SeoTitle);
            product.SeoDescription = NormalizeNullable(request.SeoDescription);
            product.ThumbnailImagePath = NormalizeNullable(request.ThumbnailImagePath);
            product.SortOrder = request.SortOrder;
            product.UpdatedAt = DateTime.UtcNow;

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            await SyncMetadataAsync(product, request.Features, request.InputFields);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return await GetByIdAsync(product.Id);
        }

        public async Task DeleteAsync(Guid id)
        {
            var product = await _dbContext.Products
                .Include(x => x.GiftCodes)
                .Include(x => x.OrderItems)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (product == null)
                throw new NotFoundException("محصول یافت نشد.");

            if (product.OrderItems.Any())
                throw new BusinessException("این محصول دارای سفارش است و قابل حذف نیست.");

            if (product.GiftCodes.Any())
                throw new BusinessException("این محصول دارای کد گیفت کارت است و قابل حذف نیست.");

            product.IsDeleted = true;
            product.DeletedAt = DateTime.UtcNow;
            product.IsActive = false;

            await _dbContext.SaveChangesAsync();
        }

        private async Task ValidateAsync(
            CreateProductRequestDto request,
            Guid? currentId)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new BusinessException("عنوان محصول الزامی است.");

            if (string.IsNullOrWhiteSpace(request.Slug))
                throw new BusinessException("اسلاگ محصول الزامی است.");

            if (request.CategoryId == Guid.Empty)
                throw new BusinessException("دسته‌بندی محصول الزامی است.");

            if (request.BrandId.HasValue && request.BrandId.Value == Guid.Empty)
                request.BrandId = null;

            if (request.BasePrice < 0)
                throw new BusinessException("قیمت محصول معتبر نیست.");

            if (request.DiscountPrice.HasValue &&
                request.DiscountPrice.Value < 0)
            {
                throw new BusinessException("قیمت تخفیف معتبر نیست.");
            }

            if (request.DiscountPrice.HasValue &&
                request.DiscountPrice.Value > 0 &&
                request.DiscountPrice.Value > request.BasePrice)
            {
                throw new BusinessException("قیمت تخفیف نمی‌تواند بیشتر از قیمت اصلی باشد.");
            }

            if (request.MinOrderQuantity <= 0)
                throw new BusinessException("حداقل تعداد سفارش باید بیشتر از صفر باشد.");

            if (request.MaxOrderQuantity.HasValue &&
                request.MaxOrderQuantity.Value < request.MinOrderQuantity)
            {
                throw new BusinessException("حداکثر تعداد سفارش نمی‌تواند کمتر از حداقل تعداد باشد.");
            }

            if (!Enum.IsDefined(typeof(ProductType), request.ProductType))
                throw new BusinessException("نوع محصول معتبر نیست.");

            if (!Enum.IsDefined(typeof(DeliveryType), request.DeliveryType))
                throw new BusinessException("نوع تحویل معتبر نیست.");

            if (!Enum.IsDefined(typeof(CurrencyType), request.CurrencyType))
                throw new BusinessException("واحد پول معتبر نیست.");

            if (request.CurrencyType != (byte)CurrencyType.Rial &&
                request.CurrencyType != (byte)CurrencyType.Toman)
            {
                throw new BusinessException("فقط ثبت قیمت با واحد ریال یا تومان مجاز است.");
            }

            var normalizedSlug = request.Slug.Trim().ToLowerInvariant();

            var slugExists = await _dbContext.Products.AnyAsync(x =>
                x.Slug == normalizedSlug &&
                !x.IsDeleted &&
                (!currentId.HasValue || x.Id != currentId.Value));

            if (slugExists)
                throw new BusinessException("این اسلاگ قبلاً برای محصول دیگری ثبت شده است.");

            var categoryExists = await _dbContext.Categories.AnyAsync(x =>
                x.Id == request.CategoryId &&
                !x.IsDeleted &&
                x.IsActive);

            if (!categoryExists)
                throw new BusinessException("دسته‌بندی محصول معتبر نیست.");

            if (request.BrandId.HasValue)
            {
                var brandExists = await _dbContext.Brands.AnyAsync(x =>
                    x.Id == request.BrandId.Value &&
                    x.IsActive);

                if (!brandExists)
                    throw new BusinessException("برند محصول معتبر نیست.");
            }

            if (request.Features.Count > 50 || request.InputFields.Count > 30)
                throw new BusinessException("تعداد ویژگی‌ها یا فیلدهای موردنیاز بیش از حد مجاز است.");
            if (request.InputFields.Select(x => x.Key?.Trim().ToLowerInvariant()).Distinct(StringComparer.Ordinal).Count() != request.InputFields.Count)
                throw new BusinessException("نام داخلی فیلدهای اطلاعات خریدار باید یکتا باشد.");
            foreach (var field in request.InputFields) ProductInputRules.ValidateDefinition(field);
            foreach (var feature in request.Features) ProductFeatureRules.Validate(feature);
        }

        private async Task SyncMetadataAsync(Product product, IReadOnlyList<ProductFeatureDto> features, IReadOnlyList<ProductInputFieldDto> fields)
        {
            if (product.ProductFeatures.Count > 0) _dbContext.ProductFeatures.RemoveRange(product.ProductFeatures);
            if (product.ProductInputFields.Count > 0) _dbContext.ProductInputFields.RemoveRange(product.ProductInputFields);
            var now = DateTime.UtcNow;
            await _dbContext.ProductFeatures.AddRangeAsync(features.Select((x, index) => new ProductFeature
            {
                Id = Guid.NewGuid(), ProductId = product.Id, Title = x.Title.Trim(), Value = x.Value.Trim(),
                IconKey = string.IsNullOrWhiteSpace(x.IconKey) ? null : x.IconKey.Trim(),
                SortOrder = x.SortOrder == 0 ? (index + 1) * 10 : x.SortOrder,
                IsActive = x.IsActive, CreatedAt = now
            }));
            await _dbContext.ProductInputFields.AddRangeAsync(fields.Select((x, index) => new ProductInputField
            {
                Id = Guid.NewGuid(), ProductId = product.Id, Key = x.Key.Trim().ToLowerInvariant(), Label = x.Label.Trim(),
                Description = NormalizeNullable(x.Description), Placeholder = NormalizeNullable(x.Placeholder),
                FieldType = x.FieldType, IsRequired = x.IsRequired,
                OptionsJson = x.Options.Count == 0 ? null : JsonSerializer.Serialize(x.Options),
                DefaultValue = NormalizeNullable(x.DefaultValue), MinLength = x.MinLength, MaxLength = x.MaxLength,
                ValidationPattern = NormalizeNullable(x.ValidationPattern), ValidationMessage = NormalizeNullable(x.ValidationMessage),
                IsSensitive = x.IsSensitive, RequiresConfirmation = x.RequiresConfirmation,
                DisplayStage = x.DisplayStage, SortOrder = x.SortOrder == 0 ? (index + 1) * 10 : x.SortOrder,
                IsActive = x.IsActive, CreatedAt = now
            }));
        }

        private static List<string> ParseOptions(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
            catch { return new(); }
        }

        private static void NormalizeRequest(CreateProductRequestDto request)
        {
            request.Title = request.Title?.Trim() ?? string.Empty;
            request.Slug = request.Slug?.Trim().ToLowerInvariant() ?? string.Empty;

            if (request.BrandId.HasValue && request.BrandId.Value == Guid.Empty)
                request.BrandId = null;

            request.ShortDescription = NormalizeNullable(request.ShortDescription);
            request.FullDescription = NormalizeNullable(request.FullDescription);
            request.SeoTitle = NormalizeNullable(request.SeoTitle);
            request.SeoDescription = NormalizeNullable(request.SeoDescription);
            request.ThumbnailImagePath = NormalizeNullable(request.ThumbnailImagePath);
            request.DiscountPrice = NormalizeDiscountPrice(request.DiscountPrice);
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static decimal? NormalizeDiscountPrice(decimal? value)
        {
            if (!value.HasValue)
                return null;

            if (value.Value <= 0)
                return null;

            return value.Value;
        }
    }
}
