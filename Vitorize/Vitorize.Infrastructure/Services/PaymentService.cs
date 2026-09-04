using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vitorize.Application.DTOs.Payments;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Vitorize.Shared.Logging;
using Vitorize.Application.Common;
using Vitorize.Application.Models.Email;

namespace Vitorize.Infrastructure.Services
{
    public class PaymentService : IPaymentService
    {
        private const string ZarinpalGatewayName = "Zarinpal";

        private readonly VitorizeDbContext _dbContext;
        private readonly IGiftCodeDeliveryService _giftCodeDeliveryService;
        private readonly IPostPaymentOrderProcessor? _postPaymentOrderProcessor;
        private readonly ICouponService _couponService;
        private readonly IWalletService _walletService;
        private readonly INotificationService _notificationService;
        private readonly IZarinpalGatewayService _zarinpalGatewayService;
        private readonly ISmsOutboxEnqueuer _smsOutbox;
        private readonly IOrderEmailOutboxEnqueuer? _orderEmailOutbox;
        private readonly ILogger<PaymentService> _logger;
        private readonly PaymentTimingOptions _paymentTiming;

        public PaymentService(
            VitorizeDbContext dbContext,
            IGiftCodeDeliveryService giftCodeDeliveryService,
            ICouponService couponService,
            IWalletService walletService,
            INotificationService notificationService,
            IZarinpalGatewayService zarinpalGatewayService,
            ISmsOutboxEnqueuer smsOutbox,
            ILogger<PaymentService>? logger = null,
            IOptions<PaymentTimingOptions>? paymentTiming = null,
            IPostPaymentOrderProcessor? postPaymentOrderProcessor = null,
            IOrderEmailOutboxEnqueuer? orderEmailOutbox = null)
        {
            _dbContext = dbContext;
            _giftCodeDeliveryService = giftCodeDeliveryService;
            _couponService = couponService;
            _walletService = walletService;
            _notificationService = notificationService;
            _zarinpalGatewayService = zarinpalGatewayService;
            _smsOutbox = smsOutbox;
            _logger = logger ?? NullLogger<PaymentService>.Instance;
            _paymentTiming = paymentTiming?.Value ?? new PaymentTimingOptions();
            _postPaymentOrderProcessor = postPaymentOrderProcessor;
            _orderEmailOutbox = orderEmailOutbox;
        }

        public async Task<PaymentStartResultDto> StartPaymentAsync(Guid userId, Guid orderId)
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation(
                "Payment start requested for order {OrderId} by user {UserId}. Provider={Provider} EventType={EventType}",
                orderId, userId, ZarinpalGatewayName, OperationalEventNames.PaymentStarted);
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var prepared = await PrepareGatewayAttemptAsync(userId, orderId);
            if (prepared.ExistingAuthority is not null)
            {
                // Configuration lookup is deliberately outside the serializable attempt transaction.
                var url = await _zarinpalGatewayService.BuildPaymentUrlAsync(prepared.ExistingAuthority);
                return ToStartResult(prepared, prepared.ExistingAuthority, url);
            }

            // The external request must never run inside a database transaction. The attempt was
            // committed as INITIALIZING above, so a process failure leaves an auditable, recoverable
            // local record rather than an untracked redirect.
            var gatewayResult = await _zarinpalGatewayService.CreatePaymentAsync(
                prepared.Amount,
                prepared.Currency,
                $"پرداخت سفارش {prepared.OrderNumber} در Vitorize",
                prepared.Mobile,
                prepared.Email,
                prepared.OrderNumber);

            if (!gatewayResult.Success || string.IsNullOrWhiteSpace(gatewayResult.Authority))
            {
                await MarkGatewayAttemptFailedAsync(prepared.PaymentId, "REQUEST_FAILED",
                    "خطا در ایجاد درخواست پرداخت زرین‌پال.");
                _logger.LogWarning(
                    "Payment provider request failed for order {OrderNumber}. Provider={Provider} ElapsedMs={ElapsedMs} EventType={EventType}",
                    prepared.OrderNumber, ZarinpalGatewayName, stopwatch.ElapsedMilliseconds, OperationalEventNames.PaymentVerificationFailed);
                throw new BusinessException("امکان اتصال به درگاه پرداخت وجود ندارد.");
            }

            var persisted = await PersistGatewayAuthorityAsync(prepared, gatewayResult.Authority, gatewayResult.PaymentUrl);
            _logger.LogInformation(
                "Payment provider request created for order {OrderNumber}. Provider={Provider} Authority={Authority} ElapsedMs={ElapsedMs} EventType={EventType}",
                prepared.OrderNumber, ZarinpalGatewayName, SensitiveLogData.Sanitize(gatewayResult.Authority, 100), stopwatch.ElapsedMilliseconds, OperationalEventNames.PaymentStarted);
            return persisted;
        }

        public async Task<PaymentRetryEligibilityDto> GetRetryEligibilityAsync(Guid userId, Guid orderId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var order = await _dbContext.Orders.AsNoTracking()
                .Include(x => x.Payments)
                .FirstOrDefaultAsync(x => x.Id == orderId && x.UserId == userId)
                ?? throw new NotFoundException("سفارش یافت نشد.");
            var reason = PaymentAttemptPolicy.GetIneligibilityReason(order, order.Payments);
            return new PaymentRetryEligibilityDto { OrderId = orderId, CanRetry = reason is null, Reason = reason };
        }

        private async Task<GatewayAttemptPreparation> PrepareGatewayAttemptAsync(Guid userId, Guid orderId)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                await SqlServerTransactionLock.AcquireAsync(_dbContext, $"payment-start:order:{orderId:N}");
                var now = DateTime.UtcNow;
                var order = await _dbContext.Orders
                    .Include(x => x.User)
                    .Include(x => x.Payments)
                    .Include(x => x.OrderItems)
                    .Include(x => x.GiftCodeReservations).ThenInclude(x => x.GiftCode)
                    .FirstOrDefaultAsync(x => x.Id == orderId && x.UserId == userId)
                    ?? throw new NotFoundException("سفارش یافت نشد.");

                var reason = PaymentAttemptPolicy.GetIneligibilityReason(order, order.Payments);
                if (reason is not null)
                    throw new BusinessException(reason);
                if (!Enum.IsDefined(typeof(CurrencyType), order.CurrencyType))
                    throw new BusinessException("واحد پول پرداخت با سفارش همخوانی ندارد.");

                var current = order.Payments
                    .Where(x => x.Status == (byte)PaymentStatus.Pending &&
                                (x.Gateway == ZarinpalGatewayName || x.Gateway == "Mock"))
                    .OrderByDescending(x => x.RequestedAt)
                    .FirstOrDefault();

                if (current is not null && current.Amount != order.FinalAmount)
                    throw new BusinessException("مبلغ پرداخت با مبلغ سفارش همخوانی ندارد.");
                if (current is not null && current.CurrencyType != order.CurrencyType)
                    throw new BusinessException("واحد پول پرداخت با سفارش همخوانی ندارد.");

