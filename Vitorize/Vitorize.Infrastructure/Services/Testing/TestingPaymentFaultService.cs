namespace Vitorize.Infrastructure.Services.Testing;

/// <summary>
/// Runtime fault switch used only by isolated browser verification. Production callers never map
/// a control endpoint and consumers additionally guard on the Testing environment.
/// </summary>
public sealed class TestingPaymentFaultService
{
    private string _mode = "Off";

    public bool BlockMockVerification => string.Equals(Volatile.Read(ref _mode), "MockVerifyFail", StringComparison.OrdinalIgnoreCase);

    public void Set(string? mode)
    {
        var normalized = string.IsNullOrWhiteSpace(mode) ? "Off" : mode.Trim();
        if (!string.Equals(normalized, "Off", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalized, "MockVerifyFail", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Unsupported testing payment fault mode.", nameof(mode));
        Volatile.Write(ref _mode, normalized);
    }
}
