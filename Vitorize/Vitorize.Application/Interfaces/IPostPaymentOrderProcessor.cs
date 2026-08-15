namespace Vitorize.Application.Interfaces;

/// <summary>
/// Runs the non-financial, idempotent work that follows a durably paid order.
/// Payment providers and wallet payments deliberately share this entry point.
/// </summary>
public interface IPostPaymentOrderProcessor
{
    Task ProcessPaidOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
}
