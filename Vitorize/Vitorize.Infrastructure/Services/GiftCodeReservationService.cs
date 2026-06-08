using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.GiftCodes;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Shared.Enums;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class GiftCodeReservationService : IGiftCodeReservationService
    {
        private readonly VitorizeDbContext _dbContext;

        public GiftCodeReservationService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<GiftCodeReservationDto> ReserveAsync(
            Guid userId,
            ReserveGiftCodeRequestDto request)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            if (request.ProductId == Guid.Empty)
                throw new BusinessException("محصول الزامی است.");

            if (request.ReservationMinutes <= 0)
                request.ReservationMinutes = 15;

            var now = DateTime.UtcNow;
            var expiresAt = now.AddMinutes(request.ReservationMinutes);

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            var productExists = await _dbContext.Products
                .AnyAsync(x =>
                    x.Id == request.ProductId &&
                    x.IsActive &&
                    !x.IsDeleted);

            if (!productExists)
                throw new BusinessException("محصول معتبر نیست.");

            if (request.ProductVariantId.HasValue)
            {
                var variantExists = await _dbContext.ProductVariants
                    .AnyAsync(x =>
                        x.Id == request.ProductVariantId.Value &&
                        x.ProductId == request.ProductId &&
                        x.IsActive);

                if (!variantExists)
                    throw new BusinessException("تنوع محصول معتبر نیست.");
            }

            var giftCode = await _dbContext.GiftCodes
                .Where(x =>
                    x.ProductId == request.ProductId &&
                    x.ProductVariantId == request.ProductVariantId &&
                    x.Status == (byte)GiftCodeStatus.Available)
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (giftCode == null)
                throw new BusinessException("در حال حاضر کد موجود برای این محصول وجود ندارد.");

            giftCode.Status = (byte)GiftCodeStatus.Reserved;
            giftCode.ReservedByUserId = userId;
            giftCode.ReservedAt = now;
            giftCode.ReservationExpiresAt = expiresAt;
            giftCode.UpdatedAt = now;

            var reservation = new GiftCodeReservation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrderId = request.OrderId,
                OrderItemId = request.OrderItemId,
                ProductId = request.ProductId,
                ProductVariantId = request.ProductVariantId,
                GiftCodeId = giftCode.Id,
                Status = (byte)GiftCodeReservationStatus.Active,
                ReservedAt = now,
                ExpiresAt = expiresAt
            };

            await _dbContext.GiftCodeReservations.AddAsync(reservation);

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return new GiftCodeReservationDto
            {
                Id = reservation.Id,
                GiftCodeId = giftCode.Id,
                ProductId = reservation.ProductId,
                ProductVariantId = reservation.ProductVariantId,
                UserId = reservation.UserId,
                Status = reservation.Status,
                ReservedAt = reservation.ReservedAt,
                ExpiresAt = reservation.ExpiresAt,
                MaskedCode = giftCode.MaskedCode
            };
        }

        public async Task ReleaseAsync(
            Guid userId,
            Guid reservationId)
        {
            var reservation = await _dbContext.GiftCodeReservations
                .Include(x => x.GiftCode)
                .FirstOrDefaultAsync(x =>
                    x.Id == reservationId &&
                    x.UserId == userId &&
                    x.Status == (byte)GiftCodeReservationStatus.Active);

            if (reservation == null)
                throw new NotFoundException("رزرو مورد نظر یافت نشد.");

            var now = DateTime.UtcNow;

            reservation.Status = (byte)GiftCodeReservationStatus.Released;
            reservation.ReleasedAt = now;

            reservation.GiftCode.Status = (byte)GiftCodeStatus.Available;
            reservation.GiftCode.ReservedByUserId = null;
            reservation.GiftCode.ReservedAt = null;
            reservation.GiftCode.ReservationExpiresAt = null;
            reservation.GiftCode.UpdatedAt = now;

            await _dbContext.SaveChangesAsync();
        }

        public async Task ReleaseExpiredReservationsAsync()
        {
            var now = DateTime.UtcNow;

            var expiredReservations = await _dbContext.GiftCodeReservations
                .Include(x => x.GiftCode)
                .Where(x =>
                    x.Status == (byte)GiftCodeReservationStatus.Active &&
                    x.ExpiresAt <= now)
                .ToListAsync();

            if (!expiredReservations.Any())
                return;

            foreach (var reservation in expiredReservations)
            {
                reservation.Status = (byte)GiftCodeReservationStatus.Expired;
                reservation.ReleasedAt = now;

                reservation.GiftCode.Status = (byte)GiftCodeStatus.Available;
                reservation.GiftCode.ReservedByUserId = null;
                reservation.GiftCode.ReservedAt = null;
                reservation.GiftCode.ReservationExpiresAt = null;
                reservation.GiftCode.UpdatedAt = now;
            }

            await _dbContext.SaveChangesAsync();
        }
    }
}