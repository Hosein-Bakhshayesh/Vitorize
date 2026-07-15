namespace Vitorize.Web.Models.Admin.System;

public sealed class AdminMonitoringModel
{
    public string ApiStatus { get; set; } = string.Empty;
    public bool DatabaseReady { get; set; }
    public string ExpectedSchemaVersion { get; set; } = string.Empty;
    public string? CurrentSchemaVersion { get; set; }
    public DateTime? LastDeploymentAt { get; set; }
    public string ApplicationVersion { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public int PendingPayments { get; set; }
    public int FailedPaymentsLast24Hours { get; set; }
    public int RefundFailures { get; set; }
    public int WalletCompensationFailures { get; set; }
    public int PendingManualDeliveries { get; set; }
    public int FailedGiftCodeDeliveries { get; set; }
    public int OutboxPending { get; set; }
    public int OutboxRetrying { get; set; }
    public int OutboxStuck { get; set; }
    public int OutboxDeadLetter { get; set; }
    public int SmsFailed { get; set; }
    public int SmsDeadLetter { get; set; }
    public int KycConversionFailures { get; set; }
    public int ErrorLogsInWindow { get; set; }
    public int SecurityWarningsInWindow { get; set; }
    public int ErrorWindowHours { get; set; }
    public int OutboxWarningThreshold { get; set; }
    public bool ShowSeqLink { get; set; }
    public string? SeqUiUrl { get; set; }
    public List<WorkerHeartbeatModel> Workers { get; set; } = [];
}

public sealed class WorkerHeartbeatModel
{
    public string WorkerName { get; set; } = string.Empty;
    public DateTime LastHeartbeatUtc { get; set; }
    public bool IsHealthy { get; set; }
    public int LastBatchCount { get; set; }
    public long LastDurationMs { get; set; }
    public string? LastOutcome { get; set; }
}
