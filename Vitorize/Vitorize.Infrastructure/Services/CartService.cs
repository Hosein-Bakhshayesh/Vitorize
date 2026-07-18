using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.Json;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.DTOs.Products;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services;

public class CartService : ICartService
{
    private readonly VitorizeDbContext _dbContext;
    private readonly IEncryptionService _encryptionService;

    public CartService(VitorizeDbContext dbContext, IEncryptionService encryptionService)
    {
        _dbContext = dbContext;
        _encryptionService = encryptionService;
    }

    public async Task<CartDto> GetAsync(Guid userId) => MapToDto(await GetOrCreateCartAsync(userId));

    public async Task<CartDto> AddItemAsync(Guid userId, AddToCartRequestDto request)
    {
        if (userId == Guid.Empty) throw new UnauthorizedException("کاربر احراز هویت نشده است.");
        if (request.ProductId == Guid.Empty) throw new BusinessException("محصول الزامی است.");
        if (request.Quantity <= 0) throw new BusinessException("تعداد باید بیشتر از صفر باشد.");

        var product = await _dbContext.Products.AsNoTracking()
            .Include(x => x.ProductVariants)
            .Include(x => x.ProductInputFields.Where(f => f.IsActive))
            .FirstOrDefaultAsync(x => x.Id == request.ProductId && x.IsActive && !x.IsDeleted)
            ?? throw new BusinessException("محصول معتبر نیست.");

        ProductVariant? variant = null;
        if (request.ProductVariantId.HasValue)
        {
            variant = product.ProductVariants.FirstOrDefault(x => x.Id == request.ProductVariantId && x.IsActive)
                ?? throw new BusinessException("تنوع محصول معتبر نیست.");
        }

        var unitPrice = variant is null
            ? ResolveFinalPrice(product.BasePrice, product.DiscountPrice)
            : ResolveFinalPrice(variant.Price, variant.DiscountPrice);
        var values = ValidateInputs(product.ProductInputFields, request.InputValues, includeAllStages: false);
        var fingerprint = ProductInputRules.Fingerprint(values);

        // Concurrent identical add-to-cart calls race between reading the existing line and inserting a
        // new one, which produced duplicate cart lines instead of merging (Phase 4 regression). Serialize
        // the read-modify-write per user with the same transaction-scoped application lock the wallet and
        // coupon services use so identical items merge deterministically.
        var isRelational = _dbContext.Database.IsRelational();
        var hasAmbientTransaction = _dbContext.Database.CurrentTransaction is not null;
        await using var transaction = isRelational && !hasAmbientTransaction
            ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;
        try
        {
            if (isRelational)
                await SqlServerTransactionLock.AcquireAsync(_dbContext, $"cart:user:{userId:N}");

            var cart = await GetOrCreateCartAsync(userId);
            var existing = cart.CartItems.FirstOrDefault(x => x.ProductId == request.ProductId &&
                x.ProductVariantId == request.ProductVariantId && x.InputFingerprint == fingerprint);

            if (existing is not null)
            {
                existing.Quantity += request.Quantity;
                existing.UnitPrice = unitPrice;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var item = new CartItem
                {
                    Id = Guid.NewGuid(), CartId = cart.Id, ProductId = request.ProductId,
                    ProductVariantId = request.ProductVariantId, InputFingerprint = fingerprint,
                    Quantity = request.Quantity, UnitPrice = unitPrice, CreatedAt = DateTime.UtcNow
                };
                AddInputValues(item, product.ProductInputFields, values);
                await _dbContext.CartItems.AddAsync(item);
            }

            await _dbContext.SaveChangesAsync();
            if (transaction is not null)
                await transaction.CommitAsync();
            return MapToDto(await LoadCartAsync(userId));
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<CartDto> UpdateItemAsync(Guid userId, Guid cartItemId, UpdateCartItemRequestDto request)
    {
        if (userId == Guid.Empty) throw new UnauthorizedException("کاربر احراز هویت نشده است.");
        if (cartItemId == Guid.Empty) throw new BusinessException("آیتم سبد خرید معتبر نیست.");
        if (request.Quantity <= 0) throw new BusinessException("تعداد باید بیشتر از صفر باشد.");

        var item = await _dbContext.CartItems
            .Include(x => x.Cart).Include(x => x.InputValues)
            .Include(x => x.Product).ThenInclude(x => x.ProductInputFields.Where(f => f.IsActive))
            .FirstOrDefaultAsync(x => x.Id == cartItemId && x.Cart.UserId == userId)
            ?? throw new NotFoundException("آیتم سبد خرید یافت نشد.");

        item.Quantity = request.Quantity;
        item.UpdatedAt = DateTime.UtcNow;
        if (request.InputValues is not null)
        {
            var values = ValidateInputs(item.Product.ProductInputFields, request.InputValues, includeAllStages: true);
            item.InputFingerprint = ProductInputRules.Fingerprint(values);
            SyncInputValues(item, item.Product.ProductInputFields, values);
        }
        await _dbContext.SaveChangesAsync();
        return MapToDto(await LoadCartAsync(userId));
    }

    public async Task<CartDto> RemoveItemAsync(Guid userId, Guid cartItemId)
    {
        if (userId == Guid.Empty) throw new UnauthorizedException("کاربر احراز هویت نشده است.");
        var item = await _dbContext.CartItems.Include(x => x.Cart)
            .FirstOrDefaultAsync(x => x.Id == cartItemId && x.Cart.UserId == userId)
            ?? throw new NotFoundException("آیتم سبد خرید یافت نشد.");
        _dbContext.CartItems.Remove(item);
        await _dbContext.SaveChangesAsync();
        return MapToDto(await LoadCartAsync(userId));
    }

    public async Task ClearAsync(Guid userId)
    {
        if (userId == Guid.Empty) throw new UnauthorizedException("کاربر احراز هویت نشده است.");
        var cart = await LoadCartAsync(userId);
        if (cart.CartItems.Count == 0) return;
        _dbContext.CartItems.RemoveRange(cart.CartItems);
        await _dbContext.SaveChangesAsync();
    }

    private async Task<Cart> GetOrCreateCartAsync(Guid userId)
    {
        var cart = await LoadCartOrDefaultAsync(userId);
        if (cart is not null) return cart;
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        if (!_dbContext.Database.IsRelational())
        {
            cart = new Cart { Id = id, UserId = userId, CreatedAt = createdAt };
            await _dbContext.Carts.AddAsync(cart);
            await _dbContext.SaveChangesAsync();
            return cart;
        }

        // Multiple interactive components hydrate concurrently after authentication. A
        // serializable key-range lock makes the create-if-missing operation atomic and
        // avoids using a unique-index exception as normal control flow.
        await _dbContext.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO dbo.Carts (Id, UserId, CreatedAt)
            SELECT {id}, {userId}, {createdAt}
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM dbo.Carts WITH (UPDLOCK, HOLDLOCK)
                WHERE UserId = {userId}
            );");
        return await LoadCartAsync(userId);
    }

    private async Task<Cart> LoadCartAsync(Guid userId) =>
        await LoadCartOrDefaultAsync(userId) ?? throw new NotFoundException("سبد خرید یافت نشد.");

    private Task<Cart?> LoadCartOrDefaultAsync(Guid userId) => _dbContext.Carts
        .Include(x => x.CartItems).ThenInclude(x => x.Product).ThenInclude(x => x.ProductInputFields.Where(f => f.IsActive))
        .Include(x => x.CartItems).ThenInclude(x => x.ProductVariant)
        .Include(x => x.CartItems).ThenInclude(x => x.InputValues)
        .FirstOrDefaultAsync(x => x.UserId == userId);

    private static decimal ResolveFinalPrice(decimal basePrice, decimal? discountPrice) =>
        discountPrice is > 0 && discountPrice < basePrice ? discountPrice.Value : basePrice;

    private static CartDto MapToDto(Cart cart)
    {
        var items = cart.CartItems.OrderBy(x => x.CreatedAt).Select(x => new CartItemDto
        {
            Id = x.Id, ProductId = x.ProductId, ProductVariantId = x.ProductVariantId,
            ProductTitle = x.Product.Title, VariantTitle = x.ProductVariant?.Title,
            ThumbnailImagePath = x.Product.ThumbnailImagePath, Quantity = x.Quantity,
            UnitPrice = x.UnitPrice, TotalPrice = x.UnitPrice * x.Quantity,
            InputFields = x.Product.ProductInputFields.Where(f => f.IsActive).OrderBy(f => f.SortOrder).ThenBy(f => f.Id)
                .Select(ToDefinitionDto).ToList(),
            InputValues = x.InputValues.OrderBy(v => v.FieldKey).Select(v => new ProductInputValueDto
            {
                Id = v.Id, ProductInputFieldId = v.ProductInputFieldId, FieldKey = v.FieldKey,
                FieldLabel = v.FieldLabel, FieldType = v.FieldType,
                Value = v.IsSensitive ? ProductInputRules.Mask(null) : v.Value,
                IsSensitive = v.IsSensitive, IsMasked = v.IsSensitive
            }).ToList()
        }).ToList();
        return new CartDto
        {
            Id = cart.Id, UserId = cart.UserId, Items = items,
            TotalQuantity = items.Sum(x => x.Quantity), SubtotalAmount = items.Sum(x => x.TotalPrice)
        };
    }

    internal static Dictionary<string, string?> ValidateInputs(
        IEnumerable<ProductInputField> definitions,
        IReadOnlyDictionary<string, string?>? supplied,
        bool includeAllStages)
    {
        var active = definitions.Where(x => x.IsActive && (includeAllStages || x.DisplayStage == 1))
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList();
        var input = supplied ?? new Dictionary<string, string?>();
        var known = active.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (input.Keys.Any(x => !known.Contains(x)))
            throw new BusinessException("یکی از اطلاعات ارسال‌شده برای این محصول تعریف نشده است.");

        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var definition in active)
        {
            input.TryGetValue(definition.Key, out var value);
            result[definition.Key] = ProductInputRules.ValidateValue(ToDefinitionDto(definition), value);
        }
        return result;
    }

