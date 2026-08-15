using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services;

/// <summary>
/// The existing Sold reservation/code pair is the durable paid-allocation
/// model: it is assigned to one OrderItem, cannot be selected as Available,
/// and is not customer-visible until a separate OrderItemDelivery is created.
/// </summary>
public sealed class PaidGiftCodeAllocationService : IPaidGiftCodeAllocationService
{
    private readonly VitorizeDbContext _dbContext;
    private readonly ILogger<PaidGiftCodeAllocationService> _logger;

    public PaidGiftCodeAllocationService(
        VitorizeDbContext dbContext,
        ILogger<PaidGiftCodeAllocationService>? logger = null)
    {
        _dbContext = dbContext;
        _logger = logger ?? NullLogger<PaidGiftCodeAllocationService>.Instance;
    }

    public async Task EnsurePaidAllocationAsync(
        Order order,
        OrderItem orderItem,
        CancellationToken cancellationToken = default)
    {
        if (order.PaymentStatus != (byte)PaymentStatus.Paid ||
            orderItem.DeliveryType != (byte)DeliveryType.Instant)
            return;

        await SqlServerTransactionLock.AcquireAsync(
            _dbContext,
            $"gift-reservation:{orderItem.ProductId:N}:{orderItem.ProductVariantId?.ToString("N") ?? "none"}",
            cancellationToken);

        var reservations = await _dbContext.GiftCodeReservations
            .Include(x => x.GiftCode)
            .Where(x => x.OrderId == order.Id && x.OrderItemId == orderItem.Id)
            .ToListAsync(cancellationToken);
        // Earlier cancelled/retry attempts remain historical rows. Only the
        // current Active/Sold ownership chain participates in this invariant.
        var currentOwnership = reservations.Where(x =>
            x.Status is (byte)GiftCodeReservationStatus.Active or (byte)GiftCodeReservationStatus.Sold).ToList();
        var sold = currentOwnership.Where(x =>
            x.Status == (byte)GiftCodeReservationStatus.Sold &&
            x.GiftCode.Status is (byte)GiftCodeStatus.Sold or (byte)GiftCodeStatus.Delivered).ToList();

        if (sold.Count == orderItem.Quantity)
        {
            _logger.LogDebug(
                "Paid gift-code allocation already exists. OrderId={OrderId} OrderItemId={OrderItemId} Quantity={Quantity}",
                order.Id, orderItem.Id, orderItem.Quantity);
            return;
        }

        if (sold.Count != 0 || currentOwnership.Count != orderItem.Quantity ||
            currentOwnership.Any(x => x.Status != (byte)GiftCodeReservationStatus.Active ||
                                  x.GiftCode.Status != (byte)GiftCodeStatus.Reserved))
        {
            _logger.LogCritical(
                "Paid gift-code allocation invariant failed. OrderId={OrderId} OrderItemId={OrderItemId} RequiredQuantity={RequiredQuantity} SoldCount={SoldCount} ReservationCount={ReservationCount}",
                order.Id, orderItem.Id, orderItem.Quantity, sold.Count, currentOwnership.Count);
            throw new BusinessException("تخصیص پایدار کد برای سفارش پرداخت‌شده کامل نیست و نیاز به پیگیری دارد.");
        }

        var now = DateTime.UtcNow;
        foreach (var reservation in currentOwnership)
        {
            reservation.Status = (byte)GiftCodeReservationStatus.Sold;
            reservation.SoldAt = now;
            reservation.GiftCode.Status = (byte)GiftCodeStatus.Sold;
            reservation.GiftCode.OrderItemId = orderItem.Id;
            reservation.GiftCode.SoldAt = now;
            reservation.GiftCode.ReservationExpiresAt = null;
            reservation.GiftCode.UpdatedAt = now;
        }

        _logger.LogInformation(
            "Paid gift-code allocation promoted. OrderId={OrderId} OrderItemId={OrderItemId} Quantity={Quantity}",
            order.Id, orderItem.Id, orderItem.Quantity);
    }
}
