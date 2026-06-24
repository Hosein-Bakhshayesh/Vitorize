using Microsoft.EntityFrameworkCore;
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

        public PaymentService(
            VitorizeDbContext dbContext,
            IGiftCodeDeliveryService giftCodeDeliveryService,
            ICouponService couponService,
            IWalletService walletService,
            INotificationService notificationService,
            IZarinpalGatewayService zarinpalGatewayService)
        {
            _dbContext = dbContext;
            _giftCodeDeliveryService = giftCodeDeliveryService;
            _couponService = couponService;
            _walletService = walletService;
            _notificationService = notificationService;
            _zarinpalGatewayService = zarinpalGatewayService;
        }

        public async Task<PaymentStartResultDto> StartPaymentAsync(Guid userId, Guid orderId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

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

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
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

                await CompletePaidOrderAsync(order, payment.UserId, now);

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

            var payments = await _dbContext.Payments
                .Include(x => x.Order)
                    .ThenInclude(x => x.GiftCodeReservations)
                .Include(x => x.Order)
                    .ThenInclude(x => x.OrderItems)
                .Where(x =>
                    x.Gateway == ZarinpalGatewayName &&
                    x.Status == (byte)PaymentStatus.Pending &&
                    x.Authority != null &&
                    x.RequestedAt <= threshold)
                .OrderBy(x => x.RequestedAt)
                .Take(50)
                .ToListAsync();

            var processed = 0;

            foreach (var payment in payments)
            {
                try
                {
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

                        payment.Status = (byte)PaymentStatus.Paid;
                        payment.CallbackVerified = true;
                        payment.VerifiedAt = now;
                        payment.UpdatedAt = now;
                        payment.ReferenceNumber = verifyResult.RefId.ToString();
                        payment.TransactionId = payment.Authority;
                        payment.GatewayTrackingCode = verifyResult.RefId.ToString();
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

                    processed++;
                }
                catch (Exception ex)
                {
                    payment.ErrorMessage = $"Reconcile error: {ex.Message}";
                    payment.UpdatedAt = DateTime.UtcNow;

                    await _dbContext.SaveChangesAsync();
                }
            }

            return processed;
        }

        public async Task<PaymentVerifyResultDto> VerifyMockPaymentAsync(Guid userId, Guid paymentId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
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

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
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

        private async Task AddCallbackIfNotExistsAsync(
            Payment payment,
            string authority,
            string status)
        {
            var alreadyExists = payment.PaymentCallbacks.Any(x =>
                x.CallbackData.Contains($"\"authority\":\"{authority}\"") &&
                x.CallbackData.Contains($"\"status\":\"{status}\""));

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

            if (!activeReservations.Any())
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

            await _notificationService.CreateAsync(
                userId,
                (byte)NotificationType.PaymentSucceeded,
                "پرداخت موفق",
                $"پرداخت سفارش {order.OrderNumber} با موفقیت انجام شد.");

            await _dbContext.SaveChangesAsync();

            await _giftCodeDeliveryService.DeliverOrderAsync(order.Id, userId);

            await _notificationService.CreateAsync(
                userId,
                (byte)NotificationType.GiftCodeDelivered,
                "تحویل سفارش",
                $"کدهای سفارش {order.OrderNumber} با موفقیت تحویل شدند.");
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