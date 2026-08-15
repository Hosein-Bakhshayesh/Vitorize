using Vitorize.Shared.Enums;

namespace Vitorize.Application.Common;

public sealed record KycRequirementEvaluation(bool RequiresKyc, KycRequirementMode Mode,
    decimal? ThresholdAmount, decimal EvaluatedAmount, Guid? PolicyVersionId,
    int? CustomerActionDeadlineHours = null);

public static class KycRequirementEvaluator
{
    public static KycRequirementEvaluation Evaluate(bool requiresVerification, byte modeValue,
        decimal? thresholdAmount, Guid? policyVersionId, decimal purchasedUnitPrice, int quantity)
    {
        if (modeValue == (byte)KycRequirementMode.None && requiresVerification && !policyVersionId.HasValue)
            return new(true, KycRequirementMode.Always, null, purchasedUnitPrice * quantity, null);

        return Evaluate(modeValue, thresholdAmount, policyVersionId, purchasedUnitPrice, quantity);
    }

    public static KycRequirementEvaluation Evaluate(byte modeValue, decimal? thresholdAmount,
        Guid? policyVersionId, decimal purchasedUnitPrice, int quantity)
    {
        if (!Enum.IsDefined(typeof(KycRequirementMode), modeValue) || purchasedUnitPrice < 0 || quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(modeValue), "Invalid KYC evaluation input.");
        var mode = (KycRequirementMode)modeValue;
        var amount = purchasedUnitPrice * quantity;
        return mode switch
        {
            KycRequirementMode.None => new(false, mode, null, amount, null),
            KycRequirementMode.Always when policyVersionId.HasValue => new(true, mode, null, amount, policyVersionId),
            KycRequirementMode.AboveThreshold when thresholdAmount is > 0 && policyVersionId.HasValue =>
                new(amount >= thresholdAmount.Value, mode, thresholdAmount, amount, policyVersionId),
            _ => throw new InvalidOperationException("KYC product configuration is invalid.")
        };
    }
}
