using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.Json;
using Vitorize.Application.Common;
using Vitorize.Application.Cart;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.DTOs.Products;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;
using Vitorize.Shared.Enums;

namespace Vitorize.Infrastructure.Services;

public class CartService : ICartService
{
    private readonly VitorizeDbContext _dbContext;
    private readonly IEncryptionService _encryptionService;
    private readonly IVatSettingsProvider _vatSettingsProvider;

    public CartService(VitorizeDbContext dbContext, IEncryptionService encryptionService,
        IVatSettingsProvider vatSettingsProvider)
    {
        _dbContext = dbContext;
        _encryptionService = encryptionService;
        _vatSettingsProvider = vatSettingsProvider;
    }

    public Task<CartDto> GetAsync(Guid userId) => GetAsync(CartIdentity.ForUser(userId));

    public async Task<CartDto> GetAsync(CartIdentity identity)
    {
        EnsureIdentity(identity);
        // Retain the established authenticated-cart initialization behavior, but avoid
        // creating an empty database row for every newly provisioned guest cookie.
        var cart = identity.IsAuthenticated
            ? await GetOrCreateCartAsync(identity)
            : await LoadCartOrDefaultAsync(identity);
        if (cart is null) return new CartDto { UserId = identity.UserId };
        // A guest-cart read must remain read-only. Touching LastActivityAt here turned an
        // otherwise concurrent GET into a write that could deadlock with the serializable,
        // application-lock-protected cart mutation path. Guest activity is updated by the
        // actual add/update/remove/clear mutations instead.
        return MapToDto(cart, await _vatSettingsProvider.GetAsync());
    }

    public Task<CartDto> AddItemAsync(Guid userId, AddToCartRequestDto request) =>
        AddItemAsync(CartIdentity.ForUser(userId), request);

    /// <summary>
    /// Managed inventory is validated on cart writes but never reserved — carts must not hold units,
    /// so stock is consumed only at authoritative payment success. Instant delivery is excluded here:
    /// its availability is the gift-code pool, reserved under lock during checkout.
    /// </summary>
    private static void EnsureManagedStockAllows(Product product, ProductVariant? variant, int resultingQuantity)
    {
        if (variant is null) return;
        if (ProductAvailabilityRules.IsGiftCodeDriven(product.DeliveryType)) return;
        if (resultingQuantity <= variant.StockQuantity) return;

        throw new BusinessException(variant.StockQuantity == 0
            ? $"محصول «{product.Title}» در حال حاضر ناموجود است."
            : $"موجودی «{product.Title}» کافی نیست؛ حداکثر {variant.StockQuantity} عدد قابل سفارش است.");
    }

