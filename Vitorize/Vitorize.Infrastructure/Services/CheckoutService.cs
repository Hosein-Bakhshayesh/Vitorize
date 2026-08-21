using Microsoft.EntityFrameworkCore;
using System.Data;
using Vitorize.Application.DTOs.Checkout;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Shared.Enums;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Vitorize.Application.Common;
using Vitorize.Shared.Logging;

namespace Vitorize.Infrastructure.Services
{
    public class CheckoutService : ICheckoutService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly ICouponService _couponService;
        private readonly INotificationService _notificationService;
        private readonly IEncryptionService _encryptionService;
        private readonly IVatSettingsProvider _vatSettingsProvider;
        private readonly ILogger<CheckoutService> _logger;
        private readonly PaymentTimingOptions _paymentTiming;

        public CheckoutService(
            VitorizeDbContext dbContext,
            ICouponService couponService,
            INotificationService notificationService,
            IEncryptionService encryptionService,
            IVatSettingsProvider vatSettingsProvider,
            ILogger<CheckoutService>? logger = null,
            IOptions<PaymentTimingOptions>? paymentTiming = null)
        {
            _dbContext = dbContext;
            _couponService = couponService;
            _notificationService = notificationService;
            _encryptionService = encryptionService;
            _vatSettingsProvider = vatSettingsProvider;
            _logger = logger ?? NullLogger<CheckoutService>.Instance;
            _paymentTiming = paymentTiming?.Value ?? new PaymentTimingOptions();
        }

        public async Task<CheckoutResultDto> CheckoutAsync(
            Guid userId,
            CheckoutRequestDto request)
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation(
                "Checkout started for user {UserId}. EventType={EventType}",
                userId, OperationalEventNames.CheckoutStarted);
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            request ??= new CheckoutRequestDto();

            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                var now = DateTime.UtcNow;
                await SqlServerTransactionLock.AcquireAsync(_dbContext, $"checkout:user:{userId:N}");

