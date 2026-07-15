namespace Vitorize.Shared.Logging;

public sealed class SeqOptions
{
    public bool Enabled { get; set; }
    public string? ServerUrl { get; set; }
    public string? ApiKey { get; set; }
    public string ApplicationName { get; set; } = "Vitorize";
    public int QueueSizeLimit { get; set; } = 10_000;

    public bool TryGetValidatedServer(out Uri? server)
    {
        server = null;
        if (!Enabled) return false;
        if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme is not ("http" or "https")) return false;
        if (!string.IsNullOrEmpty(parsed.UserInfo) || !string.IsNullOrEmpty(parsed.Query) || !string.IsNullOrEmpty(parsed.Fragment)) return false;
        server = parsed;
        return true;
    }
}

public sealed class MonitoringOptions
{
    public string? SeqUiUrl { get; set; }
    public bool ShowSeqLink { get; set; }
    public int ErrorWindowHours { get; set; } = 24;
    public int OutboxWarningThreshold { get; set; } = 20;
    public int PaymentPendingMinutes { get; set; } = 30;
    public int WorkerHeartbeatMinutes { get; set; } = 15;

    public void Normalize()
    {
        ErrorWindowHours = Math.Clamp(ErrorWindowHours, 1, 168);
        OutboxWarningThreshold = Math.Clamp(OutboxWarningThreshold, 1, 100_000);
        PaymentPendingMinutes = Math.Clamp(PaymentPendingMinutes, 1, 1440);
        WorkerHeartbeatMinutes = Math.Clamp(WorkerHeartbeatMinutes, 1, 1440);
        if (!TryGetSafeSeqUiUrl(SeqUiUrl, out _)) ShowSeqLink = false;
    }

    public static bool TryGetSafeSeqUiUrl(string? value, out string? normalized)
    {
        normalized = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            return false;
        normalized = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return true;
    }
}

public enum OperationalLogLevel
{
    Debug,
    Information,
    Warning,
    Error
}

public static class OperationalLogLevelPolicy
{
    public static OperationalLogLevel ForHttpStatus(int statusCode, bool hasException, bool isExpectedNoise)
    {
        if (hasException || statusCode >= 500) return OperationalLogLevel.Error;
        if (isExpectedNoise) return OperationalLogLevel.Debug;
        if (statusCode is 401 or 403 or 429) return OperationalLogLevel.Warning;
        return OperationalLogLevel.Information;
    }
}
