using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Admin.ProductVariants;
using Vitorize.Application.DTOs.Admin.Products;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;
using Vitorize.Shared.Enums;

namespace Vitorize.Infrastructure.Services
{
    public class AdminProductVariantService : IAdminProductVariantService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUser;

        private const byte GiftCodeStatusAvailable = 0;

        public AdminProductVariantService(
            VitorizeDbContext dbContext,
            IAuditService auditService,
            ICurrentUserService currentUser)
        {
            _dbContext = dbContext;
            _auditService = auditService;
            _currentUser = currentUser;
        }

        /// <summary>Delivery type of the owning product; decides which inventory regime applies.</summary>
        private async Task<byte> GetDeliveryTypeAsync(Guid productId) =>
            await _dbContext.Products
                .Where(x => x.Id == productId)
                .Select(x => x.DeliveryType)
                .FirstOrDefaultAsync();

        /// <summary>
        /// Server-side floor for managed inventory. The database also carries a CHECK constraint, so a
        /// negative value cannot reach storage even if a caller bypasses this.
        /// </summary>
        private static int NormalizeStockQuantity(int requested) =>
            requested < 0
                ? throw new BusinessException("موجودی نمی‌تواند منفی باشد.")
                : requested;

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
                    StockQuantity = x.StockQuantity,
                    IsDefault = x.IsDefault,
                    IsActive = x.IsActive,
                    SortOrder = x.SortOrder,
                    AvailableStock = _dbContext.GiftCodes.Count(g =>
                        g.ProductVariantId == x.Id &&
                        g.Status == GiftCodeStatusAvailable)
                })
                .ToListAsync();
        }

        public async Task<Vitorize.Shared.Common.PagedResult<AdminProductVariantDto>> GetPagedByProductIdAsync(
            Guid productId, ProductDetailFilterDto filter, CancellationToken cancellationToken = default)
        {
            filter ??= new ProductDetailFilterDto();
            if (!await _dbContext.Products.AsNoTracking().AnyAsync(x => x.Id == productId && !x.IsDeleted, cancellationToken))
                throw new NotFoundException("محصول یافت نشد.");
            var page = Math.Max(1, filter.PageNumber ?? filter.Page);
            var pageSize = filter.PageSize <= 0 ? 25 : Math.Min(filter.PageSize, 100);
            var query = _dbContext.ProductVariants.AsNoTracking().Where(x => x.ProductId == productId);
            var totalCount = await query.CountAsync(cancellationToken);
            query = string.Equals(filter.SortDirection, "desc", StringComparison.OrdinalIgnoreCase)
                ? query.OrderByDescending(x => x.SortOrder).ThenByDescending(x => x.Title).ThenBy(x => x.Id)
                : query.OrderBy(x => x.SortOrder).ThenBy(x => x.Title).ThenBy(x => x.Id);
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new AdminProductVariantDto
            {
                Id = x.Id, ProductId = x.ProductId, ProductTitle = x.Product.Title, Title = x.Title,
                Sku = x.Sku, Price = x.Price, DiscountPrice = x.DiscountPrice, Value = x.Value,
                StockMode = x.StockMode, StockQuantity = x.StockQuantity, IsDefault = x.IsDefault, IsActive = x.IsActive, SortOrder = x.SortOrder,
                AvailableStock = _dbContext.GiftCodes.Count(g => g.ProductVariantId == x.Id && g.Status == GiftCodeStatusAvailable)
            }).ToListAsync(cancellationToken);
            return new Vitorize.Shared.Common.PagedResult<AdminProductVariantDto>
                { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
        }

        public async Task<List<AdminProductVariantLookupDto>> GetLookupByProductIdAsync(
            Guid productId, string? search, Guid? selectedId, CancellationToken cancellationToken = default)
        {
            if (!await _dbContext.Products.AsNoTracking().AnyAsync(x => x.Id == productId && !x.IsDeleted, cancellationToken))
                throw new NotFoundException("محصول یافت نشد.");

            var normalizedSearch = search?.Trim();
            var query = _dbContext.ProductVariants.AsNoTracking().Where(x => x.ProductId == productId);
            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = selectedId.HasValue && selectedId.Value != Guid.Empty
                    ? query.Where(x => x.Id == selectedId || x.Title.Contains(normalizedSearch) || (x.Sku != null && x.Sku.Contains(normalizedSearch)))
                    : query.Where(x => x.Title.Contains(normalizedSearch) || (x.Sku != null && x.Sku.Contains(normalizedSearch)));
            }

            return await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Title).ThenBy(x => x.Id)
                .Take(100)
                .Select(x => new AdminProductVariantLookupDto { Id = x.Id, Title = x.Title, Sku = x.Sku })
                .ToListAsync(cancellationToken);
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
                    StockQuantity = x.StockQuantity,
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

            var deliveryType = await GetDeliveryTypeAsync(productId);

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
                // The product's delivery type — not the caller — decides the inventory regime, so an
                // Instant variant can never be given a manual quantity that claims stock the gift-code
                // pool cannot deliver.
                // Delivery type decides the regime; within managed delivery the administrator may
                // choose a counted quantity or Unlimited, and that choice is preserved.
                StockMode = (byte)ProductAvailabilityRules.NormalizeStockMode(
                    deliveryType, (ProductVariantStockMode)request.StockMode),
                StockQuantity = ProductAvailabilityRules.IsManagedStock(deliveryType)
                    ? NormalizeStockQuantity(request.StockQuantity)
                    : 0,
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

            var deliveryType = await GetDeliveryTypeAsync(variant.ProductId);
            var managed = ProductAvailabilityRules.IsManagedStock(deliveryType);
            var newStockMode = (byte)ProductAvailabilityRules.NormalizeStockMode(
                deliveryType, (ProductVariantStockMode)request.StockMode);

            if (newStockMode != variant.StockMode)
            {
                // The inventory policy decides whether a paid order consumes units at all, so a
                // change of policy is an inventory event in its own right.
                await _auditService.LogAsync(
                    _currentUser.UserId ?? Guid.Empty,
                    "ProductVariantStockModeChanged",
                    nameof(ProductVariant),
                    variant.Id.ToString(),
                    $"variant:{variant.Title}; from:{(ProductVariantStockMode)variant.StockMode}; to:{(ProductVariantStockMode)newStockMode}",
                    _currentUser.IpAddress,
                    _currentUser.UserAgent);
            }
            variant.StockMode = newStockMode;

            // Instant delivery draws availability from gift codes and ProductAvailabilityRules never
            // reads StockQuantity for it, so a dormant value cannot make an Instant variant sellable.
            // We therefore PRESERVE it rather than zeroing: a product flipped to Instant and back
            // would otherwise lose real inventory permanently, and the admin form does not even post
            // a quantity in Instant mode, so "0" here would mean "erase" rather than "unchanged".
            // Unlimited ignores StockQuantity for availability, but the number is still recorded so
            // it is there when the administrator switches back to a counted quantity. The editor
            // posts whatever it displayed even while the input was disabled, so accepting it keeps
            // the stored value equal to what the administrator actually saw - discarding it would
            // silently drop an edit made in the same submission.
            //
            // Instant is the one case that preserves instead of accepting: its form shows gift-code
            // stock rather than a quantity field, so a posted 0 there would mean "erase" rather than
            // "unchanged".
            var newQuantity = managed ? NormalizeStockQuantity(request.StockQuantity) : variant.StockQuantity;
            if (newQuantity != variant.StockQuantity)
            {
                await _auditService.LogAsync(
                    _currentUser.UserId ?? Guid.Empty,
                    "ProductVariantStockChanged",
                    nameof(ProductVariant),
                    variant.Id.ToString(),
                    $"variant:{variant.Title}; from:{variant.StockQuantity}; to:{newQuantity}; delta:{newQuantity - variant.StockQuantity}",
                    _currentUser.IpAddress,
                    _currentUser.UserAgent);
            }
            variant.StockQuantity = newQuantity;

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