    private void AddInputValues(CartItem item, IEnumerable<ProductInputField> definitions,
        IReadOnlyDictionary<string, string?> values)
    {
        foreach (var field in definitions.Where(x => values.ContainsKey(x.Key)))
        {
            var value = values[field.Key];
            item.InputValues.Add(new CartItemInputValue
            {
                Id = Guid.NewGuid(), ProductInputFieldId = field.Id, FieldKey = field.Key,
                FieldLabel = field.Label, FieldType = field.FieldType,
                Value = field.IsSensitive ? null : value,
                EncryptedValue = field.IsSensitive && value is not null ? _encryptionService.Encrypt(value) : null,
                IsSensitive = field.IsSensitive, CreatedAt = DateTime.UtcNow
            });
        }
    }

    private void SyncInputValues(CartItem item, IEnumerable<ProductInputField> definitions,
        IReadOnlyDictionary<string, string?> values)
    {
        var removed = item.InputValues
            .Where(existing => !values.ContainsKey(existing.FieldKey))
            .ToList();
        if (removed.Count > 0)
            _dbContext.CartItemInputValues.RemoveRange(removed);

        foreach (var field in definitions.Where(x => values.ContainsKey(x.Key)))
        {
            var value = values[field.Key];
            var existing = item.InputValues.FirstOrDefault(x =>
                x.FieldKey.Equals(field.Key, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                AddInputValues(item, new[] { field }, values);
                continue;
            }

            existing.ProductInputFieldId = field.Id;
            existing.FieldLabel = field.Label;
            existing.FieldType = field.FieldType;
            existing.Value = field.IsSensitive ? null : value;
            existing.EncryptedValue = field.IsSensitive && value is not null
                ? _encryptionService.Encrypt(value)
                : null;
            existing.IsSensitive = field.IsSensitive;
            existing.UpdatedAt = DateTime.UtcNow;
        }
    }

    internal static ProductInputFieldDto ToDefinitionDto(ProductInputField field) => new()
    {
        Id = field.Id, Key = field.Key, Label = field.Label, Description = field.Description,
        Placeholder = field.Placeholder, FieldType = field.FieldType, IsRequired = field.IsRequired,
        Options = string.IsNullOrWhiteSpace(field.OptionsJson) ? new() : JsonSerializer.Deserialize<List<string>>(field.OptionsJson) ?? new(),
        DefaultValue = field.DefaultValue, MinLength = field.MinLength, MaxLength = field.MaxLength,
        ValidationPattern = field.ValidationPattern, ValidationMessage = field.ValidationMessage,
        IsSensitive = field.IsSensitive, RequiresConfirmation = field.RequiresConfirmation,
        DisplayStage = field.DisplayStage, SortOrder = field.SortOrder, IsActive = field.IsActive
    };
}
