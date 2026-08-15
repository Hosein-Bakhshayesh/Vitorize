namespace Vitorize.Application.Interfaces;

/// <summary>
/// Releases only paid, KYC-satisfied item-level fulfillment. It is safe to
/// invoke again after a post-commit failure or process restart.
/// </summary>
public interface IOrderItemFulfillmentReleaseService
{
    Task ReleaseSatisfiedItemsForVerificationAsync(Guid verificationProfileId, CancellationToken cancellationToken = default);

    Task ReleaseSatisfiedOrderItemAsync(Guid orderItemId, CancellationToken cancellationToken = default);
}