                var user = await _dbContext.Users.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted)
                    ?? throw new UnauthorizedException("کاربر معتبر نیست.");
                if (user.Status != (byte)UserStatus.Active)
                    throw new BusinessException("حساب کاربری برای خرید فعال نیست.");

                // قفل انحصاری و به‌ازای هر محصول/تنوعِ تحویل‌آنی، پیش از هر خواندنِ قفل‌دارِ سبد.
                // شناسایی محصولات با هینت READCOMMITTEDLOCK انجام می‌شود تا این خواندن قفلی نگه ندارد؛
                // بنابراین Checkoutِ منتظرِ همان محصول، هیچ قفلِ متعارضی روی سبد نگه نمی‌دارد و
                // چرخهٔ بن‌بست (GiftCodes ⇄ CartItems) میان دو خرید هم‌زمانِ یک محصول شکل نمی‌گیرد.
                // کلید قفل هم‌راستا با GiftCodeReservationService است تا تخصیص کد در همهٔ مسیرها سریالی شود.
                var instantLockKeys = (await _dbContext.Database
                    .SqlQuery<string>($@"
                        SELECT DISTINCT
                            LOWER(REPLACE(CONVERT(varchar(36), ci.ProductId), '-', '')) + ':' +
                            ISNULL(LOWER(REPLACE(CONVERT(varchar(36), ci.ProductVariantId), '-', '')), 'none') AS Value
                        FROM CartItems AS ci WITH (READCOMMITTEDLOCK)
                        INNER JOIN Carts AS c WITH (READCOMMITTEDLOCK) ON c.Id = ci.CartId
                        INNER JOIN Products AS p WITH (READCOMMITTEDLOCK) ON p.Id = ci.ProductId
                        WHERE c.UserId = {userId} AND p.DeliveryType = {(byte)DeliveryType.Instant}")
                    .ToListAsync())
                    .Select(x => $"gift-reservation:{x}")
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToList();

                foreach (var lockKey in instantLockKeys)
                    await SqlServerTransactionLock.AcquireAsync(_dbContext, lockKey);

                // Cart prices are display caches; authoritative catalog state is reloaded and
                // repriced inside this serializable transaction.
                var cart = await _dbContext.Carts
                    .Include(x => x.CartItems).ThenInclude(x => x.Product).ThenInclude(x => x.KycPolicyVersion)
                    .Include(x => x.CartItems).ThenInclude(x => x.ProductVariant)
                    .Include(x => x.CartItems).ThenInclude(x => x.InputValues)
                    .Include(x => x.CartItems).ThenInclude(x => x.Product)
                        .ThenInclude(x => x.ProductInputFields.Where(f => f.IsActive))
                    .FirstOrDefaultAsync(x => x.UserId == userId);

                if (cart == null || !cart.CartItems.Any())
                    throw new BusinessException("سبد خرید خالی است.");

                foreach (var item in cart.CartItems)
                {
                    var product = item.Product;
                    if (!product.IsActive || product.IsDeleted)
                        throw new BusinessException($"محصول «{product.Title}» دیگر قابل خرید نیست.");
                    if (!Enum.IsDefined(typeof(DeliveryType), product.DeliveryType))
                        throw new BusinessException($"روش تحویل محصول «{product.Title}» معتبر نیست.");
                    if (!Enum.IsDefined(typeof(CurrencyType), product.CurrencyType))
                        throw new BusinessException($"واحد پول محصول «{product.Title}» معتبر نیست.");
                    if (item.Quantity < Math.Max(1, product.MinOrderQuantity) ||
                        (product.MaxOrderQuantity.HasValue && item.Quantity > product.MaxOrderQuantity.Value))
                        throw new BusinessException($"تعداد سفارش محصول «{product.Title}» خارج از محدوده مجاز است.");
                    if (item.ProductVariantId.HasValue)
                    {
                        var variant = item.ProductVariant;
                        if (variant is null || variant.ProductId != product.Id || !variant.IsActive)
                            throw new BusinessException($"تنوع انتخاب‌شده برای «{product.Title}» غیرفعال یا نامعتبر است.");
                        if (!Enum.IsDefined(typeof(ProductVariantStockMode), variant.StockMode))
                            throw new BusinessException($"نوع موجودی تنوع «{variant.Title}» معتبر نیست.");
                        if (product.DeliveryType == (byte)DeliveryType.Instant &&
                            variant.StockMode != (byte)ProductVariantStockMode.GiftCode)
                            throw new BusinessException($"تنوع «{variant.Title}» موجودی کد قابل تحویل ندارد.");
                        if (product.DeliveryType != (byte)DeliveryType.Instant &&
                            variant.StockMode == (byte)ProductVariantStockMode.GiftCode)
                            throw new BusinessException($"نوع موجودی تنوع «{variant.Title}» با روش تحویل محصول سازگار نیست.");
                        item.UnitPrice = ResolveFinalPrice(variant.Price, variant.DiscountPrice);
                    }
                    else
                    {
                        item.UnitPrice = ResolveFinalPrice(product.BasePrice, product.DiscountPrice);
                    }

                    if (item.UnitPrice < 0)
                        throw new BusinessException($"قیمت محصول «{product.Title}» معتبر نیست.");
                    item.CurrencyType = product.CurrencyType;

                    // The immutable OrderItem KYC snapshot is evaluated below. KYC governs
                    // post-payment fulfillment, not whether an authenticated customer may pay.
                }

                var currencies = cart.CartItems.Select(x => x.CurrencyType).Distinct().ToList();
                if (currencies.Count != 1)
                    throw new BusinessException("سبد خرید نمی‌تواند شامل کالاهایی با واحد پول متفاوت باشد.");
                var currencyType = currencies[0];

                var subtotalAmount = cart.CartItems.Sum(x =>
                    x.UnitPrice * x.Quantity);

                var discountAmount = 0m;
                Guid? couponId = null;

                if (!string.IsNullOrWhiteSpace(request.CouponCode))
                {
                    var couponResult = await _couponService.ValidateAsync(
                        userId,
                        new Vitorize.Application.DTOs.Coupons.ValidateCouponRequestDto
                        {
                            Code = request.CouponCode,
                            OrderAmount = subtotalAmount
                        });

                    couponId = couponResult.CouponId;
                    discountAmount = couponResult.DiscountAmount;
                }

                // VAT settings are read exactly once, here, inside the authoritative transaction.
                // The resulting snapshot is persisted on the order and is never re-read afterwards,
                // so later administrative changes cannot alter an existing order or its retries.
                var vatSettings = await _vatSettingsProvider.GetAsync();
                var pricing = OrderPricingCalculator.Calculate(subtotalAmount, discountAmount, vatSettings);
                subtotalAmount = pricing.SubtotalAmount;
                discountAmount = pricing.DiscountAmount;
                var finalAmount = pricing.FinalAmount;

                // There is no zero-value payment/fulfilment workflow.  Reject this before creating an
                // order or reserving stock, rather than stranding a pending order that no payment path
                // can settle. The guard deliberately uses the product amount after discount and BEFORE
                // VAT, in both calculation modes, so a 100% coupon can never become a tax-only order.
                if (pricing.IsZeroPayable)
                    throw new BusinessException("پرداخت سفارش رایگان پشتیبانی نمی‌شود. قیمت کالا یا تخفیف را اصلاح کنید.");

                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    // پسوند تصادفی برای جلوگیری از برخورد شماره سفارش در ثبت هم‌زمان (ایندکس یکتا دارد)
                    OrderNumber = $"VT-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                    Status = (byte)OrderStatus.PendingPayment,
                    PaymentStatus = (byte)PaymentStatus.Pending,
                    SubtotalAmount = subtotalAmount,
                    DiscountAmount = discountAmount,
                    FinalAmount = finalAmount,
                    VatEnabled = pricing.VatEnabled,
                    VatRatePercent = pricing.VatRatePercent,
                    VatCalculationMode = (byte)pricing.VatCalculationMode,
                    VatTaxableAmount = pricing.VatTaxableAmount,
                    VatAmount = pricing.VatAmount,
                    CurrencyType = currencyType,
                    CouponId = couponId,
                    Description = request.Description,
                    CreatedAt = now
                };

                await _dbContext.Orders.AddAsync(order);

                var orderItems = new List<OrderItem>();

                foreach (var cartItem in cart.CartItems)
                {
                    var kyc = EvaluateProductKyc(cartItem.Product, cartItem.UnitPrice, cartItem.Quantity);
                    var suppliedValues = cartItem.InputValues.ToDictionary(
                        x => x.FieldKey,
                        x => x.IsSensitive && x.EncryptedValue is not null
                            ? _encryptionService.Decrypt(x.EncryptedValue)
                            : x.Value,
                        StringComparer.OrdinalIgnoreCase);
                    var validatedValues = CartService.ValidateInputs(
                        cartItem.Product.ProductInputFields,
                        suppliedValues,
                        includeAllStages: true);

                    var orderItem = new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        ProductId = cartItem.ProductId,
                        ProductVariantId = cartItem.ProductVariantId,
                        ProductTitle = cartItem.Product.Title,
                        VariantTitle = cartItem.ProductVariant?.Title,
                        Quantity = cartItem.Quantity,
                        UnitPrice = cartItem.UnitPrice,
                        TotalPrice = cartItem.UnitPrice * cartItem.Quantity,
                        CurrencyType = currencyType,
                        DeliveryType = cartItem.Product.DeliveryType,
                        DeliveryStatus = (byte)DeliveryStatus.Pending,
                        RequiresVerification = kyc.RequiresKyc,
                        KycRequirementMode = (byte)kyc.Mode,
                        KycThresholdAmount = kyc.ThresholdAmount,
                        KycEvaluatedAmount = kyc.EvaluatedAmount,
                        KycPolicyVersionId = kyc.PolicyVersionId,
                        KycCustomerActionDeadlineHours = kyc.CustomerActionDeadlineHours,
                        CreatedAt = now
                    };

                    foreach (var field in cartItem.Product.ProductInputFields
                                 .Where(x => x.IsActive && validatedValues.ContainsKey(x.Key)))
                    {
                        var cartValue = cartItem.InputValues.FirstOrDefault(x =>
                            string.Equals(x.FieldKey, field.Key, StringComparison.OrdinalIgnoreCase));
                        orderItem.InputValues.Add(new OrderItemInputValue
                        {
                            Id = Guid.NewGuid(),
                            ProductInputFieldId = field.Id,
                            FieldKey = field.Key,
                            FieldLabel = field.Label,
                            FieldType = field.FieldType,
                            Value = field.IsSensitive ? null : validatedValues[field.Key],
                            EncryptedValue = field.IsSensitive ? cartValue?.EncryptedValue : null,
                            IsSensitive = field.IsSensitive,
                            CreatedAt = now
                        });
                    }

                    orderItems.Add(orderItem);
                }

                await _dbContext.OrderItems.AddRangeAsync(orderItems);

                var reservationIds = new List<Guid>();
                var reservationExpiresAt = now.AddMinutes(_paymentTiming.InstantCodeReservationLifetimeMinutes);

                // قفل انحصاری و به‌ازای هر محصول/تنوع پیش از انتخاب کد. کلید هم‌راستا با
                // GiftCodeReservationService است تا تخصیص کد در همهٔ مسیرها سریالی شود.
                // قفل‌ها به ترتیب یکسان (مرتب‌شده) گرفته می‌شوند تا هنگام رزرو چند کد به‌صورت
                // هم‌زمان، نه بن‌بست ردیفی (deadlock) رخ دهد و نه بن‌بست ناشی از ترتیب قفل‌ها.
                // Managed inventory is validated here but deliberately NOT reserved: stock is consumed
                // only on authoritative payment success, so an abandoned checkout never holds units.
                // This is a pre-payment guard against the obvious case; the atomic decrement at payment
                // capture remains the real defence, because stock can still change after this point.
                foreach (var orderItem in orderItems)
                {
                    if (orderItem.DeliveryType == (byte)DeliveryType.Instant)
                        continue;
                    // Non-Instant cart lines always carry a variant id: AddItemAsync resolves the
                    // product's canonical SKU when the caller omits one. A null here means a cart
                    // built before that guarantee existed, and there is no SKU whose stock could be
                    // checked, so it falls through to the atomic decrement at payment capture.
                    if (orderItem.ProductVariantId is null)
                        continue;

                    var sku = await _dbContext.ProductVariants
                        .Where(v => v.Id == orderItem.ProductVariantId)
                        .Select(v => new { v.StockQuantity, v.StockMode, v.Product.ForceOutOfStock })
                        .FirstOrDefaultAsync();

                    if (sku is null)
                        continue;

                    // An administrator can take a product off sale between the cart write and here,
                    // and that decision outranks whatever inventory exists.
                    if (sku.ForceOutOfStock)
                        throw new BusinessException($"محصول {orderItem.ProductTitle} در حال حاضر ناموجود است.");

                    // Unlimited has no quantity to compare against, so there is nothing to guard.
                    if (ProductAvailabilityRules.IsUnlimited((ProductVariantStockMode)sku.StockMode))
                        continue;

                    if (sku.StockQuantity < orderItem.Quantity)
                        throw new BusinessException(
                            $"موجودی محصول {orderItem.ProductTitle} کافی نیست؛ موجودی فعلی: {sku.StockQuantity}.");
                }

                foreach (var orderItem in orderItems)
                {
                    if (orderItem.DeliveryType != (byte)DeliveryType.Instant)
                        continue;

                    var quantity = orderItem.Quantity;

                    // تمام کدهای موردنیاز این آیتم در یک کوئری قفل‌دار و به‌صورت مجزا انتخاب می‌شوند.
                    // UPDLOCK/ROWLOCK: دو Checkout هم‌زمان هرگز یک کد را رزرو نمی‌کنند. انتخاب یکجای
                    // TOP(N) از انتخاب دوبارهٔ یک کد جلوگیری می‌کند؛ چون در حلقهٔ تک‌به‌تک، تغییرِ
                    // وضعیت هنوز در پایگاه‌داده اعمال نشده بود و همان ردیف دوباره برگردانده می‌شد.
                    var giftCodes = await _dbContext.GiftCodes
                        .FromSqlInterpolated($@"
                            SELECT TOP({quantity}) * FROM GiftCodes WITH (UPDLOCK, ROWLOCK)
                            WHERE ProductId = {orderItem.ProductId}
                              AND ((ProductVariantId IS NULL AND {orderItem.ProductVariantId} IS NULL)
                                   OR ProductVariantId = {orderItem.ProductVariantId})
                              AND Status = {(byte)GiftCodeStatus.Available}
                            ORDER BY CreatedAt")
                        .AsTracking()
                        .ToListAsync();

                    if (giftCodes.Count < quantity)
                        throw new BusinessException(
                            $"موجودی کد برای محصول {orderItem.ProductTitle} کافی نیست.");

                    foreach (var giftCode in giftCodes)
                    {
                        giftCode.Status = (byte)GiftCodeStatus.Reserved;
                        giftCode.ReservedByUserId = userId;
                        giftCode.ReservedAt = now;
                        giftCode.ReservationExpiresAt = reservationExpiresAt;
                        giftCode.OrderItemId = orderItem.Id;
                        giftCode.UpdatedAt = now;

                        var reservation = new GiftCodeReservation
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId,
                            OrderId = order.Id,
                            OrderItemId = orderItem.Id,
                            ProductId = orderItem.ProductId,
                            ProductVariantId = orderItem.ProductVariantId,
                            GiftCodeId = giftCode.Id,
                            Status = (byte)GiftCodeReservationStatus.Active,
                            ReservedAt = now,
                            ExpiresAt = reservationExpiresAt
                        };

                        reservationIds.Add(reservation.Id);

                        await _dbContext.GiftCodeReservations.AddAsync(reservation);
                    }
                }

                var payment = new Payment
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    UserId = userId,
                    Amount = finalAmount,
                    CurrencyType = currencyType,
                    // The row is the durable first external-payment attempt. In Development and
                    // Testing it is still completed only through the guarded mock verifier.
                    Gateway = "Zarinpal",
                    Status = (byte)PaymentStatus.Pending,
                    CallbackVerified = false,
                    RequestedAt = now
                };

                await _dbContext.Payments.AddAsync(payment);

                _dbContext.CartItems.RemoveRange(cart.CartItems);

                await _notificationService.CreateAsync(
                    userId,
                    (byte)NotificationType.OrderCreated,
                    "سفارش ثبت شد",
                    $"سفارش {order.OrderNumber} ثبت شد و در انتظار پرداخت است.");

                await _dbContext.SaveChangesAsync();

                await transaction.CommitAsync();

                stopwatch.Stop();
                _logger.LogInformation(
                    "Checkout completed for order {OrderNumber}. ItemCount={ItemCount} ReservationCount={ReservationCount} ElapsedMs={ElapsedMs} EventType={EventType}",
                    order.OrderNumber, orderItems.Count, reservationIds.Count, stopwatch.ElapsedMilliseconds, OperationalEventNames.CheckoutCompleted);

                return new CheckoutResultDto
                {
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber,
                    SubtotalAmount = order.SubtotalAmount,
                    DiscountAmount = order.DiscountAmount,
                    FinalAmount = order.FinalAmount,
                    VatEnabled = order.VatEnabled,
                    VatRatePercent = order.VatRatePercent,
                    VatCalculationMode = order.VatCalculationMode,
                    VatTaxableAmount = order.VatTaxableAmount,
                    VatAmount = order.VatAmount,
                    CurrencyType = order.CurrencyType,
                    OrderStatus = order.Status,
                    PaymentStatus = order.PaymentStatus,
                    ReservationIds = reservationIds
                };
            }
            catch (BusinessException exception)
            {
                await transaction.RollbackAsync();
                // موجودیت‌های ردیابی‌شده‌ی این تراکنشِ برگشت‌خورده (مثلاً Order/OrderItem) نباید در
                // ذخیره‌ی بعدی روی همین DbContext مشترک (مثل ثبت شکست Idempotency در کنترلر) نشت کنند.
                _dbContext.ChangeTracker.Clear();
                stopwatch.Stop();
                _logger.LogWarning(
                    "Checkout rejected for user {UserId}. ReasonCategory={ReasonCategory} ElapsedMs={ElapsedMs} EventType={EventType}",
                    userId, exception.GetType().Name, stopwatch.ElapsedMilliseconds, OperationalEventNames.CheckoutFailed);
                throw;
            }
            catch
            {
                await transaction.RollbackAsync();
                _dbContext.ChangeTracker.Clear();
                throw;
            }
        }

        private static KycRequirementEvaluation EvaluateProductKyc(Product product, decimal unitPrice, int quantity)
        {
            var evaluation = KycRequirementEvaluator.Evaluate(product.RequiresVerification, product.KycRequirementMode,
                product.KycThresholdAmount, product.KycPolicyVersionId, unitPrice, quantity);
            return evaluation with
            {
                CustomerActionDeadlineHours = evaluation.RequiresKyc
                    ? product.KycPolicyVersion?.CustomerActionDeadlineHours
                    : null
            };
        }

        private static decimal ResolveFinalPrice(decimal basePrice, decimal? discountPrice) =>
            discountPrice is > 0 && discountPrice < basePrice ? discountPrice.Value : basePrice;
    }
}