    public async Task<CartDto> AddItemAsync(CartIdentity identity, AddToCartRequestDto request)
    {
        EnsureIdentity(identity);
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
        else if (!ProductAvailabilityRules.IsGiftCodeDriven(product.DeliveryType))
        {
            // Managed-stock purchases are SKU-scoped: a request without a variant resolves the
            // product's single (implicit default) SKU server-side, so stock validation at add,
            // checkout revalidation and paid-time consumption all see a concrete variant.
            // Instant products stay variant-optional because their gift codes are product-scoped.
            var actives = product.ProductVariants.Where(x => x.IsActive).ToList();
            variant = actives.Count == 1
                ? actives[0]
                : throw new BusinessException(actives.Count == 0
                    ? "این محصول در حال حاضر قابل خرید نیست."
                    : "لطفاً نسخه محصول را انتخاب کنید.");
        }
        var resolvedVariantId = variant?.Id ?? request.ProductVariantId;

        var unitPrice = variant is null
            ? ResolveFinalPrice(product.BasePrice, product.DiscountPrice)
            : ResolveFinalPrice(variant.Price, variant.DiscountPrice);
        if (!Enum.IsDefined(typeof(CurrencyType), product.CurrencyType))
            throw new BusinessException("واحد پول محصول معتبر نیست.");
        // Adding to the cart never depends on product information: it is collected at Checkout.
        var values = ValidateInputs(product.ProductInputFields, request.InputValues,
            includeAllStages: true, enforceRequired: false);
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
                await SqlServerTransactionLock.AcquireAsync(_dbContext, OwnerLockKey(identity));

            var cart = await GetOrCreateCartAsync(identity);
            TouchGuestCart(cart, identity);
            if (cart.CartItems.Any(x => x.CurrencyType != product.CurrencyType))
                throw new BusinessException("سبد خرید نمی‌تواند شامل کالاهایی با واحد پول متفاوت باشد.");
            var existing = cart.CartItems.FirstOrDefault(x => x.ProductId == request.ProductId &&
                x.ProductVariantId == resolvedVariantId && x.InputFingerprint == fingerprint);

            // Validate the RESULTING total for this SKU, not just the delta: the same variant can sit
            // on several lines with different custom-input fingerprints and together they must still
            // fit within stock.
            var resultingQuantity = cart.CartItems
                .Where(x => x.ProductVariantId == resolvedVariantId)
                .Sum(x => x.Quantity) + request.Quantity;
            EnsureManagedStockAllows(product, variant, resultingQuantity);

            if (existing is not null)
            {
                existing.Quantity += request.Quantity;
                existing.UnitPrice = unitPrice;
                existing.CurrencyType = product.CurrencyType;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var item = new CartItem
                {
                    Id = Guid.NewGuid(), CartId = cart.Id, ProductId = request.ProductId,
                    ProductVariantId = resolvedVariantId, InputFingerprint = fingerprint,
                    Quantity = request.Quantity, UnitPrice = unitPrice, CurrencyType = product.CurrencyType,
                    CreatedAt = DateTime.UtcNow
                };
                AddInputValues(item, product.ProductInputFields, values);
                await _dbContext.CartItems.AddAsync(item);
            }

            await _dbContext.SaveChangesAsync();
            if (transaction is not null)
                await transaction.CommitAsync();
            return MapToDto(await LoadCartAsync(identity), await _vatSettingsProvider.GetAsync());
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync();
            throw;
        }
    }

    public Task<CartDto> UpdateItemAsync(Guid userId, Guid cartItemId, UpdateCartItemRequestDto request) =>
        UpdateItemAsync(CartIdentity.ForUser(userId), cartItemId, request);

    public async Task<CartDto> UpdateItemAsync(CartIdentity identity, Guid cartItemId, UpdateCartItemRequestDto request)
    {
        EnsureIdentity(identity);
        if (cartItemId == Guid.Empty) throw new BusinessException("آیتم سبد خرید معتبر نیست.");
        if (request.Quantity <= 0) throw new BusinessException("تعداد باید بیشتر از صفر باشد.");

        var isRelational = _dbContext.Database.IsRelational();
        await using var transaction = isRelational
            ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;
        try
        {
        if (isRelational)
            await SqlServerTransactionLock.AcquireAsync(_dbContext, OwnerLockKey(identity));

        var item = await _dbContext.CartItems
            .Include(x => x.Cart).Include(x => x.InputValues)
            .Include(x => x.Product).ThenInclude(x => x.ProductInputFields.Where(f => f.IsActive))
            .FirstOrDefaultAsync(x => x.Id == cartItemId &&
                ((identity.IsAuthenticated && x.Cart.UserId == identity.UserId) ||
                 (identity.IsGuest && x.Cart.GuestTokenHash == identity.GuestTokenHash)))
            ?? throw new NotFoundException("آیتم سبد خرید یافت نشد.");

        if (item.ProductVariantId.HasValue)
        {
            var variant = await _dbContext.ProductVariants.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == item.ProductVariantId.Value);

            // Resulting total for this SKU across the cart: the edited line's new quantity plus every
            // other line that shares the variant.
            var otherLines = await _dbContext.CartItems
                .Where(x => x.CartId == item.CartId && x.Id != item.Id &&
                            x.ProductVariantId == item.ProductVariantId)
                .SumAsync(x => (int?)x.Quantity) ?? 0;

            EnsureManagedStockAllows(item.Product, variant, otherLines + request.Quantity);
        }

        item.Quantity = request.Quantity;
        item.UpdatedAt = DateTime.UtcNow;
        if (request.InputValues is not null)
        {
            var values = ValidateInputs(item.Product.ProductInputFields, request.InputValues,
                includeAllStages: true, enforceRequired: false);
            item.InputFingerprint = ProductInputRules.Fingerprint(values);
            SyncInputValues(item, item.Product.ProductInputFields, values);
        }
        TouchGuestCart(item.Cart, identity);
        await _dbContext.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return MapToDto(await LoadCartAsync(identity), await _vatSettingsProvider.GetAsync());
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    public Task<CartDto> RemoveItemAsync(Guid userId, Guid cartItemId) =>
        RemoveItemAsync(CartIdentity.ForUser(userId), cartItemId);

    public async Task<CartDto> RemoveItemAsync(CartIdentity identity, Guid cartItemId)
    {
        EnsureIdentity(identity);
        var item = await _dbContext.CartItems.Include(x => x.Cart)
            .FirstOrDefaultAsync(x => x.Id == cartItemId &&
                ((identity.IsAuthenticated && x.Cart.UserId == identity.UserId) ||
                 (identity.IsGuest && x.Cart.GuestTokenHash == identity.GuestTokenHash)))
            ?? throw new NotFoundException("آیتم سبد خرید یافت نشد.");
        TouchGuestCart(item.Cart, identity);
        _dbContext.CartItems.Remove(item);
        await _dbContext.SaveChangesAsync();
        return MapToDto(await LoadCartAsync(identity), await _vatSettingsProvider.GetAsync());
    }

    public Task ClearAsync(Guid userId) => ClearAsync(CartIdentity.ForUser(userId));

    public async Task ClearAsync(CartIdentity identity)
    {
        EnsureIdentity(identity);
        var cart = await LoadCartOrDefaultAsync(identity);
        if (cart is null) return;
        if (cart.CartItems.Count == 0) return;
        TouchGuestCart(cart, identity);
        _dbContext.CartItems.RemoveRange(cart.CartItems);
        await _dbContext.SaveChangesAsync();
    }

    private async Task<Cart> GetOrCreateCartAsync(CartIdentity identity)
    {
        var cart = await LoadCartOrDefaultAsync(identity);
        if (cart is not null) return cart;
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        if (!_dbContext.Database.IsRelational())
        {
            cart = new Cart { Id = id, UserId = identity.UserId, GuestTokenHash = identity.GuestTokenHash, CreatedAt = createdAt, LastActivityAt = identity.IsGuest ? createdAt : null };
            await _dbContext.Carts.AddAsync(cart);
            await _dbContext.SaveChangesAsync();
            return cart;
        }

        // Multiple interactive components hydrate concurrently after authentication. A
        // serializable key-range lock makes the create-if-missing operation atomic and
        // avoids using a unique-index exception as normal control flow.
        await _dbContext.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO dbo.Carts (Id, UserId, GuestTokenHash, CreatedAt, LastActivityAt)
            SELECT {id}, {identity.UserId}, {identity.GuestTokenHash}, {createdAt}, {(identity.IsGuest ? createdAt : (DateTime?)null)}
            WHERE NOT EXISTS
            (
                SELECT 1 FROM dbo.Carts WITH (UPDLOCK, HOLDLOCK)
                WHERE ({identity.UserId} IS NOT NULL AND UserId = {identity.UserId})
                   OR ({identity.GuestTokenHash} IS NOT NULL AND GuestTokenHash = {identity.GuestTokenHash})
            );");
        return await LoadCartAsync(identity);
    }

    private async Task<Cart> LoadCartAsync(CartIdentity identity) =>
        await LoadCartOrDefaultAsync(identity) ?? throw new NotFoundException("سبد خرید یافت نشد.");

    private Task<Cart?> LoadCartOrDefaultAsync(CartIdentity identity) => _dbContext.Carts
        .Include(x => x.CartItems).ThenInclude(x => x.Product).ThenInclude(x => x.ProductInputFields.Where(f => f.IsActive))
        .Include(x => x.CartItems).ThenInclude(x => x.ProductVariant)
        .Include(x => x.CartItems).ThenInclude(x => x.InputValues)
        .FirstOrDefaultAsync(x => (identity.IsAuthenticated && x.UserId == identity.UserId) ||
                                  (identity.IsGuest && x.GuestTokenHash == identity.GuestTokenHash));

    public async Task<CartDto> MergeGuestCartAsync(Guid userId, string guestToken)
    {
        if (userId == Guid.Empty || !GuestCartToken.IsWellFormed(guestToken))
            throw new BusinessException("سبد خرید مهمان معتبر نیست.");

        var guest = CartIdentity.ForGuest(GuestCartToken.Hash(guestToken));
        var user = CartIdentity.ForUser(userId);
        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        try
        {
            if (_dbContext.Database.IsRelational())
            {
                await SqlServerTransactionLock.AcquireAsync(_dbContext, OwnerLockKey(guest));
                await SqlServerTransactionLock.AcquireAsync(_dbContext, OwnerLockKey(user));
            }

            var guestCart = await LoadCartOrDefaultAsync(guest);
            if (guestCart is null) return await GetAsync(user);
            var userCart = await LoadCartOrDefaultAsync(user);
            if (userCart is null)
            {
                guestCart.UserId = userId;
                guestCart.GuestTokenHash = null;
                guestCart.LastActivityAt = null;
                guestCart.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                if (transaction is not null) await transaction.CommitAsync();
                return MapToDto(await LoadCartAsync(user), await _vatSettingsProvider.GetAsync());
            }

            foreach (var guestItem in guestCart.CartItems.ToList())
            {
                var existing = userCart.CartItems.FirstOrDefault(item =>
                    item.ProductId == guestItem.ProductId && item.ProductVariantId == guestItem.ProductVariantId &&
                    item.InputFingerprint == guestItem.InputFingerprint);
                if (existing is not null)
                {
                    existing.Quantity += guestItem.Quantity;
                    existing.UpdatedAt = DateTime.UtcNow;
                    _dbContext.CartItems.Remove(guestItem);
                }
                else
                {
                    guestItem.CartId = userCart.Id;
                    userCart.CartItems.Add(guestItem);
                }
            }
            _dbContext.Carts.Remove(guestCart);
            await _dbContext.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();
            return MapToDto(await LoadCartAsync(user), await _vatSettingsProvider.GetAsync());
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    private static void EnsureIdentity(CartIdentity identity)
    {
        if (!identity.IsAuthenticated && !identity.IsGuest)
            throw new UnauthorizedException("شناسه سبد خرید معتبر نیست.");
    }

    private static string OwnerLockKey(CartIdentity identity) => identity.IsAuthenticated
        ? $"cart:user:{identity.UserId!.Value:N}"
        : $"cart:guest:{identity.GuestTokenHash}";

    private static void TouchGuestCart(Cart cart, CartIdentity identity)
    {
        if (identity.IsGuest) cart.LastActivityAt = DateTime.UtcNow;
    }

    private static decimal ResolveFinalPrice(decimal basePrice, decimal? discountPrice) =>
        discountPrice is > 0 && discountPrice < basePrice ? discountPrice.Value : basePrice;

    private static CartDto MapToDto(Cart cart, VatSettingsSnapshot vat)
    {
        var items = cart.CartItems.OrderBy(x => x.CreatedAt).Select(x =>
        {
            var kyc = KycRequirementEvaluator.Evaluate(
                x.Product.RequiresVerification,
                x.Product.KycRequirementMode,
                x.Product.KycThresholdAmount,
                x.Product.KycPolicyVersionId,
                x.UnitPrice,
                x.Quantity);
            return new CartItemDto
            {
                Id = x.Id, ProductId = x.ProductId, ProductVariantId = x.ProductVariantId,
                ProductTitle = x.Product.Title, VariantTitle = x.ProductVariant?.Title,
                ThumbnailImagePath = x.Product.ThumbnailImagePath, Quantity = x.Quantity,
                UnitPrice = x.UnitPrice, TotalPrice = x.UnitPrice * x.Quantity, CurrencyType = x.CurrencyType,
                RequiresKyc = kyc.RequiresKyc, KycRequirementMode = (byte)kyc.Mode,
                KycThresholdAmount = kyc.ThresholdAmount, KycEvaluatedAmount = kyc.EvaluatedAmount,
                KycPolicyVersionId = kyc.PolicyVersionId,
                InputFields = x.Product.ProductInputFields.Where(f => f.IsActive).OrderBy(f => f.SortOrder).ThenBy(f => f.Id)
                    .Select(ToDefinitionDto).ToList(),
                InputValues = x.InputValues.OrderBy(v => v.FieldKey).Select(v => new ProductInputValueDto
                {
                    Id = v.Id, ProductInputFieldId = v.ProductInputFieldId, FieldKey = v.FieldKey,
                    FieldLabel = v.FieldLabel, FieldType = v.FieldType,
                    Value = v.IsSensitive ? ProductInputRules.Mask(null) : v.Value,
                    IsSensitive = v.IsSensitive, IsMasked = v.IsSensitive
                }).ToList()
            };
        }).ToList();
        // Preview only. The cart carries no coupon, so the discount is zero here and the
        // authoritative decomposition is recalculated by CheckoutService at order creation.
        var pricing = OrderPricingCalculator.Calculate(items.Sum(x => x.TotalPrice), 0m, vat);
        return new CartDto
        {
            Id = cart.Id, UserId = cart.UserId, Items = items,
            TotalQuantity = items.Sum(x => x.Quantity), SubtotalAmount = pricing.SubtotalAmount,
            DiscountAmount = pricing.DiscountAmount, VatEnabled = pricing.VatEnabled,
            VatRatePercent = pricing.VatRatePercent, VatCalculationMode = (byte)pricing.VatCalculationMode,
            VatTaxableAmount = pricing.VatTaxableAmount, VatAmount = pricing.VatAmount,
            FinalAmount = pricing.FinalAmount,
            CurrencyType = items.Select(x => (byte?)x.CurrencyType).Distinct().SingleOrDefault()
        };
    }

    /// <param name="enforceRequired">
    /// The cart parks whatever the customer has supplied so far; Checkout is where product
    /// information is actually collected and where the required rule is enforced.
    /// </param>
    internal static Dictionary<string, string?> ValidateInputs(
        IEnumerable<ProductInputField> definitions,
        IReadOnlyDictionary<string, string?>? supplied,
        bool includeAllStages,
        bool enforceRequired = true)
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
            result[definition.Key] = ProductInputRules.ValidateValue(ToDefinitionDto(definition), value, enforceRequired);
        }
        return result;
    }

    private void AddInputValues(CartItem item, IEnumerable<ProductInputField> definitions,
        IReadOnlyDictionary<string, string?> values)
    {
        foreach (var field in definitions.Where(x => values.ContainsKey(x.Key)))
        {
            var value = values[field.Key];
            var inputValue = new CartItemInputValue
            {
                Id = Guid.NewGuid(), CartItemId = item.Id, ProductInputFieldId = field.Id, FieldKey = field.Key,
                FieldLabel = field.Label, FieldType = field.FieldType,
                Value = field.IsSensitive ? null : value,
                EncryptedValue = field.IsSensitive && value is not null ? _encryptionService.Encrypt(value) : null,
                IsSensitive = field.IsSensitive, CreatedAt = DateTime.UtcNow
            };
            item.InputValues.Add(inputValue);
            _dbContext.CartItemInputValues.Add(inputValue);
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
