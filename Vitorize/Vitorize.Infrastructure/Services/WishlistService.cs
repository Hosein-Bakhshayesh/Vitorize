using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Wishlist;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;
using Vitorize.Shared.Enums;

namespace Vitorize.Infrastructure.Services
{
    public class WishlistService : IWishlistService
    {
        private const byte GiftCodeStatusAvailable = 0;
        private const byte DeliveryTypeInstant = 1;
        private const byte StockModeUnlimited = (byte)ProductVariantStockMode.Unlimited;

        private readonly VitorizeDbContext _dbContext;

        public WishlistService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<WishlistItemDto>> GetMyWishlistAsync(Guid userId)
        {
            EnsureUser(userId);

            return await _dbContext.WishLists
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId &&
                    x.Product.IsActive &&
                    !x.Product.IsDeleted)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new WishlistItemDto
                {
                    Id = x.Id,
                    ProductId = x.ProductId,
                    Title = x.Product.Title,
                    Slug = x.Product.Slug,
                    RedirectUrl = x.Product.RedirectUrl,
                    ThumbnailImagePath = x.Product.ThumbnailImagePath,
                    BasePrice = x.Product.BasePrice,
                    DiscountPrice = x.Product.DiscountPrice,
                    ProductType = x.Product.ProductType,
                    DeliveryType = x.Product.DeliveryType,
                    CurrencyType = x.Product.CurrencyType,
                    CategoryTitle = x.Product.Category.Title,
                    BrandTitle = x.Product.Brand != null ? x.Product.Brand.Title : null,
                    HasVariants = x.Product.ProductVariants.Any(v => v.IsActive),
                    // Instant is gift-code driven; every non-Instant mode uses managed variant stock.
                    // Previously Manual was hard-coded available, so wishlisted Manual products never
                    // showed as ناموجود.
                    // Same precedence as ProductAvailabilityRules: the administrator's override
                    // first, then Unlimited, then the inventory the regime actually counts.
                    IsAvailable = !x.Product.ForceOutOfStock &&
                        (x.Product.DeliveryType == DeliveryTypeInstant
                            ? _dbContext.GiftCodes.Any(g =>
                                g.ProductId == x.ProductId &&
                                g.Status == GiftCodeStatusAvailable)
                            : x.Product.ProductVariants.Any(v => v.IsActive &&
                                (v.StockMode == StockModeUnlimited || v.StockQuantity > 0))),
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<List<Guid>> GetMyWishlistProductIdsAsync(Guid userId)
        {
            EnsureUser(userId);

            return await _dbContext.WishLists
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.ProductId)
                .ToListAsync();
        }

        public async Task<int> GetMyWishlistCountAsync(Guid userId)
        {
            EnsureUser(userId);

            return await _dbContext.WishLists
                .AsNoTracking()
                .CountAsync(x =>
                    x.UserId == userId &&
                    x.Product.IsActive &&
                    !x.Product.IsDeleted);
        }

        public async Task<bool> ToggleAsync(Guid userId, Guid productId)
        {
            EnsureUser(userId);

            var existing = await _dbContext.WishLists
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.ProductId == productId);

            if (existing != null)
            {
                _dbContext.WishLists.Remove(existing);
                await _dbContext.SaveChangesAsync();

                return false;
            }

            var productExists = await _dbContext.Products
                .AnyAsync(x =>
                    x.Id == productId &&
                    x.IsActive &&
                    !x.IsDeleted);

            if (!productExists)
                throw new NotFoundException("محصول یافت نشد.");

            await _dbContext.WishLists.AddAsync(new WishList
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProductId = productId,
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();

            return true;
        }

        public async Task RemoveAsync(Guid userId, Guid productId)
        {
            EnsureUser(userId);

            var existing = await _dbContext.WishLists
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.ProductId == productId);

            if (existing == null)
                return;

            _dbContext.WishLists.Remove(existing);
            await _dbContext.SaveChangesAsync();
        }

        private static void EnsureUser(Guid userId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");
        }
    }
}
