using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class CartService : ICartService
    {
        private readonly VitorizeDbContext _dbContext;

        public CartService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<CartDto> GetAsync(Guid userId)
        {
            var cart = await GetOrCreateCartAsync(userId);
            return MapToDto(cart);
        }

        public async Task<CartDto> AddItemAsync(
            Guid userId,
            AddToCartRequestDto request)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            if (request.ProductId == Guid.Empty)
                throw new BusinessException("محصول الزامی است.");

            if (request.Quantity <= 0)
                throw new BusinessException("تعداد باید بیشتر از صفر باشد.");

            var product = await _dbContext.Products
                .AsNoTracking()
                .Include(x => x.ProductVariants)
                .FirstOrDefaultAsync(x =>
                    x.Id == request.ProductId &&
                    x.IsActive &&
                    !x.IsDeleted);

            if (product == null)
                throw new BusinessException("محصول معتبر نیست.");

            ProductVariant? variant = null;

            if (request.ProductVariantId.HasValue)
            {
                variant = product.ProductVariants.FirstOrDefault(x =>
                    x.Id == request.ProductVariantId.Value &&
                    x.IsActive);

                if (variant == null)
                    throw new BusinessException("تنوع محصول معتبر نیست.");
            }

            var unitPrice = variant != null
                ? ResolveFinalPrice(variant.Price, variant.DiscountPrice)
                : ResolveFinalPrice(product.BasePrice, product.DiscountPrice);

            var cart = await GetOrCreateCartAsync(userId);

            var existingItem = cart.CartItems.FirstOrDefault(x =>
                x.ProductId == request.ProductId &&
                x.ProductVariantId == request.ProductVariantId);

            if (existingItem != null)
            {
                existingItem.Quantity += request.Quantity;
                existingItem.UnitPrice = unitPrice;
                existingItem.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var newItem = new CartItem
                {
                    Id = Guid.NewGuid(),
                    CartId = cart.Id,
                    ProductId = request.ProductId,
                    ProductVariantId = request.ProductVariantId,
                    Quantity = request.Quantity,
                    UnitPrice = unitPrice,
                    CreatedAt = DateTime.UtcNow
                };

                await _dbContext.CartItems.AddAsync(newItem);
            }

            await _dbContext.SaveChangesAsync();

            var updatedCart = await LoadCartAsync(userId);
            return MapToDto(updatedCart);
        }

        public async Task<CartDto> UpdateItemAsync(
            Guid userId,
            Guid cartItemId,
            UpdateCartItemRequestDto request)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            if (cartItemId == Guid.Empty)
                throw new BusinessException("آیتم سبد خرید معتبر نیست.");

            if (request.Quantity <= 0)
                throw new BusinessException("تعداد باید بیشتر از صفر باشد.");

            var item = await _dbContext.CartItems
                .Include(x => x.Cart)
                .FirstOrDefaultAsync(x =>
                    x.Id == cartItemId &&
                    x.Cart.UserId == userId);

            if (item == null)
                throw new NotFoundException("آیتم سبد خرید یافت نشد.");

            item.Quantity = request.Quantity;
            item.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            var updatedCart = await LoadCartAsync(userId);
            return MapToDto(updatedCart);
        }

        public async Task<CartDto> RemoveItemAsync(
            Guid userId,
            Guid cartItemId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            if (cartItemId == Guid.Empty)
                throw new BusinessException("آیتم سبد خرید معتبر نیست.");

            var item = await _dbContext.CartItems
                .Include(x => x.Cart)
                .FirstOrDefaultAsync(x =>
                    x.Id == cartItemId &&
                    x.Cart.UserId == userId);

            if (item == null)
                throw new NotFoundException("آیتم سبد خرید یافت نشد.");

            _dbContext.CartItems.Remove(item);

            await _dbContext.SaveChangesAsync();

            var updatedCart = await LoadCartAsync(userId);
            return MapToDto(updatedCart);
        }

        public async Task ClearAsync(Guid userId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var cart = await LoadCartAsync(userId);

            if (!cart.CartItems.Any())
                return;

            _dbContext.CartItems.RemoveRange(cart.CartItems);

            await _dbContext.SaveChangesAsync();
        }

        private async Task<Cart> GetOrCreateCartAsync(Guid userId)
        {
            var cart = await LoadCartOrDefaultAsync(userId);

            if (cart != null)
                return cart;

            var newCart = new Cart
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.Carts.AddAsync(newCart);
            await _dbContext.SaveChangesAsync();

            return await LoadCartAsync(userId);
        }

        private async Task<Cart> LoadCartAsync(Guid userId)
        {
            var cart = await LoadCartOrDefaultAsync(userId);

            if (cart == null)
                throw new NotFoundException("سبد خرید یافت نشد.");

            return cart;
        }

        private async Task<Cart?> LoadCartOrDefaultAsync(Guid userId)
        {
            return await _dbContext.Carts
                .Include(x => x.CartItems)
                    .ThenInclude(x => x.Product)
                .Include(x => x.CartItems)
                    .ThenInclude(x => x.ProductVariant)
                .FirstOrDefaultAsync(x => x.UserId == userId);
        }

        private static decimal ResolveFinalPrice(decimal basePrice, decimal? discountPrice)
        {
            return discountPrice.HasValue &&
                   discountPrice.Value > 0 &&
                   discountPrice.Value < basePrice
                ? discountPrice.Value
                : basePrice;
        }

        private static CartDto MapToDto(Cart cart)
        {
            var items = cart.CartItems
                .OrderBy(x => x.CreatedAt)
                .Select(x => new CartItemDto
                {
                    Id = x.Id,
                    ProductId = x.ProductId,
                    ProductVariantId = x.ProductVariantId,
                    ProductTitle = x.Product.Title,
                    VariantTitle = x.ProductVariant?.Title,
                    ThumbnailImagePath = x.Product.ThumbnailImagePath,
                    Quantity = x.Quantity,
                    UnitPrice = x.UnitPrice,
                    TotalPrice = x.UnitPrice * x.Quantity
                })
                .ToList();

            return new CartDto
            {
                Id = cart.Id,
                UserId = cart.UserId,
                Items = items,
                TotalQuantity = items.Sum(x => x.Quantity),
                SubtotalAmount = items.Sum(x => x.TotalPrice)
            };
        }
    }
}