using Vitorize.Application.DTOs.GiftCodes;

namespace Vitorize.Application.Interfaces
{
    public interface IGiftCodeReservationService
    {
        Task<GiftCodeReservationDto> ReserveAsync(
            Guid userId,
            ReserveGiftCodeRequestDto request);

        Task ReleaseAsync(
            Guid userId,
            Guid reservationId);

        Task ReleaseExpiredReservationsAsync();
    }
}