                if (current is not null && !IsAttemptStale(current, now))
                {
                    if (!string.IsNullOrWhiteSpace(current.Authority))
                    {
                        await transaction.CommitAsync();
                        return GatewayAttemptPreparation.Existing(order, current);
                    }

                    // Checkout stages a READY attempt. A second browser click after it was claimed
                    // sees INITIALIZING and cannot create a competing authority.
                    if (string.Equals(current.ProviderStatusCode, "INITIALIZING", StringComparison.Ordinal))
                        throw new BusinessException("درخواست پرداخت در حال آماده‌سازی است. چند لحظه دیگر دوباره تلاش کنید.");

                    current.Gateway = ZarinpalGatewayName;
                }
                else
                {
                    if (current is not null)
                    {
                        current.Status = (byte)PaymentStatus.Failed;
                        current.ProviderStatusCode = "ATTEMPT_EXPIRED";
                        current.ErrorMessage = "مهلت اقدام برای این تلاش پرداخت به پایان رسید.";
                        current.UpdatedAt = now;
                    }

                    current = new Payment
                    {
                        Id = Guid.NewGuid(), OrderId = order.Id, UserId = userId,
                        Amount = order.FinalAmount, CurrencyType = order.CurrencyType,
                        Gateway = ZarinpalGatewayName, Status = (byte)PaymentStatus.Pending,
                        CallbackVerified = false, RequestedAt = now
                    };
                    await _dbContext.Payments.AddAsync(current);
                }

