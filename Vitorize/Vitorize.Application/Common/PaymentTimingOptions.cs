using Microsoft.Extensions.Options;

namespace Vitorize.Application.Common;

public sealed class PaymentTimingOptions
{
    public const string SectionName = "PaymentTiming";

    public int GatewayAttemptLifetimeMinutes { get; init; } = 30;
    public int InstantCodeReservationLifetimeMinutes { get; init; } = 60;
    public int PendingPaymentReconciliationAgeMinutes { get; init; } = 10;
    public int ReconciliationIntervalSeconds { get; init; } = 60;
}

public sealed class PaymentTimingOptionsValidator : IValidateOptions<PaymentTimingOptions>
{
    public ValidateOptionsResult Validate(string? name, PaymentTimingOptions options)
    {
        if (options.GatewayAttemptLifetimeMinutes <= 0 ||
            options.InstantCodeReservationLifetimeMinutes <= 0 ||
            options.PendingPaymentReconciliationAgeMinutes <= 0 ||
            options.ReconciliationIntervalSeconds <= 0)
            return ValidateOptionsResult.Fail("Payment timing values must all be positive.");

        if (options.PendingPaymentReconciliationAgeMinutes >= options.InstantCodeReservationLifetimeMinutes)
            return ValidateOptionsResult.Fail(
                "PaymentTiming:PendingPaymentReconciliationAgeMinutes must be less than PaymentTiming:InstantCodeReservationLifetimeMinutes.");

        if (options.GatewayAttemptLifetimeMinutes >= options.InstantCodeReservationLifetimeMinutes)
            return ValidateOptionsResult.Fail(
                "PaymentTiming:GatewayAttemptLifetimeMinutes must be less than PaymentTiming:InstantCodeReservationLifetimeMinutes so an expired attempt can safely recover inventory.");

        return ValidateOptionsResult.Success;
    }
}
