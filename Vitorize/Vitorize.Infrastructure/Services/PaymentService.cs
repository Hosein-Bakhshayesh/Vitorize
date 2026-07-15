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

namespace Vitorize.Infrastructure.Services
{
    public class PaymentService : IPaymentService
    {
        private const string ZarinpalGatewayName = "Zarinpal";

        private readonly VitorizeDbContext _dbContext;
        private readonly IGiftCodeDeliveryService _giftCodeDeliveryService;
        private readonly ICouponService _couponService;
        private readonly IWalletService _walletService;
        private readonly INotificationService _notificationService;
        private readonly IZarinpalGatewayService _zarinpalGatewayService;
        private readonly ISmsOutboxEnqueuer _smsOutbox;

        public PaymentService(
            VitorizeDbContext dbContext,
            IGiftCodeDeliveryService giftCodeDeliveryService,
            ICouponService couponService,
            IWalletService walletService,
            INotificationService notificationService,
            IZarinpalGatewayService zarinpalGatewayService,
            ISmsOutboxEnqueuer smsOutbox)
        {
            _dbContext = dbContext;
            _giftCodeDeliveryService = giftCodeDeliveryService;
            _couponService = couponService;
            _walletService = walletService;
            _notificationService = notificationService;
            _zarinpalGatewayService = zarinpalGatewayService;
            _smsOutbox = smsOutbox;
        }

