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
using System.Diagnostics;
using Vitorize.Shared.Logging;

namespace Vitorize.Infrastructure.Services
{
    public class CheckoutService : ICheckoutService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly ICouponService _couponService;
        private readonly INotificationService _notificationService;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<CheckoutService> _logger;

        public CheckoutService(
            VitorizeDbContext dbContext,
            ICouponService couponService,
            INotificationService notificationService,
            IEncryptionService encryptionService,
            ILogger<CheckoutService>? logger = null)
        {
            _dbContext = dbContext;
            _couponService = couponService;
            _notificationService = notificationService;
            _encryptionService = encryptionService;
            _logger = logger ?? NullLogger<CheckoutService>.Instance;
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

                // Cart prices are display caches; authoritative catalog state is reloaded and
                // repriced inside this serializable transaction.
                var cart = await _dbContext.Carts
                    .Include(x => x.CartItems).ThenInclude(x => x.Product)
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
                    if (item.Quantity < Math.Max(1, product.MinOrderQuantity) ||
                        (product.MaxOrderQuantity.HasValue && item.Quantity > product.MaxOrderQuantity.Value))
                        throw new BusinessException($"تعداد سفارش محصول «{product.Title}» خارج از محدوده مجاز است.");
                    if (product.RequiresVerification &&
                        (!user.IsMobileConfirmed || user.VerificationStatus != (byte)VerificationStatus.Verified))
                        throw new BusinessException("برای خرید این محصول، تأیید موبایل و احراز هویت کامل الزامی است.");

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
                }

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

                var finalAmount = subtotalAmount - discountAmount;

                if (finalAmount < 0)
                    finalAmount = 0;

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
                    CouponId = couponId,
                    Description = request.Description,
                    CreatedAt = now
                };

                await _dbContext.Orders.AddAsync(order);

                var orderItems = new List<OrderItem>();

                foreach (var cartItem in cart.CartItems)
                {
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
                        DeliveryType = cartItem.Product.DeliveryType,
                        DeliveryStatus = (byte)DeliveryStatus.Pending,
                        RequiresVerification = cartItem.Product.RequiresVerification,
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
                var reservationExpiresAt = now.AddMinutes(15);

                foreach (var orderItem in orderItems)
                {
                    if (orderItem.DeliveryType != (byte)DeliveryType.Instant)
                        continue;

                    for (var i = 0; i < orderItem.Quantity; i++)
                    {
                        // UPDLOCK/READPAST: دو Checkout هم‌زمان هرگز یک کد را رزرو نمی‌کنند
                        // (بدون قفل، خواندن-سپس-نوشتن باعث فروش دوباره‌ی یک کد می‌شد).
                        var giftCode = (await _dbContext.GiftCodes
                            .FromSqlInterpolated($@"
                                SELECT TOP(1) * FROM GiftCodes WITH (UPDLOCK, ROWLOCK)
                                WHERE ProductId = {orderItem.ProductId}
                                  AND ((ProductVariantId IS NULL AND {orderItem.ProductVariantId} IS NULL)
                                       OR ProductVariantId = {orderItem.ProductVariantId})
                                  AND Status = {(byte)GiftCodeStatus.Available}
                                ORDER BY CreatedAt")
                            .AsTracking()
                            .ToListAsync())
                            .FirstOrDefault();

                        if (giftCode == null)
                            throw new BusinessException(
                                $"موجودی کد برای محصول {orderItem.ProductTitle} کافی نیست.");

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
                    Gateway = "Mock",
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
                    OrderStatus = order.Status,
                    PaymentStatus = order.PaymentStatus,
                    ReservationIds = reservationIds
                };
            }
            catch (BusinessException exception)
            {
                await transaction.RollbackAsync();
                stopwatch.Stop();
                _logger.LogWarning(
                    "Checkout rejected for user {UserId}. ReasonCategory={ReasonCategory} ElapsedMs={ElapsedMs} EventType={EventType}",
                    userId, exception.GetType().Name, stopwatch.ElapsedMilliseconds, OperationalEventNames.CheckoutFailed);
                throw;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static decimal ResolveFinalPrice(decimal basePrice, decimal? discountPrice) =>
            discountPrice is > 0 && discountPrice < basePrice ? discountPrice.Value : basePrice;
    }
}