                await EnsureInstantReservationsAsync(order, now);
                current.ProviderStatusCode = "INITIALIZING";
                current.RawRequestData = JsonSerializer.Serialize(new
                {
                    Type = "ZarinpalRequestPrepared", order.Id, order.OrderNumber,
                    current.Amount, PreparedAt = now
                });
                current.UpdatedAt = now;
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return GatewayAttemptPreparation.New(order, current);
            }
            catch
            {
                await transaction.RollbackAsync();
                _dbContext.ChangeTracker.Clear();
                throw;
            }
        }

        private async Task<PaymentStartResultDto> PersistGatewayAuthorityAsync(
            GatewayAttemptPreparation prepared, string authority, string paymentUrl)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                await SqlServerTransactionLock.AcquireAsync(_dbContext, $"payment-start:order:{prepared.OrderId:N}");
                var payment = await _dbContext.Payments.Include(x => x.Order)
                    .FirstOrDefaultAsync(x => x.Id == prepared.PaymentId && x.UserId == prepared.UserId)
                    ?? throw new NotFoundException("تلاش پرداخت یافت نشد.");
                if (payment.Status != (byte)PaymentStatus.Pending ||
                    payment.Order.PaymentStatus == (byte)PaymentStatus.Paid ||
                    payment.Order.Status != (byte)OrderStatus.PendingPayment)
                {
                    payment.Status = (byte)PaymentStatus.Failed;
                    payment.ProviderStatusCode = "SUPERSEDED_BEFORE_REDIRECT";
                    payment.ErrorMessage = "سفارش پیش از انتقال به درگاه با روش دیگری تعیین تکلیف شد.";
                    payment.UpdatedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                    throw new BusinessException("وضعیت سفارش تغییر کرده است؛ پرداخت مجدد ممکن نیست.");
                }

                payment.Authority = authority;
                payment.ProviderStatusCode = "REQUESTED";
                payment.RawResponseData = JsonSerializer.Serialize(new
                {
                    Type = "ZarinpalRequest", Authority = authority, PersistedAt = DateTime.UtcNow
                });
                payment.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return ToStartResult(prepared, authority, paymentUrl);
            }
            catch
            {
                await transaction.RollbackAsync();
                _dbContext.ChangeTracker.Clear();
                throw;
            }
        }

        private async Task MarkGatewayAttemptFailedAsync(Guid paymentId, string providerStatus, string error)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var payment = await _dbContext.Payments.FirstOrDefaultAsync(x => x.Id == paymentId);
                if (payment is not null && payment.Status == (byte)PaymentStatus.Pending)
                {
                    payment.Status = (byte)PaymentStatus.Failed;
                    payment.ProviderStatusCode = providerStatus;
                    payment.ErrorMessage = error;
                    payment.UpdatedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                }
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                _dbContext.ChangeTracker.Clear();
                throw;
            }
        }

        private async Task EnsureInstantReservationsAsync(Order order, DateTime now)
        {
            var instantItems = order.OrderItems.Where(x => x.DeliveryType == (byte)DeliveryType.Instant).ToList();
            if (instantItems.Count == 0) return;

            foreach (var key in instantItems
                .Select(x => $"gift-reservation:{x.ProductId:N}:{x.ProductVariantId?.ToString("N") ?? "none"}")
                .Distinct().OrderBy(x => x, StringComparer.Ordinal))
                await SqlServerTransactionLock.AcquireAsync(_dbContext, key);

            var expired = order.GiftCodeReservations.Where(x =>
                x.Status == (byte)GiftCodeReservationStatus.Active && x.ExpiresAt <= now).ToList();
            foreach (var reservation in expired)
            {
                reservation.Status = (byte)GiftCodeReservationStatus.Expired;
                reservation.ReleasedAt = now;
                if (reservation.GiftCode is not null)
                {
                    reservation.GiftCode.Status = (byte)GiftCodeStatus.Available;
                    reservation.GiftCode.ReservedByUserId = null;
                    reservation.GiftCode.ReservedAt = null;
                    reservation.GiftCode.ReservationExpiresAt = null;
                    reservation.GiftCode.UpdatedAt = now;
                }
            }
            if (expired.Count > 0) await _dbContext.SaveChangesAsync();

            var expiresAt = now.AddMinutes(_paymentTiming.InstantCodeReservationLifetimeMinutes);
            foreach (var item in instantItems)
            {
                var activeCount = order.GiftCodeReservations.Count(x =>
                    x.OrderItemId == item.Id && x.Status == (byte)GiftCodeReservationStatus.Active && x.ExpiresAt > now);
                for (var i = activeCount; i < item.Quantity; i++)
                {
                    var code = (await _dbContext.GiftCodes.FromSqlInterpolated($@"
                        SELECT TOP(1) * FROM GiftCodes WITH (UPDLOCK, ROWLOCK)
                        WHERE ProductId = {item.ProductId}
                          AND ((ProductVariantId IS NULL AND {item.ProductVariantId} IS NULL)
                               OR ProductVariantId = {item.ProductVariantId})
                          AND Status = {(byte)GiftCodeStatus.Available}
                        ORDER BY CreatedAt").AsTracking().ToListAsync()).FirstOrDefault()
                        ?? throw new BusinessException("موجودی کد آنی برای تلاش مجدد پرداخت کافی نیست.");

                    code.Status = (byte)GiftCodeStatus.Reserved;
                    code.ReservedByUserId = order.UserId;
                    code.ReservedAt = now;
                    code.ReservationExpiresAt = expiresAt;
                    code.OrderItemId = item.Id;
                    code.UpdatedAt = now;
                    var reservation = new GiftCodeReservation
                    {
                        Id = Guid.NewGuid(), UserId = order.UserId, OrderId = order.Id,
                        OrderItemId = item.Id, ProductId = item.ProductId,
                        ProductVariantId = item.ProductVariantId, GiftCodeId = code.Id,
                        Status = (byte)GiftCodeReservationStatus.Active, ReservedAt = now, ExpiresAt = expiresAt
                    };
                    order.GiftCodeReservations.Add(reservation);
                    await _dbContext.GiftCodeReservations.AddAsync(reservation);
                }
            }
        }

        private bool IsAttemptStale(Payment payment, DateTime now) =>
            payment.RequestedAt <= now.AddMinutes(-_paymentTiming.GatewayAttemptLifetimeMinutes);

        private static PaymentStartResultDto ToStartResult(GatewayAttemptPreparation prepared, string authority, string paymentUrl) => new()
        {
            PaymentId = prepared.PaymentId, OrderId = prepared.OrderId, Amount = prepared.Amount,
            Gateway = ZarinpalGatewayName, Authority = authority, PaymentUrl = paymentUrl
        };

        private sealed record GatewayAttemptPreparation(Guid PaymentId, Guid OrderId, Guid UserId,
            string OrderNumber, decimal Amount, CurrencyType Currency, string? Mobile, string? Email,
            string? ExistingAuthority)
        {
            public static GatewayAttemptPreparation New(Order order, Payment payment) =>
                new(payment.Id, order.Id, order.UserId, order.OrderNumber, payment.Amount,
                    (CurrencyType)payment.CurrencyType, order.User?.Mobile, order.User?.Email, null);

            public static GatewayAttemptPreparation Existing(Order order, Payment payment) =>
                New(order, payment) with { ExistingAuthority = payment.Authority };
        }
        public async Task<PaymentVerifyResultDto> VerifyZarinpalPaymentAsync(
            string authority,
            string status)
        {
            return await VerifyZarinpalPaymentCoreAsync(authority, status, isReconciliation: false);
        }

        private async Task<PaymentVerifyResultDto> VerifyZarinpalPaymentCoreAsync(
            string authority,
            string status,
            bool isReconciliation)
        {
            var stopwatch = Stopwatch.StartNew();
            if (string.IsNullOrWhiteSpace(authority))
                throw new BusinessException("Authority معتبر نیست.");

            var normalizedStatus = string.IsNullOrWhiteSpace(status)
                ? "NOK"
                : status.Trim();

            Guid paymentId = Guid.Empty;
            decimal amount = 0m;
            await using (var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable))
            try
            {
                await SqlServerTransactionLock.AcquireAsync(
                    _dbContext,
                    $"payment-callback:{authority.Trim().ToUpperInvariant()}");
                var payment = await _dbContext.Payments
                    .Include(x => x.PaymentCallbacks)
                    .Include(x => x.Order)
                        .ThenInclude(x => x.GiftCodeReservations)
                    .Include(x => x.Order)
                        .ThenInclude(x => x.OrderItems)
                    .FirstOrDefaultAsync(x =>
                        x.Authority == authority &&
                        x.Gateway == ZarinpalGatewayName);

                if (payment == null)
                    throw new NotFoundException("پرداخت یافت نشد.");

                var order = payment.Order;

                if (!isReconciliation)
                    await AddCallbackIfNotExistsAsync(payment, authority, normalizedStatus);

                if (payment.Status == (byte)PaymentStatus.Paid)
                {
                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation(
                        "Duplicate payment callback ignored for order {OrderNumber}. Provider={Provider} Authority={Authority} EventType={EventType}",
                        order.OrderNumber, ZarinpalGatewayName, SensitiveLogData.Sanitize(authority, 100), OperationalEventNames.PaymentCallbackDuplicate);

                    await transaction.DisposeAsync();
                    await ProcessPaidOrderSafelyAsync(order.Id);
                    return CreateVerifyResult(payment, order);
                }

                if (!string.Equals(normalizedStatus, "OK", StringComparison.OrdinalIgnoreCase))
                {
                    payment.Status = (byte)PaymentStatus.Cancelled;
                    payment.CallbackVerified = false;
                    payment.ProviderStatusCode = normalizedStatus;
                    payment.ErrorMessage = "پرداخت توسط کاربر لغو شد یا ناموفق بود.";
                    payment.UpdatedAt = DateTime.UtcNow;

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogWarning(
                        "Payment callback was unsuccessful for order {OrderNumber}. Provider={Provider} StatusCategory={StatusCategory} ElapsedMs={ElapsedMs} EventType={EventType}",
                        order.OrderNumber, ZarinpalGatewayName, SensitiveLogData.Sanitize(normalizedStatus, 32), stopwatch.ElapsedMilliseconds, OperationalEventNames.PaymentVerificationFailed);

                    return CreateFailedVerifyResult(payment, order);
                }

                var lateTerminalAttempt = payment.Status is (byte)PaymentStatus.Cancelled or (byte)PaymentStatus.Failed;
                if (payment.Status != (byte)PaymentStatus.Pending &&
                    !(lateTerminalAttempt && string.Equals(normalizedStatus, "OK", StringComparison.OrdinalIgnoreCase)))
                {
                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return CreateFailedVerifyResult(payment, order);
                }

                if (payment.Amount != order.FinalAmount)
                    throw new BusinessException("مبلغ پرداخت معتبر نیست.");
                if (payment.CurrencyType != order.CurrencyType)
                    throw new BusinessException("واحد پول پرداخت معتبر نیست.");

                var verificationLease = TimeSpan.FromSeconds(Math.Max(60, _paymentTiming.ReconciliationIntervalSeconds * 2));
                if ((string.Equals(payment.ProviderStatusCode, "VERIFYING", StringComparison.Ordinal) ||
                     string.Equals(payment.ProviderStatusCode, "VERIFYING_LATE", StringComparison.Ordinal)) &&
                    payment.UpdatedAt >= DateTime.UtcNow.Subtract(verificationLease))
                {
                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return CreateFailedVerifyResult(payment, order);
                }
                payment.ProviderStatusCode = lateTerminalAttempt ? "VERIFYING_LATE" : "VERIFYING";
                payment.UpdatedAt = DateTime.UtcNow;
                paymentId = payment.Id;
                amount = payment.Amount;
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            // Provider verification is intentionally outside any SQL transaction. The temporary
            // VERIFYING marker serializes duplicate callbacks without keeping database locks while
            // a network request is in flight; reconciliation can reclaim a stale marker.
            var verifyResult = await _zarinpalGatewayService.VerifyPaymentAsync(authority, amount);
            string? verifiedProviderReference = verifyResult.Success ? verifyResult.RefId.ToString() : null;
            string? verifiedMaskedCardPan = verifyResult.Success ? verifyResult.CardPan : null;

            await using var finalizeTransaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                await SqlServerTransactionLock.AcquireAsync(_dbContext, $"payment-callback:{authority.Trim().ToUpperInvariant()}");
                var payment = await _dbContext.Payments
                    .Include(x => x.Order).ThenInclude(x => x.GiftCodeReservations)
                    .Include(x => x.Order).ThenInclude(x => x.OrderItems)
                    .FirstOrDefaultAsync(x => x.Id == paymentId)
                    ?? throw new NotFoundException("پرداخت یافت نشد.");
                var order = payment.Order;

                // Provider verification happened outside any transaction, and this context already
                // tracked the order from the first transaction, so the tracked copy can be stale by
                // now: the order may have been cancelled or otherwise decided while the request was
                // in flight. Every decision below turns on the order's current state, so re-read it
                // under this transaction's lock rather than trusting the tracked snapshot.
                await _dbContext.Entry(order).ReloadAsync();

                if (payment.Status == (byte)PaymentStatus.Paid)
                {
                    await finalizeTransaction.CommitAsync();
                    await finalizeTransaction.DisposeAsync();
                    await ProcessPaidOrderSafelyAsync(order.Id);
                    return CreateVerifyResult(payment, order);
                }
                var lateTerminalAttempt = payment.Status is (byte)PaymentStatus.Cancelled or (byte)PaymentStatus.Failed;
                if (payment.Status != (byte)PaymentStatus.Pending && !lateTerminalAttempt)
                {
                    await finalizeTransaction.CommitAsync();
                    return CreateFailedVerifyResult(payment, order);
                }

                payment.RawResponseData = JsonSerializer.Serialize(new
                {
                    Type = isReconciliation ? "ZarinpalReconcile" : "ZarinpalVerify",
                    Authority = authority, Amount = payment.Amount, Result = verifyResult,
                    VerifiedAt = DateTime.UtcNow
                });

                if (!verifyResult.Success)
                {
                    if (lateTerminalAttempt)
                    {
                        payment.ErrorMessage = "نتیجهٔ موفق دیرهنگام برای این تلاش از سوی درگاه تایید نشد.";
                        payment.UpdatedAt = DateTime.UtcNow;
                        await _dbContext.SaveChangesAsync();
                        await finalizeTransaction.CommitAsync();
                        return CreateFailedVerifyResult(payment, order);
                    }
                    payment.Status = (byte)PaymentStatus.Failed;
                    payment.CallbackVerified = false;
                    payment.ProviderStatusCode = isReconciliation ? "RECONCILE_FAILED" : "VERIFY_FAILED";
                    payment.ErrorMessage = "تایید پرداخت زرین‌پال ناموفق بود.";
                    payment.UpdatedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    await finalizeTransaction.CommitAsync();
                    _logger.LogWarning(
                        "Payment verification failed for order {OrderNumber}. Provider={Provider} ElapsedMs={ElapsedMs} EventType={EventType}",
                        order.OrderNumber, ZarinpalGatewayName, stopwatch.ElapsedMilliseconds, OperationalEventNames.PaymentVerificationFailed);
                    return CreateFailedVerifyResult(payment, order);
                }

                // A provider success that arrives after the order was already decided must never
                // drive fulfillment. Two ways that happens: another attempt already paid, or the
                // customer cancelled while a session was still open at the gateway. Both keep the
                // gateway proof and hand the money to finance instead of delivering goods against a
                // cancelled order.
                if (order.PaymentStatus == (byte)PaymentStatus.Paid ||
                    order.Status != (byte)OrderStatus.PendingPayment)
                {
                    // A late success for an older attempt must never overwrite the authoritative
                    // payment. There is no automatic provider refund in Phase 0, so preserve the
                    // gateway proof and create an explicit finance-resolution audit record.
                    payment.Status = (byte)PaymentStatus.Failed;
                    payment.CallbackVerified = true;
                    payment.ProviderStatusCode = order.Status == (byte)OrderStatus.Cancelled
                        ? "LATE_SUCCESS_ON_CANCELLED_ORDER_REQUIRES_FINANCE"
                        : "LATE_SUCCESS_REQUIRES_FINANCE";
                    payment.ErrorMessage = order.Status == (byte)OrderStatus.Cancelled
                        ? "پرداخت موفق دیرهنگام برای سفارش لغو‌شده؛ نیازمند بازپرداخت و بررسی مالی."
                        : "پرداخت موفق دیرهنگام پس از تعیین تکلیف سفارش؛ نیازمند بررسی مالی.";
                    payment.ReferenceNumber = verifiedProviderReference;
                    payment.MaskedCardPan = verifiedMaskedCardPan;
                    payment.TransactionId = authority;
                    payment.GatewayTrackingCode = verifiedProviderReference;
                    payment.VerifiedAt = DateTime.UtcNow;
                    payment.UpdatedAt = DateTime.UtcNow;
                    await _dbContext.FinancialAuditLogs.AddAsync(new FinancialAuditLog
                    {
                        EventType = "LateGatewayPaymentRequiresFinanceResolution", EntityType = "Payment",
                        EntityId = payment.Id, UserId = payment.UserId, Amount = payment.Amount,
                        CorrelationId = order.Id, Detail = $"order:{order.OrderNumber}", CreatedAt = DateTime.UtcNow
                    });
                    await _dbContext.SaveChangesAsync();
                    await finalizeTransaction.CommitAsync();
                    return CreateFailedVerifyResult(payment, order);
                }

                var now = DateTime.UtcNow;
                payment.Status = (byte)PaymentStatus.Paid;
                payment.CallbackVerified = true;
                payment.VerifiedAt = now;
                payment.UpdatedAt = now;
                payment.ReferenceNumber = verifiedProviderReference;
                payment.MaskedCardPan = verifiedMaskedCardPan;
                payment.TransactionId = authority;
                payment.GatewayTrackingCode = verifiedProviderReference;
                payment.ProviderStatusCode = "100";
                try
                {
                    // A late successful attempt becomes financially authoritative if no other
                    // attempt has paid yet. Disable any still-pending sibling before fulfillment
                    // so it cannot remain a second active charge path.
                    var pendingSiblings = await _dbContext.Payments.Where(x =>
                        x.OrderId == order.Id && x.Id != payment.Id &&
                        x.Status == (byte)PaymentStatus.Pending).ToListAsync();
                    foreach (var sibling in pendingSiblings)
                    {
                        sibling.Status = (byte)PaymentStatus.Failed;
                        sibling.ProviderStatusCode = "SUPERSEDED_AFTER_OTHER_ATTEMPT_PAID";
                        sibling.ErrorMessage = "تلاش دیگری برای این سفارش با موفقیت تعیین تکلیف شد.";
                        sibling.UpdatedAt = now;
                    }
                    await CompletePaidOrderAsync(order, payment.UserId, now);
                }
                catch (BusinessException ex)
                {
                    await finalizeTransaction.RollbackAsync();
                    return await CompensateVerifiedPaymentAsync(payment.Id, verifiedProviderReference!, verifiedMaskedCardPan, ex.Message);
                }
                await finalizeTransaction.CommitAsync();
                await finalizeTransaction.DisposeAsync();
                await ProcessPaidOrderSafelyAsync(order.Id);
                _logger.LogInformation(
                    "Payment verified for order {OrderNumber}. Provider={Provider} Authority={Authority} ElapsedMs={ElapsedMs} EventType={EventType}",
                    order.OrderNumber, ZarinpalGatewayName, SensitiveLogData.Sanitize(authority, 100), stopwatch.ElapsedMilliseconds, OperationalEventNames.PaymentVerified);
                return CreateVerifyResult(payment, order);
            }
            catch
            {
                await finalizeTransaction.RollbackAsync();
                throw;
            }
        }

        public async Task<int> ReconcilePendingZarinpalPaymentsAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation(
                "Payment reconciliation started. Provider={Provider} EventType={EventType}",
                ZarinpalGatewayName, OperationalEventNames.PaymentReconciliationStarted);
            var threshold = DateTime.UtcNow.AddMinutes(-_paymentTiming.PendingPaymentReconciliationAgeMinutes);

            var paymentIds = await _dbContext.Payments
                .Where(x =>
                    x.Gateway == ZarinpalGatewayName &&
                    x.Status == (byte)PaymentStatus.Pending &&
                    x.Authority != null &&
                    x.RequestedAt <= threshold)
                .OrderBy(x => x.RequestedAt)
                .Take(50)
                .Select(x => x.Id)
                .ToListAsync();

            var processed = 0;

            foreach (var paymentId in paymentIds)
            {
                _dbContext.ChangeTracker.Clear();
                var authority = await _dbContext.Payments.AsNoTracking()
                    .Where(x => x.Id == paymentId && x.Status == (byte)PaymentStatus.Pending)
                    .Select(x => x.Authority).FirstOrDefaultAsync();
                if (string.IsNullOrWhiteSpace(authority))
                    continue;
                try
                {
                    await VerifyZarinpalPaymentCoreAsync(authority, "OK", isReconciliation: true);
                    processed++;
                }
                catch (Exception ex)
                {
                    _dbContext.ChangeTracker.Clear();
                    var failedPayment = await _dbContext.Payments
                        .FirstOrDefaultAsync(x => x.Id == paymentId);
                    if (failedPayment != null)
                    {
                        failedPayment.ErrorMessage = $"Reconcile error: {SensitiveLogData.SafeExceptionMessage(ex)}";
                        failedPayment.UpdatedAt = DateTime.UtcNow;

                        await _dbContext.SaveChangesAsync();
                    }

                    _logger.LogError(
                        "Payment reconciliation failed for payment {PaymentId}. Provider={Provider} ExceptionType={ExceptionType} EventType={EventType}",
                        paymentId, ZarinpalGatewayName, ex.GetType().Name, OperationalEventNames.PaymentReconciliationFailed);
                }
            }

            _logger.LogInformation(
                "Payment reconciliation completed. CandidateCount={CandidateCount} ProcessedCount={ProcessedCount} ElapsedMs={ElapsedMs} EventType={EventType}",
                paymentIds.Count, processed, stopwatch.ElapsedMilliseconds, OperationalEventNames.PaymentReconciliationCompleted);

            return processed;
        }

        public async Task<PaymentVerifyResultDto> VerifyMockPaymentAsync(Guid userId, Guid paymentId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                await SqlServerTransactionLock.AcquireAsync(_dbContext, $"payment:{paymentId:N}");
                var payment = await _dbContext.Payments
                    .Include(x => x.Order)
                        .ThenInclude(x => x.GiftCodeReservations)
                    .Include(x => x.Order)
                        .ThenInclude(x => x.OrderItems)
                    .FirstOrDefaultAsync(x => x.Id == paymentId && x.UserId == userId);

                if (payment == null)
                    throw new NotFoundException("پرداخت یافت نشد.");

                var order = payment.Order;

                if (payment.Status == (byte)PaymentStatus.Paid)
                {
                    await transaction.CommitAsync();
                    await transaction.DisposeAsync();
                    await ProcessPaidOrderSafelyAsync(order.Id);
                    return CreateVerifyResult(payment, order);
                }

                if (payment.Status != (byte)PaymentStatus.Pending)
                    throw new BusinessException("وضعیت پرداخت قابل تایید نیست.");

                if (payment.Amount != order.FinalAmount)
                    throw new BusinessException("مبلغ پرداخت معتبر نیست.");

                var now = DateTime.UtcNow;

                payment.Status = (byte)PaymentStatus.Paid;
                payment.CallbackVerified = true;
                payment.VerifiedAt = now;
                payment.UpdatedAt = now;
                payment.ReferenceNumber = $"MOCK-REF-{now:yyyyMMddHHmmss}";

                await CompletePaidOrderAsync(order, userId, now);

                await transaction.CommitAsync();
                await transaction.DisposeAsync();
                await ProcessPaidOrderSafelyAsync(order.Id);

                return CreateVerifyResult(payment, order);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<PaymentVerifyResultDto> PayWithWalletAsync(Guid userId, Guid orderId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                await SqlServerTransactionLock.AcquireAsync(_dbContext, $"wallet-payment:order:{orderId:N}");
                var order = await _dbContext.Orders
                    .Include(x => x.GiftCodeReservations)
                    .Include(x => x.OrderItems)
                    .Include(x => x.Payments)
                    .FirstOrDefaultAsync(x => x.Id == orderId && x.UserId == userId);

                if (order == null)
                    throw new NotFoundException("سفارش یافت نشد.");

                if (order.PaymentStatus == (byte)PaymentStatus.Paid)
                    throw new BusinessException("این سفارش قبلاً پرداخت شده است.");

                // Wallet is an internal debit with no provider session, so it is refused by state
                // rather than raced: a cancelled or already-decided order can never be wallet-paid.
                if (order.Status != (byte)OrderStatus.PendingPayment)
                    throw new BusinessException("این سفارش دیگر در انتظار پرداخت نیست.");

                if (order.FinalAmount <= 0)
                    throw new BusinessException("مبلغ سفارش معتبر نیست.");
                if (order.CurrencyType != (byte)CurrencyType.Toman)
                    throw new BusinessException("پرداخت از کیف پول فقط برای سفارش‌های تومانی پشتیبانی می‌شود.");

                var now = DateTime.UtcNow;

                await _walletService.DebitAsync(
                    userId,
                    order.FinalAmount,
                    (byte)WalletReferenceType.OrderPayment,
                    order.Id,
                    $"پرداخت سفارش {order.OrderNumber} از کیف پول");

                var payment = new Payment
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    UserId = userId,
                    Amount = order.FinalAmount,
                    CurrencyType = order.CurrencyType,
                    Gateway = "Wallet",
                    Authority = $"WALLET-{Guid.NewGuid():N}",
                    ReferenceNumber = $"WALLET-REF-{now:yyyyMMddHHmmss}",
                    TransactionId = $"WALLET-TX-{Guid.NewGuid():N}",
                    Status = (byte)PaymentStatus.Paid,
                    CallbackVerified = true,
                    RequestedAt = now,
                    VerifiedAt = now,
                    UpdatedAt = now
                };

                await _dbContext.Payments.AddAsync(payment);

                await CompletePaidOrderAsync(order, userId, now);

                await transaction.CommitAsync();
                await transaction.DisposeAsync();

                await ProcessPaidOrderSafelyAsync(order.Id);

                return CreateVerifyResult(payment, order);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<PaymentRefundDto> RefundAsync(
            Guid paymentId,
            Guid adminUserId,
            PaymentRefundRequestDto request)
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation(
                "Refund requested for payment {PaymentId} by admin {AdminUserId}. Method={RefundMethod} EventType={EventType}",
                paymentId, adminUserId, request?.Method, "RefundRequested");
            if (paymentId == Guid.Empty || adminUserId == Guid.Empty)
                throw new BusinessException("شناسه پرداخت یا کاربر معتبر نیست.");
            request ??= new PaymentRefundRequestDto();
            var reason = request.Reason?.Trim();
            var key = request.IdempotencyKey?.Trim();
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
                throw new BusinessException("دلیل بازپرداخت الزامی است و حداکثر ۱۰۰۰ نویسه دارد.");
            if (string.IsNullOrWhiteSpace(key) || key.Length > 100)
                throw new BusinessException("کلید تکرارناپذیری بازپرداخت الزامی است.");
            if (!Enum.IsDefined(typeof(PaymentRefundMethod), request.Method))
                throw new BusinessException("روش بازپرداخت معتبر نیست.");

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await SqlServerTransactionLock.AcquireAsync(_dbContext, $"refund:payment:{paymentId:N}");

            var existing = await _dbContext.PaymentRefunds.AsNoTracking()
                .FirstOrDefaultAsync(x => x.PaymentId == paymentId && x.IdempotencyKey == key);
            if (existing is not null)
            {
                await transaction.CommitAsync();
                return MapRefund(existing);
            }

            var activeRefundExists = await _dbContext.PaymentRefunds.AsNoTracking()
                .AnyAsync(x => x.PaymentId == paymentId &&
                    (x.Status == (byte)PaymentRefundStatus.Pending || x.Status == (byte)PaymentRefundStatus.Completed));
            if (activeRefundExists)
                throw new BusinessException("An active or completed refund already exists for this payment.");

            var payment = await _dbContext.Payments
                .Include(x => x.Order).ThenInclude(x => x.GiftCodeReservations).ThenInclude(x => x.GiftCode)
                .FirstOrDefaultAsync(x => x.Id == paymentId)
                ?? throw new NotFoundException("پرداخت یافت نشد.");
            if (payment.Status != (byte)PaymentStatus.Paid ||
                payment.Order.PaymentStatus != (byte)PaymentStatus.Paid)
                throw new BusinessException("فقط پرداخت موفق و بازپرداخت‌نشده قابل بازپرداخت است.");

            var now = DateTime.UtcNow;
            var refund = new PaymentRefund
            {
                Id = Guid.NewGuid(), PaymentId = payment.Id, OrderId = payment.OrderId,
                UserId = payment.UserId, Amount = payment.Amount, Method = request.Method,
                Status = request.Method == (byte)PaymentRefundMethod.Wallet
                    ? (byte)PaymentRefundStatus.Completed : (byte)PaymentRefundStatus.Pending,
                Reason = reason, IdempotencyKey = key, RequestedByUserId = adminUserId,
                RequestedAt = now,
                CompletedAt = request.Method == (byte)PaymentRefundMethod.Wallet ? now : null
            };
            await _dbContext.PaymentRefunds.AddAsync(refund);

            if (request.Method == (byte)PaymentRefundMethod.Wallet)
            {
                await _walletService.CreditAsync(payment.UserId, payment.Amount,
                    (byte)WalletReferenceType.Refund, refund.Id,
                    $"بازپرداخت سفارش {payment.Order.OrderNumber}");
                await CompleteRefundStateAsync(payment, refund, adminUserId, now, "wallet");
            }
            else
            {
                await _dbContext.FinancialAuditLogs.AddAsync(new FinancialAuditLog
                {
                    EventType = "GatewayRefundRequested", EntityType = "PaymentRefund",
                    EntityId = refund.Id, UserId = adminUserId, Amount = refund.Amount,
                    CorrelationId = payment.OrderId, Detail = $"order:{payment.Order.OrderNumber}", CreatedAt = now
                });
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            _logger.LogInformation(
                "Refund state created for order {OrderNumber}. RefundId={RefundId} Status={RefundStatus} ElapsedMs={ElapsedMs} EventType={EventType}",
                payment.Order.OrderNumber, refund.Id, refund.Status, stopwatch.ElapsedMilliseconds,
                refund.Status == (byte)PaymentRefundStatus.Completed ? "RefundCompleted" : "RefundRequested");
            return MapRefund(refund);
        }

        public async Task<PaymentRefundDto> CompleteRefundAsync(
            Guid refundId,
            Guid adminUserId,
            string? gatewayReference)
        {
            if (refundId == Guid.Empty || adminUserId == Guid.Empty)
                throw new BusinessException("شناسه بازپرداخت یا کاربر معتبر نیست.");
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await SqlServerTransactionLock.AcquireAsync(_dbContext, $"refund:{refundId:N}");
            var refund = await _dbContext.PaymentRefunds.Include(x => x.Payment)
                .ThenInclude(x => x.Order).ThenInclude(x => x.GiftCodeReservations).ThenInclude(x => x.GiftCode)
                .FirstOrDefaultAsync(x => x.Id == refundId)
                ?? throw new NotFoundException("بازپرداخت یافت نشد.");
            if (refund.Status == (byte)PaymentRefundStatus.Completed)
            {
                await transaction.CommitAsync();
                return MapRefund(refund);
            }
            if (refund.Method != (byte)PaymentRefundMethod.GatewayManual ||
                refund.Status != (byte)PaymentRefundStatus.Pending)
                throw new BusinessException("این بازپرداخت قابل تکمیل نیست.");
            if (string.IsNullOrWhiteSpace(gatewayReference))
                throw new BusinessException("شماره پیگیری بازپرداخت درگاه الزامی است.");

            var now = DateTime.UtcNow;
            refund.Status = (byte)PaymentRefundStatus.Completed;
            refund.CompletedAt = now;
            await CompleteRefundStateAsync(refund.Payment, refund, adminUserId, now,
                $"gateway-reference:{gatewayReference.Trim()}");
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            _logger.LogInformation(
                "Refund completed for order {OrderNumber}. RefundId={RefundId} EventType={EventType}",
                refund.Payment.Order.OrderNumber, refund.Id, "RefundCompleted");
            return MapRefund(refund);
        }

        private async Task<PaymentVerifyResultDto> CompensateVerifiedPaymentAsync(
            Guid paymentId,
            string providerReference,
            string? maskedCardPan,
            string failureReason)
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await SqlServerTransactionLock.AcquireAsync(_dbContext, $"refund:payment:{paymentId:N}");
            var payment = await _dbContext.Payments
                .Include(x => x.Order).ThenInclude(x => x.GiftCodeReservations).ThenInclude(x => x.GiftCode)
                .FirstOrDefaultAsync(x => x.Id == paymentId)
                ?? throw new NotFoundException("پرداخت یافت نشد.");
            if (payment.Status == (byte)PaymentStatus.Refunded)
            {
                await transaction.CommitAsync();
                return CreateFailedVerifyResult(payment, payment.Order);
            }

            var key = $"compensation:{payment.Id:N}";
            var refund = await _dbContext.PaymentRefunds
                .FirstOrDefaultAsync(x => x.PaymentId == payment.Id && x.IdempotencyKey == key);
            if (refund is null)
            {
                var now = DateTime.UtcNow;
                payment.ReferenceNumber = providerReference;
                payment.MaskedCardPan = maskedCardPan;
                payment.TransactionId ??= payment.Authority;
                payment.CallbackVerified = true;
                payment.VerifiedAt ??= now;
                refund = new PaymentRefund
                {
                    Id = Guid.NewGuid(), PaymentId = payment.Id, OrderId = payment.OrderId,
                    UserId = payment.UserId, Amount = payment.Amount,
                    Method = (byte)PaymentRefundMethod.Wallet,
                    Status = (byte)PaymentRefundStatus.Completed,
                    Reason = "جبران خودکار شکست تکمیل سفارش",
                    IdempotencyKey = key, RequestedAt = now, CompletedAt = now,
                    FailureReason = failureReason.Length <= 1000 ? failureReason : failureReason[..1000]
                };
                await _dbContext.PaymentRefunds.AddAsync(refund);
                await _walletService.CreditAsync(payment.UserId, payment.Amount,
                    (byte)WalletReferenceType.Refund, refund.Id,
                    $"جبران خودکار سفارش {payment.Order.OrderNumber}");
                await CompleteRefundStateAsync(payment, refund, payment.UserId, now, "automatic-compensation");
                await _dbContext.FinancialAuditLogs.AddAsync(new FinancialAuditLog
                {
                    EventType = "OrderFulfillmentCompensated", EntityType = "Order",
                    EntityId = payment.OrderId, UserId = payment.UserId, Amount = payment.Amount,
                    CorrelationId = payment.OrderId, Detail = "Provider payment verified; fulfillment failed; wallet credited.",
                    CreatedAt = now
                });
                await _dbContext.SaveChangesAsync();
            }
            await transaction.CommitAsync();
            return CreateFailedVerifyResult(payment, payment.Order);
        }

        private async Task CompleteRefundStateAsync(
            Payment payment,
            PaymentRefund refund,
            Guid adminUserId,
            DateTime now,
            string detail)
        {
            payment.Status = (byte)PaymentStatus.Refunded;
            payment.UpdatedAt = now;
            var order = payment.Order;
            var fromStatus = order.Status;
            order.PaymentStatus = (byte)PaymentStatus.Refunded;
            order.Status = (byte)OrderStatus.Refunded;
            order.UpdatedAt = now;
            foreach (var reservation in order.GiftCodeReservations.Where(x =>
                         (x.Status == (byte)GiftCodeReservationStatus.Active ||
                          x.Status == (byte)GiftCodeReservationStatus.Sold) &&
                         x.GiftCode.Status != (byte)GiftCodeStatus.Delivered))
            {
                reservation.Status = (byte)GiftCodeReservationStatus.Released;
                reservation.ReleasedAt = now;
                reservation.GiftCode.Status = (byte)GiftCodeStatus.Available;
                reservation.GiftCode.ReservedByUserId = null;
                reservation.GiftCode.ReservedAt = null;
                reservation.GiftCode.ReservationExpiresAt = null;
                reservation.GiftCode.OrderItemId = null;
                reservation.GiftCode.UpdatedAt = now;
            }
            await _dbContext.OrderStatusHistories.AddAsync(new OrderStatusHistory
            {
                Id = Guid.NewGuid(), OrderId = order.Id, FromStatus = fromStatus,
                ToStatus = order.Status, ChangedByUserId = adminUserId,
                Note = $"بازپرداخت: {refund.Reason}", CreatedAt = now
            });
            await _dbContext.FinancialAuditLogs.AddAsync(new FinancialAuditLog
            {
                EventType = "PaymentRefundCompleted", EntityType = "PaymentRefund",
                EntityId = refund.Id, UserId = adminUserId, Amount = refund.Amount,
                CorrelationId = order.Id, Detail = $"order:{order.OrderNumber};{detail}", CreatedAt = now
            });
            await _notificationService.CreateAsync(order.UserId, (byte)NotificationType.PaymentFailed,
                "بازپرداخت انجام شد", $"بازپرداخت سفارش {order.OrderNumber} ثبت شد.");
        }

        private static PaymentRefundDto MapRefund(PaymentRefund refund) => new()
        {
            Id = refund.Id, PaymentId = refund.PaymentId, OrderId = refund.OrderId,
            Amount = refund.Amount, Method = refund.Method, Status = refund.Status,
            Reason = refund.Reason, RequestedAt = refund.RequestedAt, CompletedAt = refund.CompletedAt
        };

        private async Task AddCallbackIfNotExistsAsync(
            Payment payment,
            string authority,
            string status)
        {
            var callbackKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"{authority.Trim().ToUpperInvariant()}|{status.Trim().ToUpperInvariant()}")));
            var alreadyExists = payment.PaymentCallbacks.Any(x => x.CallbackKey == callbackKey) ||
                await _dbContext.PaymentCallbacks.AnyAsync(x =>
                    x.PaymentId == payment.Id && x.CallbackKey == callbackKey);

            if (alreadyExists)
                return;

            var callbackJson = JsonSerializer.Serialize(new
            {
                authority,
                status,
                paymentId = payment.Id,
                orderId = payment.OrderId,
                receivedAt = DateTime.UtcNow
            });

            await _dbContext.PaymentCallbacks.AddAsync(new PaymentCallback
            {
                Id = Guid.NewGuid(),
                PaymentId = payment.Id,
                CallbackKey = callbackKey,
                CallbackData = callbackJson,
                CreatedAt = DateTime.UtcNow
            });
        }

        private async Task CompletePaidOrderAsync(Order order, Guid userId, DateTime now)
        {
            if (order.PaymentStatus == (byte)PaymentStatus.Paid)
                return;

            // شمارهٔ کوتاه صرفاً برای پرداختی که واقعاً موفق شده مصرف می‌شود. همهٔ مسیرهای
            // موفق (زرین‌پال، کیف پول و Mock) به این متد و یک تراکنش Serializable می‌رسند.
            order.OrderNumber = await AssignNextPaidOrderNumberAsync();
            order.PaymentStatus = (byte)PaymentStatus.Paid;
            order.Status = (byte)OrderStatus.Processing;
            order.PaidAt = now;
            order.UpdatedAt = now;

            // Managed inventory is consumed here and nowhere else. This method is the single paid
            // transition for every success path (gateway verification, wallet, reconciliation), it
            // is guarded by the PaymentStatus check above, and all three callers run it inside a
            // Serializable transaction — so consumption is exactly-once and commits atomically with
            // the paid state.
            await ConsumeManagedStockAsync(order, userId, now);

            await _dbContext.FinancialAuditLogs.AddAsync(new FinancialAuditLog
            {
                EventType = "PaymentCaptured",
                EntityType = "Order",
                EntityId = order.Id,
                UserId = userId,
                Amount = order.FinalAmount,
                CorrelationId = order.Id,
                Detail = $"order:{order.OrderNumber}",
                CreatedAt = now
            });

            if (order.CouponId.HasValue)
            {
                await _couponService.MarkCouponAsUsedAsync(
                    userId,
                    order.Id,
                    order.CouponId.Value);
            }

            var customer = await _dbContext.Users.AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => new { x.Mobile, x.Email, x.FullName })
                .FirstOrDefaultAsync();

            await _notificationService.CreateAsync(
                userId,
                (byte)NotificationType.PaymentSucceeded,
                "پرداخت موفق",
                $"پرداخت سفارش {order.OrderNumber} با موفقیت انجام شد.");

            // پیامک وضعیت سفارش از طریق Outbox؛ متن اختصاصی است تا به قالب عمومی
            // «اطلاع‌رسانی جدید» SMS.ir وابسته نباشد و شکست ارسال هم پرداخت را برنگرداند.
            await _smsOutbox.EnqueueTextAsync(
                customer?.Mobile,
                OrderSmsMessages.Processing(order.OrderNumber),
                purpose: "OrderProcessing",
                aggregateId: order.Id,
                userId: userId,
                relatedEntityType: "Order",
                relatedEntityReference: order.OrderNumber);

            if (_orderEmailOutbox is not null && customer is not null)
            {
                var emailItems = await _dbContext.OrderItems.AsNoTracking()
                    .Where(item => item.OrderId == order.Id)
                    .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
                    .Select(item => new PaidOrderEmailItem
                    {
                        ProductTitle = item.ProductTitle,
                        VariantTitle = item.VariantTitle,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    })
                    .ToListAsync();

                await _orderEmailOutbox.EnqueuePaidOrderEmailsAsync(new PaidOrderEmailRequest
                {
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber,
                    CustomerName = customer.FullName,
                    CustomerMobile = customer.Mobile,
                    CustomerEmail = customer.Email,
                    FinalAmount = order.FinalAmount,
                    Items = emailItems
                });
            }

            await _dbContext.SaveChangesAsync();

        }

        private async Task<string> AssignNextPaidOrderNumberAsync()
        {
            // sp_getapplock در SQL Server تمام پرداخت‌های موفق را روی همین شمارنده سریالی
            // می‌کند؛ بنابراین هم عدد تکراری نداریم و هم هیچ عددی قبل از پرداخت موفق مصرف نمی‌شود.
            await SqlServerTransactionLock.AcquireAsync(_dbContext, "payment:public-order-number");

            var counter = await _dbContext.OrderNumberCounters
                .SingleOrDefaultAsync(x => x.Id == 1);

            if (counter is null)
            {
                // دفاع برای محیط‌های توسعه‌ای که بدون اجرای Migration ساخته شده‌اند.
                counter = new OrderNumberCounter { Id = 1, NextNumber = 8000 };
                await _dbContext.OrderNumberCounters.AddAsync(counter);
            }

            var number = Math.Max(8000L, counter.NextNumber);
            counter.NextNumber = checked(number + 1);
            return $"vtrz-{number}";
        }

        /// <summary>
        /// Decrements managed variant inventory for a newly paid order.
        ///
        /// Instant items are skipped entirely: their inventory is the gift-code pool and is handled
        /// by the existing reservation/allocation pipeline, which this must not disturb.
        ///
        /// Each decrement is a single conditional UPDATE, so two concurrent orders competing for the
        /// last unit cannot both succeed and stock can never go negative — the database, not the
        /// application, arbitrates. A read-modify-save would lose that guarantee.
        ///
        /// Because inventory is deliberately not reserved at cart or checkout, a second buyer can
        /// still complete payment after the last unit is gone. That payment is real and is never
        /// discarded: the order stays paid and in Processing (the queue administrators already work),
        /// and a distinct financial audit event records the shortfall so it is traceable and can be
        /// resolved through the existing finance/manual path. We do not fabricate a delivery, and we
        /// do not silently drive stock negative.
        /// </summary>
        private async Task ConsumeManagedStockAsync(Order order, Guid userId, DateTime now)
        {
            var managedItems = await _dbContext.OrderItems
                .Where(oi => oi.OrderId == order.Id && oi.ProductVariantId != null)
                .Select(oi => new
                {
                    oi.Id,
                    VariantId = oi.ProductVariantId!.Value,
                    oi.Quantity,
                    DeliveryType = oi.Product.DeliveryType,
                    StockMode = oi.ProductVariant!.StockMode,
                    VariantTitle = oi.ProductVariant!.Title
                })
                .ToListAsync();

            foreach (var item in managedItems)
            {
                // Gift-code delivery consumes codes, not a quantity, and an unlimited SKU has no
                // quantity to consume: decrementing it would turn the policy into a countdown.
                if (!ProductAvailabilityRules.ConsumesStockOnPayment(
                        item.DeliveryType, (ProductVariantStockMode)item.StockMode))
                    continue;

                if (item.Quantity <= 0)
                    continue;

                var affected = await _dbContext.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.ProductVariants
SET    StockQuantity = StockQuantity - {item.Quantity}
WHERE  Id = {item.VariantId}
  AND  StockQuantity >= {item.Quantity}");

                await _dbContext.FinancialAuditLogs.AddAsync(new FinancialAuditLog
                {
                    EventType = affected == 1 ? "StockConsumed" : "StockShortfall",
                    EntityType = "ProductVariant",
                    EntityId = item.VariantId,
                    UserId = userId,
                    Amount = item.Quantity,
                    CorrelationId = order.Id,
                    Detail = affected == 1
                        ? $"order:{order.OrderNumber}; variant:{item.VariantTitle}; consumed:{item.Quantity}"
                        : $"order:{order.OrderNumber}; variant:{item.VariantTitle}; requested:{item.Quantity}; insufficient stock at payment capture — requires manual fulfilment resolution",
                    CreatedAt = now
                });
            }
        }

        private async Task ProcessPaidOrderSafelyAsync(Guid orderId)
        {
            if (_postPaymentOrderProcessor is null)
                return;

            try
            {
                await _postPaymentOrderProcessor.ProcessPaidOrderAsync(orderId);
            }
            catch (Exception ex)
            {
                // Provider/wallet financial finalization is already committed. A
                // later operational failure must be retried, not reverse payment.
                _logger.LogError(ex,
                    "Post-payment processing failed after payment capture. OrderId={OrderId}", orderId);
            }
        }

        private static PaymentVerifyResultDto CreateVerifyResult(Payment payment, Order order)
        {
            return new PaymentVerifyResultDto
            {
                PaymentId = payment.Id,
                OrderId = order.Id,
                IsPaid = true,
                ReferenceNumber = payment.ReferenceNumber,
                PaymentStatus = payment.Status,
                OrderStatus = order.Status
            };
        }

        private static PaymentVerifyResultDto CreateFailedVerifyResult(Payment payment, Order order)
        {
            return new PaymentVerifyResultDto
            {
                PaymentId = payment.Id,
                OrderId = order.Id,
                IsPaid = false,
                ReferenceNumber = payment.ReferenceNumber,
                PaymentStatus = payment.Status,
                OrderStatus = order.Status
            };
        }
    }
}
