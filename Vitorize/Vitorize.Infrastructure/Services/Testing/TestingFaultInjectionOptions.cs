namespace Vitorize.Infrastructure.Services.Testing;

/// <summary>
/// Testing-environment-only fault injection for Phase 4 resilience and chaos testing.
///
/// Every mode defaults to <c>Off</c>, and the values are honoured ONLY when the host runs in the
/// "Testing" environment: the consuming fakes hard-guard on
/// <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.IsEnvironment(string)"/>, so this can
/// never alter Production or Development behaviour even if the configuration section is present.
///
/// Bind from configuration section <see cref="SectionName"/>. Example (Testing appsettings or env):
/// <code>
/// { "Testing": { "FaultInjection": { "Sms": "Timeout", "Payment": "VerifyFail", "DelayMs": 250 } } }
/// </code>
/// </summary>
public sealed class TestingFaultInjectionOptions
{
    public const string SectionName = "Testing:FaultInjection";

    /// <summary>SMS transport fault: Off | Network | Timeout | Unavailable | Fail.</summary>
    public string Sms { get; set; } = "Off";

    /// <summary>Payment gateway fault: Off | CreateFail | VerifyFail.</summary>
    public string Payment { get; set; } = "Off";

    /// <summary>Optional artificial latency (milliseconds) added before a faked response. 0 = none.</summary>
    public int DelayMs { get; set; }

    public bool IsSmsFaultRequested =>
        !string.IsNullOrWhiteSpace(Sms) && !Sms.Equals("Off", StringComparison.OrdinalIgnoreCase);

    public bool IsPaymentFaultRequested =>
        !string.IsNullOrWhiteSpace(Payment) && !Payment.Equals("Off", StringComparison.OrdinalIgnoreCase);
}
