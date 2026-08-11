using Microsoft.Extensions.Options;
using Xunit;
using Vitorize.Application.Common;

namespace Vitorize.Tests.Unit;

public sealed class PaymentTimingOptionsTests
{
    [Fact]
    public void Default_policy_reconciles_before_instant_reservation_expires()
    {
        var options = new PaymentTimingOptions();
        var result = new PaymentTimingOptionsValidator().Validate(null, options);

        Assert.False(result.Failed);
        Assert.True(options.PendingPaymentReconciliationAgeMinutes < options.InstantCodeReservationLifetimeMinutes);
    }

    [Fact]
    public void Unsafe_reconciliation_after_reservation_expiry_is_rejected()
    {
        var options = new PaymentTimingOptions
        {
            GatewayAttemptLifetimeMinutes = 20,
            InstantCodeReservationLifetimeMinutes = 15,
            PendingPaymentReconciliationAgeMinutes = 15,
            ReconciliationIntervalSeconds = 30
        };

        var result = new PaymentTimingOptionsValidator().Validate(null, options);
        Assert.True(result.Failed);
    }
}
