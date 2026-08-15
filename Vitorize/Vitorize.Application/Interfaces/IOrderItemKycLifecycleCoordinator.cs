namespace Vitorize.Application.Interfaces;

/// <summary>Synchronizes the real verification workflow with paid item KYC states.</summary>
public interface IOrderItemKycLifecycleCoordinator
{
    Task<int> SynchronizeSubmissionAsync(Guid userId, Guid verificationProfileId, CancellationToken cancellationToken = default);
    Task SynchronizeReviewAsync(Guid userId, Guid verificationProfileId, bool approved, CancellationToken cancellationToken = default);
}
