using Vitorize.Application.DTOs.Verification;

namespace Vitorize.Application.Interfaces;

/// <summary>Authoritative mutations for persisted customer-action KYC deadlines.</summary>
public interface IOrderItemKycDeadlineService
{
    /// <summary>Requires the caller's serialized verification transaction/user lock.</summary>
    Task<CustomerDeadlineEnforcementResult> EnforceCustomerActionsWithinTransactionAsync(
        Guid userId, Guid? orderItemId = null, CancellationToken cancellationToken = default);

    Task<bool> ExpireIfOverdueAsync(Guid orderItemId, CancellationToken cancellationToken = default);
    Task<int> ProcessOverdueBatchAsync(int batchSize, CancellationToken cancellationToken = default);
    Task<OrderItemKycDeadlineOperationDto> ExtendDeadlineAsync(Guid orderItemId, DateTime newDeadlineAtUtc,
        Guid adminUserId, CancellationToken cancellationToken = default);
    Task<OrderItemKycDeadlineOperationDto> ReopenExpiredAsync(Guid orderItemId, DateTime newDeadlineAtUtc,
        Guid adminUserId, CancellationToken cancellationToken = default);
    Task<OrderItemKycDeadlineOperationDto> FinalRejectExpiredAsync(Guid orderItemId, Guid adminUserId,
        CancellationToken cancellationToken = default);
}
