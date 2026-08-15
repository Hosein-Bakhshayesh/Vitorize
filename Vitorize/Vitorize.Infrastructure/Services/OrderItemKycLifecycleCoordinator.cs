using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Vitorize.Application.Common;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;

namespace Vitorize.Infrastructure.Services;

/// <summary>
/// Coordinates only lifecycle state. Fulfillment, delivery and inventory are
/// deliberately outside this service until Phase 2E.
/// </summary>
public sealed class OrderItemKycLifecycleCoordinator : IOrderItemKycLifecycleCoordinator
{
    private readonly VitorizeDbContext _dbContext;
    private readonly ILogger<OrderItemKycLifecycleCoordinator> _logger;
    private readonly TimeProvider _timeProvider;

    public OrderItemKycLifecycleCoordinator(
        VitorizeDbContext dbContext,
        ILogger<OrderItemKycLifecycleCoordinator>? logger = null,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _logger = logger ?? NullLogger<OrderItemKycLifecycleCoordinator>.Instance;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<int> SynchronizeSubmissionAsync(Guid userId, Guid verificationProfileId, CancellationToken cancellationToken = default)
    {
        var documents = await _dbContext.VerificationDocuments.AsNoTracking()
            .Where(x => x.UserVerificationProfileId == verificationProfileId &&
                        x.Status == (byte)VerificationStatus.Pending &&
                        x.KycDocumentTypeId.HasValue)
            .Select(x => x.KycDocumentTypeId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var states = await ManagedStatesAsync(userId,
            [(byte)OrderItemKycStatus.AwaitingSubmission, (byte)OrderItemKycStatus.Rejected], cancellationToken);
        if (states.Count == 0) return 0;

        var policyIds = states.Select(x => x.OrderItem.KycPolicyVersionId!.Value).Distinct().ToList();
        var requirements = await _dbContext.KycPolicyDocumentRequirements.AsNoTracking()
            .Where(x => policyIds.Contains(x.KycPolicyVersionId))
            .Select(x => new { x.KycPolicyVersionId, x.KycDocumentTypeId, x.IsRequired })
            .ToListAsync(cancellationToken);

        var transitioned = 0;
        foreach (var state in states)
        {
            var required = requirements.Where(x => x.KycPolicyVersionId == state.OrderItem.KycPolicyVersionId && x.IsRequired)
                .Select(x => x.KycDocumentTypeId);
            if (!required.All(documents.Contains))
                continue;

            Transition(state, OrderItemKycStatus.AwaitingReview, null);
            transitioned++;
            _logger.LogInformation(
                "Order item KYC moved to review after complete policy submission. UserId={UserId} ProfileId={ProfileId} OrderItemId={OrderItemId}",
                userId, verificationProfileId, state.OrderItemId);
        }
        return transitioned;
    }

    public async Task SynchronizeReviewAsync(Guid userId, Guid verificationProfileId, bool approved, CancellationToken cancellationToken = default)
    {
        var states = await ManagedStatesAsync(userId, [(byte)OrderItemKycStatus.AwaitingReview], cancellationToken);
        if (states.Count == 0) return;

        if (approved)
        {
            var user = await _dbContext.Users.SingleAsync(x => x.Id == userId, cancellationToken);
            if (!KycVerificationSatisfaction.IsSatisfied(user))
            {
                _logger.LogWarning(
                    "Approved verification did not satisfy the authoritative KYC gate; item states remain in review. UserId={UserId} ProfileId={ProfileId}",
                    userId, verificationProfileId);
                return;
            }
            foreach (var state in states)
                Transition(state, OrderItemKycStatus.Satisfied, verificationProfileId);
        }
        else
        {
            foreach (var state in states)
                Transition(state, OrderItemKycStatus.Rejected, null);
        }
    }

    private async Task<List<Vitorize.Domain.Entities.OrderItemKycState>> ManagedStatesAsync(
        Guid userId, IReadOnlyCollection<byte> statuses, CancellationToken cancellationToken) =>
        await _dbContext.OrderItemKycStates
            .Include(x => x.OrderItem)
            .Where(x => statuses.Contains(x.Status) && x.OrderItem.RequiresVerification &&
                        x.OrderItem.KycPolicyVersionId.HasValue &&
                        x.OrderItem.Order.UserId == userId &&
                        x.OrderItem.Order.PaymentStatus == (byte)PaymentStatus.Paid)
            .ToListAsync(cancellationToken);

    private void Transition(Vitorize.Domain.Entities.OrderItemKycState state, OrderItemKycStatus target, Guid? profileId)
    {
        var current = (OrderItemKycStatus)state.Status;
        OrderItemKycStateMachine.EnsureTransition(current, target);
        state.Status = (byte)target;
        state.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        state.CustomerActionDeadlineAt = KycCustomerActionDeadlineRules.DeadlineAfterTransition(
            target, state.OrderItem.KycCustomerActionDeadlineHours, state.UpdatedAt);
        if (target == OrderItemKycStatus.Satisfied)
        {
            state.SatisfiedAt = state.UpdatedAt;
            state.SatisfiedByVerificationProfileId = profileId;
        }
    }
}
