using Vitorize.Domain.Entities;

namespace Vitorize.Application.Interfaces;

/// <summary>
/// Promotes an instant item's existing pre-payment reservations into durable,
/// paid ownership. It never exposes gift-code secrets.
/// </summary>
public interface IPaidGiftCodeAllocationService
{
    Task EnsurePaidAllocationAsync(
        Order order,
        OrderItem orderItem,
        CancellationToken cancellationToken = default);
}
