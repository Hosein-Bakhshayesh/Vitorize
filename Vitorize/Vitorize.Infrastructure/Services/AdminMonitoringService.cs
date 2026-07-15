using System.Data;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Vitorize.Application.DTOs.Admin.System;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Logging;

namespace Vitorize.Infrastructure.Services;

public sealed class AdminMonitoringService : IAdminMonitoringService
{
    private readonly VitorizeDbContext _db;
    private readonly IWorkerHeartbeatRegistry _heartbeats;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly MonitoringOptions _options;

    public AdminMonitoringService(
        VitorizeDbContext db,
        IWorkerHeartbeatRegistry heartbeats,
        IHostEnvironment environment,
        IConfiguration configuration,
        IOptions<MonitoringOptions> options)
    {
        _db = db;
        _heartbeats = heartbeats;
        _environment = environment;
        _configuration = configuration;
        _options = options.Value;
        _options.Normalize();
    }

    public async Task<AdminMonitoringDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var window = now.AddHours(-_options.ErrorWindowHours);
        var pendingCutoff = now.AddMinutes(-_options.PaymentPendingMinutes);
        var heartbeatWindow = TimeSpan.FromMinutes(_options.WorkerHeartbeatMinutes);
        var databaseReady = await _db.Database.CanConnectAsync(cancellationToken);
        var deployment = databaseReady ? await ReadLastDeploymentAsync(cancellationToken) : default;
        MonitoringOptions.TryGetSafeSeqUiUrl(_options.SeqUiUrl, out var seqUiUrl);

        return new AdminMonitoringDto
        {
            ApiStatus = databaseReady ? "Healthy" : "Degraded",
            DatabaseReady = databaseReady,
            ExpectedSchemaVersion = _configuration["Database:ExpectedSchemaVersion"] ?? "V0005",
            CurrentSchemaVersion = deployment.Version,
            LastDeploymentAt = deployment.AppliedAt,
            ApplicationVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
            Environment = _environment.EnvironmentName,
            GeneratedAtUtc = now,
            PendingPayments = await _db.Payments.CountAsync(x => x.Status == (byte)PaymentStatus.Pending && x.RequestedAt <= pendingCutoff, cancellationToken),
            FailedPaymentsLast24Hours = await _db.Payments.CountAsync(x => x.Status == (byte)PaymentStatus.Failed && x.UpdatedAt >= now.AddHours(-24), cancellationToken),
            RefundFailures = await _db.PaymentRefunds.CountAsync(x => x.Status == (byte)PaymentRefundStatus.Failed && x.RequestedAt >= window, cancellationToken),
            WalletCompensationFailures = await _db.ErrorLogs.CountAsync(x => x.CreatedAt >= window && x.Message.Contains("compensation"), cancellationToken),
            PendingManualDeliveries = await _db.OrderItems.CountAsync(x => x.DeliveryType != (byte)DeliveryType.Instant && x.DeliveryStatus == (byte)DeliveryStatus.Pending, cancellationToken),
            FailedGiftCodeDeliveries = await _db.OrderItems.CountAsync(x => x.DeliveryType == (byte)DeliveryType.Instant && x.DeliveryStatus == (byte)DeliveryStatus.Failed, cancellationToken),
            OutboxPending = await _db.OutboxMessages.CountAsync(x => x.Status == (byte)OutboxMessageStatus.Pending && x.RetryCount == 0, cancellationToken),
            OutboxRetrying = await _db.OutboxMessages.CountAsync(x => x.Status == (byte)OutboxMessageStatus.Pending && x.RetryCount > 0, cancellationToken),
            OutboxStuck = await _db.OutboxMessages.CountAsync(x => x.Status == (byte)OutboxMessageStatus.Processing && x.LockedAt < now.AddMinutes(-5), cancellationToken),
            OutboxDeadLetter = await _db.OutboxMessages.CountAsync(x => x.Status == (byte)OutboxMessageStatus.Failed, cancellationToken),
            SmsFailed = await _db.SmsMessages.CountAsync(x => x.Status == (byte)SmsMessageStatus.Failed && x.CreatedAt >= window, cancellationToken),
            SmsDeadLetter = await _db.SmsMessages.CountAsync(x => x.Status == (byte)SmsMessageStatus.DeadLetter && x.CreatedAt >= window, cancellationToken),
            KycConversionFailures = await _db.ErrorLogs.CountAsync(x => x.CreatedAt >= window && (x.Source!.Contains("Kyc") || x.Message.Contains("KYC")), cancellationToken),
            ErrorLogsInWindow = await _db.ErrorLogs.CountAsync(x => x.CreatedAt >= window, cancellationToken),
            SecurityWarningsInWindow = await _db.SecurityLogs.CountAsync(x => x.CreatedAt >= window && !x.IsSuccessful, cancellationToken),
            ErrorWindowHours = _options.ErrorWindowHours,
            OutboxWarningThreshold = _options.OutboxWarningThreshold,
            ShowSeqLink = _options.ShowSeqLink && seqUiUrl is not null,
            SeqUiUrl = _options.ShowSeqLink ? seqUiUrl : null,
            Workers = _heartbeats.Snapshot(heartbeatWindow).ToList()
        };
    }

    private async Task<(string? Version, DateTime? AppliedAt)> ReadLastDeploymentAsync(CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        var close = connection.State != ConnectionState.Open;
        if (close) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT TOP (1) ScriptVersion, AppliedAt FROM dbo.DatabaseScriptHistory WHERE Success = 1 ORDER BY AppliedAt DESC;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return default;
            return (reader.GetString(0), reader.GetDateTime(1));
        }
        catch
        {
            return default;
        }
        finally
        {
            if (close) await connection.CloseAsync();
        }
    }
}
