using Vitorize.Application.Common;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests.Unit;

public sealed class KycRequirementEvaluatorTests
{
    [Fact]
    public void Above_threshold_uses_authoritative_unit_price_times_quantity_at_equality()
    {
        var policyVersionId = Guid.NewGuid();

        var result = KycRequirementEvaluator.Evaluate((byte)KycRequirementMode.AboveThreshold,
            300m, policyVersionId, 100m, 3);

        Assert.True(result.RequiresKyc);
        Assert.Equal(300m, result.EvaluatedAmount);
        Assert.Equal(policyVersionId, result.PolicyVersionId);
    }

    [Fact]
    public void Above_threshold_does_not_use_order_coupon_discount()
    {
        // The evaluator intentionally accepts only the item price and quantity;
        // therefore an order-level coupon cannot change this policy decision.
        var result = KycRequirementEvaluator.Evaluate((byte)KycRequirementMode.AboveThreshold,
            500m, Guid.NewGuid(), 250m, 2);

        Assert.True(result.RequiresKyc);
        Assert.Equal(500m, result.EvaluatedAmount);
    }

    [Fact]
    public void None_clears_policy_snapshot_and_never_requires_kyc()
    {
        var result = KycRequirementEvaluator.Evaluate((byte)KycRequirementMode.None,
            null, null, 100m, 1);

        Assert.False(result.RequiresKyc);
        Assert.Null(result.PolicyVersionId);
        Assert.Null(result.ThresholdAmount);
    }
}
