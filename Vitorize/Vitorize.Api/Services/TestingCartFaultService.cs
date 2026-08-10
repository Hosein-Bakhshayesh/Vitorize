namespace Vitorize.Api.Services;

/// <summary>One-shot fault switch registered only by Development/Testing hosts.</summary>
public sealed class TestingCartFaultService
{
    private int _pendingCartReadFailures;

    public void FailNextCartRead() => Interlocked.Exchange(ref _pendingCartReadFailures, 1);

    public bool ConsumeCartReadFailure() => Interlocked.Exchange(ref _pendingCartReadFailures, 0) == 1;
}