        public async Task<PaymentStartResultDto> StartPaymentAsync(Guid userId, Guid orderId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await SqlServerTransactionLock.AcquireAsync(_dbContext, $"payment-start:order:{orderId:N}");

            var order = await _dbContext.Orders
                .Include(x => x.User)
                .Include(x => x.Payments)
                .FirstOrDefaultAsync(x =>
                    x.Id == orderId &&
                    x.UserId == userId);

            if (order == null)
                throw new NotFoundException("سفارش یافت نشد.");

            if (order.PaymentStatus == (byte)PaymentStatus.Paid)
                throw new BusinessException("این سفارش قبلاً پرداخت شده است.");

            if (order.FinalAmount <= 0)
                throw new BusinessException("مبلغ سفارش معتبر نیست.");

            var payment = order.Payments
                .Where(x => x.Status == (byte)PaymentStatus.Pending)
                .OrderByDescending(x => x.RequestedAt)
                .FirstOrDefault();

            if (payment == null)
                throw new BusinessException("پرداخت معلق برای این سفارش یافت نشد.");

            if (payment.Amount != order.FinalAmount)
                throw new BusinessException("مبلغ پرداخت با مبلغ سفارش همخوانی ندارد.");

            if (payment.Gateway == ZarinpalGatewayName &&
                !string.IsNullOrWhiteSpace(payment.Authority))
            {
                var existingPaymentUrl =
                    await _zarinpalGatewayService.BuildPaymentUrlAsync(payment.Authority);

                await transaction.CommitAsync();
                return new PaymentStartResultDto
                {
                    PaymentId = payment.Id,
                    OrderId = order.Id,
                    Amount = payment.Amount,
                    Gateway = payment.Gateway,
                    Authority = payment.Authority,
                    PaymentUrl = existingPaymentUrl
                };
            }

            var description =
                $"پرداخت سفارش {order.OrderNumber} در Vitorize";

            var gatewayResult = await _zarinpalGatewayService.CreatePaymentAsync(
                payment.Amount,
                description,
                order.User?.Mobile,
                order.User?.Email,
                order.OrderNumber);

            if (!gatewayResult.Success)
            {
                payment.Status = (byte)PaymentStatus.Failed;
                payment.ErrorMessage = "خطا در ایجاد درخواست پرداخت زرین‌پال.";
                payment.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                throw new BusinessException("امکان اتصال به درگاه پرداخت وجود ندارد.");
            }

            payment.Gateway = ZarinpalGatewayName;
            payment.Authority = gatewayResult.Authority;
            payment.RawRequestData = JsonSerializer.Serialize(new
            {
                order.Id,
                order.OrderNumber,
                payment.Amount,
                description,
                mobile = order.User?.Mobile,
                email = order.User?.Email
            });

            payment.RawResponseData = JsonSerializer.Serialize(gatewayResult);
            payment.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return new PaymentStartResultDto
            {
                PaymentId = payment.Id,
                OrderId = order.Id,
                Amount = payment.Amount,
                Gateway = payment.Gateway,
                Authority = payment.Authority,
                PaymentUrl = gatewayResult.PaymentUrl
            };
        }
        public async Task<PaymentVerifyResultDto> VerifyZarinpalPaymentAsync(
            string authority,
            string status)
        {
            if (string.IsNullOrWhiteSpace(authority))
                throw new BusinessException("Authority معتبر نیست.");

            var normalizedStatus = string.IsNullOrWhiteSpace(status)
                ? "NOK"
                : status.Trim();

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

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

                await AddCallbackIfNotExistsAsync(payment, authority, normalizedStatus);

                if (payment.Status == (byte)PaymentStatus.Paid)
                {
                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

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

                    return CreateFailedVerifyResult(payment, order);
                }

                if (payment.Status != (byte)PaymentStatus.Pending)
                    throw new BusinessException("وضعیت پرداخت قابل تایید نیست.");

                if (payment.Amount != order.FinalAmount)
                    throw new BusinessException("مبلغ پرداخت معتبر نیست.");

                var verifyResult = await _zarinpalGatewayService.VerifyPaymentAsync(
                    authority,
                    payment.Amount);

                payment.RawResponseData = JsonSerializer.Serialize(new
                {
                    Type = "ZarinpalVerify",
                    Authority = authority,
                    Amount = payment.Amount,
                    Result = verifyResult,
                    VerifiedAt = DateTime.UtcNow
                });

                if (!verifyResult.Success)
                {
                    payment.Status = (byte)PaymentStatus.Failed;
                    payment.CallbackVerified = false;
                    payment.ProviderStatusCode = "VERIFY_FAILED";
                    payment.ErrorMessage = "تایید پرداخت زرین‌پال ناموفق بود.";
                    payment.UpdatedAt = DateTime.UtcNow;

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return CreateFailedVerifyResult(payment, order);
                }

                var now = DateTime.UtcNow;

                payment.Status = (byte)PaymentStatus.Paid;
                payment.CallbackVerified = true;
                payment.VerifiedAt = now;
                payment.UpdatedAt = now;
                payment.ReferenceNumber = verifyResult.RefId.ToString();
                payment.TransactionId = authority;
                payment.GatewayTrackingCode = verifyResult.RefId.ToString();
                payment.ProviderStatusCode = "100";

                try
                {
                    await CompletePaidOrderAsync(order, payment.UserId, now);
                }
                catch (BusinessException ex)
                {
                    await transaction.RollbackAsync();
                    return await CompensateVerifiedPaymentAsync(
                        payment.Id, verifyResult.RefId.ToString(), ex.Message);
                }

                await transaction.CommitAsync();

                return CreateVerifyResult(payment, order);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<int> ReconcilePendingZarinpalPaymentsAsync()
        {
            var threshold = DateTime.UtcNow.AddMinutes(-30);

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
                // هر پرداخت به‌صورت تازه و در تراکنش خودش پردازش می‌شود تا شکست تکمیل سفارش،
                // وضعیت Paid ناقص یا تغییرات پرداخت‌های بعدی را آلوده نکند.
                _dbContext.ChangeTracker.Clear();

                var payment = await _dbContext.Payments
                    .Include(x => x.Order)
                        .ThenInclude(x => x.GiftCodeReservations)
                    .Include(x => x.Order)
                        .ThenInclude(x => x.OrderItems)
                    .FirstOrDefaultAsync(x => x.Id == paymentId);

                if (payment == null || payment.Status != (byte)PaymentStatus.Pending)
                    continue;

                await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                string? verifiedProviderReference = null;

                try
                {
                    await SqlServerTransactionLock.AcquireAsync(_dbContext, $"payment:{paymentId:N}");
                    _dbContext.ChangeTracker.Clear();
                    payment = await _dbContext.Payments
                        .Include(x => x.Order).ThenInclude(x => x.GiftCodeReservations)
                        .Include(x => x.Order).ThenInclude(x => x.OrderItems)
                        .FirstOrDefaultAsync(x => x.Id == paymentId);
                    if (payment == null || payment.Status != (byte)PaymentStatus.Pending)
                    {
                        await transaction.CommitAsync();
                        continue;
                    }

                    var verifyResult = await _zarinpalGatewayService.VerifyPaymentAsync(
                        payment.Authority!,
                        payment.Amount);

                    payment.RawResponseData = JsonSerializer.Serialize(new
                    {
                        Type = "ZarinpalReconcile",
                        payment.Authority,
                        payment.Amount,
                        Result = verifyResult,
                        CheckedAt = DateTime.UtcNow
                    });

                    if (verifyResult.Success)
                    {
                        var now = DateTime.UtcNow;
                        verifiedProviderReference = verifyResult.RefId.ToString();

                        payment.Status = (byte)PaymentStatus.Paid;
                        payment.CallbackVerified = true;
                        payment.VerifiedAt = now;
                        payment.UpdatedAt = now;
                        payment.ReferenceNumber = verifiedProviderReference;
                        payment.TransactionId = payment.Authority;
                        payment.GatewayTrackingCode = verifiedProviderReference;
                        payment.ProviderStatusCode = "100";

                        await CompletePaidOrderAsync(payment.Order, payment.UserId, now);
                    }
                    else
                    {
                        payment.Status = (byte)PaymentStatus.Failed;
                        payment.CallbackVerified = false;
                        payment.ProviderStatusCode = "RECONCILE_FAILED";
                        payment.ErrorMessage = "پرداخت معلق پس از بررسی مجدد تایید نشد.";
                        payment.UpdatedAt = DateTime.UtcNow;

                        await _dbContext.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();

                    processed++;
                }
                catch (BusinessException ex) when (verifiedProviderReference != null)
                {
                    await transaction.RollbackAsync();
                    await CompensateVerifiedPaymentAsync(
                        paymentId, verifiedProviderReference, ex.Message);
                    processed++;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    // تغییرات ردیابی‌شده‌ی ناموفق (مثلاً Paid ناقص) نباید در ذخیره‌ی بعدی نشت کنند.
                    _dbContext.ChangeTracker.Clear();

                    var failedPayment = await _dbContext.Payments
                        .FirstOrDefaultAsync(x => x.Id == paymentId);

                    if (failedPayment != null)
                    {
                        failedPayment.ErrorMessage = $"Reconcile error: {ex.Message}";
                        failedPayment.UpdatedAt = DateTime.UtcNow;

                        await _dbContext.SaveChangesAsync();
                    }
                }
            }

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

                if (order.FinalAmount <= 0)
                    throw new BusinessException("مبلغ سفارش معتبر نیست.");

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
            return MapRefund(refund);
        }

        private async Task<PaymentVerifyResultDto> CompensateVerifiedPaymentAsync(
            Guid paymentId,
            string providerReference,
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

            order.PaymentStatus = (byte)PaymentStatus.Paid;
            order.Status = (byte)OrderStatus.Processing;
            order.PaidAt = now;
            order.UpdatedAt = now;

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

            var activeReservations = order.GiftCodeReservations
                .Where(x => x.Status == (byte)GiftCodeReservationStatus.Active)
                .ToList();

            // فقط آیتم‌های تحویل آنی رزرو کد دارند؛ سفارش کاملاً دستی رزرو ندارد و نباید خطا بدهد.
            var hasInstantItems = order.OrderItems
                .Any(x => x.DeliveryType == (byte)DeliveryType.Instant);

            if (hasInstantItems && !activeReservations.Any())
                throw new BusinessException("رزرو فعالی برای این سفارش یافت نشد.");

            foreach (var reservation in activeReservations)
            {
                reservation.Status = (byte)GiftCodeReservationStatus.Sold;
                reservation.SoldAt = now;

                var giftCode = await _dbContext.GiftCodes
                    .FirstOrDefaultAsync(x => x.Id == reservation.GiftCodeId);

                if (giftCode == null)
                    throw new BusinessException("کد رزرو شده یافت نشد.");

                giftCode.Status = (byte)GiftCodeStatus.Sold;
                giftCode.SoldAt = now;
                giftCode.ReservationExpiresAt = null;
                giftCode.UpdatedAt = now;
            }

            var mobile = await _dbContext.Users
                .Where(x => x.Id == userId)
                .Select(x => x.Mobile)
                .FirstOrDefaultAsync();

            await _notificationService.CreateAsync(
                userId,
                (byte)NotificationType.PaymentSucceeded,
                "پرداخت موفق",
                $"پرداخت سفارش {order.OrderNumber} با موفقیت انجام شد.");

            // پیامک رویداد تجاری از طریق Outbox؛ شکست ارسال هرگز پرداخت را برنمی‌گرداند.
            await _smsOutbox.EnqueueTemplateAsync(
                mobile,
                Vitorize.Application.Common.SmsTemplateKeys.OrderPaid,
                Vitorize.Application.Models.Sms.SmsBusinessNotificationParameters.OrderPaid(
                    order.OrderNumber),
                purpose: "OrderPaid",
                aggregateId: order.Id,
                userId: userId,
                relatedEntityType: "Order",
                relatedEntityReference: order.OrderNumber);

            await _dbContext.SaveChangesAsync();

            if (activeReservations.Any())
            {
                await _giftCodeDeliveryService.DeliverOrderAsync(order.Id, userId);

                await _notificationService.CreateAsync(
                    userId,
                    (byte)NotificationType.GiftCodeDelivered,
                    "تحویل سفارش",
                    $"کدهای سفارش {order.OrderNumber} با موفقیت تحویل شدند.");

                await _smsOutbox.EnqueueTemplateAsync(
                    mobile,
                    Vitorize.Application.Common.SmsTemplateKeys.GiftCodeDelivered,
                    Vitorize.Application.Models.Sms.SmsBusinessNotificationParameters.GiftCodeDelivered(
                        order.OrderNumber),
                    purpose: "GiftCodeDelivered",
                    aggregateId: order.Id,
                    userId: userId,
                    relatedEntityType: "Order",
                    relatedEntityReference: order.OrderNumber);

                await _dbContext.SaveChangesAsync();
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
