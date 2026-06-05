using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.ProductVariants;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class AdminProductVariantService : IAdminProductVariantService
    {
        private readonly VitorizeDbContext _dbContext;

        private const byte GiftCodeStatusAvailable = 0;

        public AdminProductVariantService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<AdminProductVariantDto>> GetByProductIdAsync(Guid productId)
        {
            var productExists = await _dbContext.Products.AnyAsync(x =>
                x.Id == productId &&
                !x.IsDeleted);

            if (!productExists)
                throw new NotFoundException("محصول یافت نشد.");

            return await _dbContext.ProductVariants
                .AsNoTracking()
                .Where(x => x.ProductId == productId)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Title)
                .Select(x => new AdminProductVariantDto
                {
                    Id = x.Id,
                    ProductId = x.ProductId,
                    ProductTitle = x.Product.Title,
                    Title = x.Title,
                    Sku = x.Sku,
                    Price = x.Price,
                    DiscountPrice = x.DiscountPrice,
                    Value = x.Value,
                    StockMode = x.StockMode,
                    IsDefault = x.IsDefault,
                    IsActive = x.IsActive,
                    SortOrder = x.SortOrder,
                    AvailableStock = _dbContext.GiftCodes.Count(g =>
                        g.ProductVariantId == x.Id &&
                        g.Status == GiftCodeStatusAvailable)
                })
                .ToListAsync();
        }

        public async Task<AdminProductVariantDto> GetByIdAsync(Guid id)
        {
            var variant = await _dbContext.ProductVariants
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new AdminProductVariantDto
                {
                    Id = x.Id,
                    ProductId = x.ProductId,
                    ProductTitle = x.Product.Title,
                    Title = x.Title,
                    Sku = x.Sku,
                    Price = x.Price,
                    DiscountPrice = x.DiscountPrice,
                    Value = x.Value,
                    StockMode = x.StockMode,
                    IsDefault = x.IsDefault,
                    IsActive = x.IsActive,
                    SortOrder = x.SortOrder,
                    AvailableStock = _dbContext.GiftCodes.Count(g =>
                        g.ProductVariantId == x.Id &&
                        g.Status == GiftCodeStatusAvailable)
                })
                .FirstOrDefaultAsync();

            if (variant == null)
                throw new NotFoundException("تنوع محصول یافت نشد.");

            return variant;
        }

        public async Task<AdminProductVariantDto> CreateAsync(
            Guid productId,
            CreateProductVariantRequestDto request)
        {
            await ValidateAsync(productId, request, null);

            if (request.IsDefault)
            {
                await ClearDefaultVariantsAsync(productId, null);
            }

            var variant = new ProductVariant
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                Title = request.Title.Trim(),
                Sku = string.IsNullOrWhiteSpace(request.Sku)
                    ? null
                    : request.Sku.Trim(),
                Price = request.Price,
                DiscountPrice = request.DiscountPrice,
                Value = request.Value,
                StockMode = request.StockMode,
                IsDefault = request.IsDefault,
                IsActive = request.IsActive,
                SortOrder = request.SortOrder,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.ProductVariants.AddAsync(variant);
            await _dbContext.SaveChangesAsync();

            return await GetByIdAsync(variant.Id);
        }

        public async Task<AdminProductVariantDto> UpdateAsync(
            Guid id,
            UpdateProductVariantRequestDto request)
        {
            var variant = await _dbContext.ProductVariants
                .FirstOrDefaultAsync(x => x.Id == id);

            if (variant == null)
                throw new NotFoundException("تنوع محصول یافت نشد.");

            await ValidateAsync(variant.ProductId, request, id);

            if (request.IsDefault)
            {
                await ClearDefaultVariantsAsync(variant.ProductId, id);
            }

            variant.Title = request.Title.Trim();
            variant.Sku = string.IsNullOrWhiteSpace(request.Sku)
                ? null
                : request.Sku.Trim();
            variant.Price = request.Price;
            variant.DiscountPrice = request.DiscountPrice;
            variant.Value = request.Value;
            variant.StockMode = request.StockMode;
            variant.IsDefault = request.IsDefault;
            variant.IsActive = request.IsActive;
            variant.SortOrder = request.SortOrder;
            variant.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            return await GetByIdAsync(variant.Id);
        }

        public async Task DeleteAsync(Guid id)
        {
            var variant = await _dbContext.ProductVariants
                .Include(x => x.GiftCodes)
                .Include(x => x.OrderItems)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (variant == null)
                throw new NotFoundException("تنوع محصول یافت نشد.");

            if (variant.OrderItems.Any())
                throw new BusinessException("این تنوع محصول دارای سفارش است و قابل حذف نیست.");

            if (variant.GiftCodes.Any())
                throw new BusinessException("این تنوع محصول دارای کد گیفت کارت است و قابل حذف نیست.");

            _dbContext.ProductVariants.Remove(variant);

            await _dbContext.SaveChangesAsync();
        }

        private async Task ValidateAsync(
            Guid productId,
            CreateProductVariantRequestDto request,
            Guid? currentId)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new BusinessException("عنوان تنوع محصول الزامی است.");

            if (request.Price < 0)
                throw new BusinessException("قیمت تنوع محصول معتبر نیست.");

            if (request.DiscountPrice.HasValue && request.DiscountPrice.Value < 0)
                throw new BusinessException("قیمت تخفیف معتبر نیست.");

            if (request.DiscountPrice.HasValue && request.DiscountPrice.Value > request.Price)
                throw new BusinessException("قیمت تخفیف نمی‌تواند بیشتر از قیمت اصلی باشد.");

            var productExists = await _dbContext.Products.AnyAsync(x =>
                x.Id == productId &&
                !x.IsDeleted);

            if (!productExists)
                throw new NotFoundException("محصول یافت نشد.");

            if (!string.IsNullOrWhiteSpace(request.Sku))
            {
                var normalizedSku = request.Sku.Trim();

                var skuExists = await _dbContext.ProductVariants.AnyAsync(x =>
                    x.Sku == normalizedSku &&
                    (!currentId.HasValue || x.Id != currentId.Value));

                if (skuExists)
                    throw new BusinessException("این SKU قبلاً ثبت شده است.");
            }
        }

        private async Task ClearDefaultVariantsAsync(Guid productId, Guid? exceptVariantId)
        {
            var defaultVariants = await _dbContext.ProductVariants
                .Where(x =>
                    x.ProductId == productId &&
                    x.IsDefault &&
                    (!exceptVariantId.HasValue || x.Id != exceptVariantId.Value))
                .ToListAsync();

            foreach (var item in defaultVariants)
            {
                item.IsDefault = false;
                item.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}