using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Vitorize.Api.Controllers.Admin;
using Vitorize.Api.Middlewares;
using Vitorize.Application.DTOs.Admin.System;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Services;
using Vitorize.Shared.Logging;
using Vitorize.Shared.Exceptions;
using WebCorrelationMiddleware = Vitorize.Web.Services.CorrelationIdMiddleware;
using Xunit;

namespace Vitorize.Tests;

public sealed class MonitoringAndLoggingTests
{
    [Theory]
    [InlineData("order.VT-123_abc-9")]
    [InlineData("0123456789abcdef0123456789abcdef")]
    public void Correlation_policy_accepts_bounded_safe_values(string value)
    {
        Assert.True(CorrelationIdPolicy.IsValid(value));
        Assert.Equal(value, CorrelationIdPolicy.Resolve(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" leading")]
    [InlineData("line\r\nforged")]
    [InlineData("has/slash")]
    public void Correlation_policy_replaces_invalid_values(string value)
    {
        var resolved = CorrelationIdPolicy.Resolve(value);
        Assert.NotEqual(value, resolved);
        Assert.True(CorrelationIdPolicy.IsValid(resolved));
        Assert.Equal(32, resolved.Length);
    }

    [Fact]
    public void Correlation_policy_rejects_oversized_input()
    {
        Assert.False(CorrelationIdPolicy.IsValid(new string('a', CorrelationIdPolicy.MaximumLength + 1)));
    }

    [Fact]
    public async Task Correlation_middleware_propagates_valid_id_to_context_activity_and_response()
    {
        const string expected = "web-api-flow-123";
        using var activity = new Activity("test").Start();
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdPolicy.HeaderName] = expected;
        var middleware = new WebCorrelationMiddleware(async current =>
        {
            Assert.Equal(expected, CorrelationContext.Current);
            Assert.Equal(expected, current.Items[CorrelationIdPolicy.HeaderName]);
            Assert.Equal(expected, Activity.Current?.GetTagItem("vitorize.correlation_id"));
            await current.Response.StartAsync();
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(expected, context.Response.Headers[CorrelationIdPolicy.HeaderName]);
        Assert.Null(CorrelationContext.Current);
    }

    [Fact]
    public void Sensitive_property_policy_is_context_aware_for_code()
    {
        Assert.True(SensitiveLogData.IsSensitiveProperty("PasswordHash"));
        Assert.True(SensitiveLogData.IsSensitiveProperty("RefreshToken"));
        Assert.True(SensitiveLogData.IsSensitiveProperty("KycDocumentContent"));
        Assert.True(SensitiveLogData.IsSensitiveProperty("Key"));
        Assert.False(SensitiveLogData.IsSensitiveProperty("ProductCode"));
        Assert.False(SensitiveLogData.IsSensitiveProperty("OrderNumber"));
    }

    [Fact]
    public void Mobile_and_email_are_masked_without_losing_operational_shape()
    {
        Assert.Equal("091***6789", SensitiveLogData.MaskMobile("09123456789"));
        Assert.Equal("a***@example.test", SensitiveLogData.MaskEmail("admin@example.test"));
    }

    [Fact]
    public void Free_text_redaction_removes_secrets_personal_data_and_log_forging()
    {
        const string password = "NeverLogMe!";
        const string token = "eyJabcdefghijk.abcdefghijklmnop.abcdefghijklmnop";
        var result = SensitiveLogData.RedactFreeText(
            $"password={password}\r\n mobile=09123456789 email=admin@example.test token={token}");

        Assert.DoesNotContain(password, result);
        Assert.DoesNotContain(token, result);
        Assert.DoesNotContain("09123456789", result);
        Assert.DoesNotContain("admin@example.test", result);
        Assert.DoesNotContain('\r', result);
        Assert.DoesNotContain('\n', result);
        Assert.Contains(SensitiveLogData.Redacted, result);
    }

    [Theory]
    [InlineData(true, "http://seq:5341", true)]
    [InlineData(true, "https://logs.internal.example", true)]
    [InlineData(true, "ftp://seq/events", false)]
    [InlineData(true, "https://user:password@seq.example", false)]
    [InlineData(true, "https://seq.example/?apiKey=secret", false)]
    [InlineData(false, "http://seq:5341", false)]
    public void Seq_configuration_requires_enabled_safe_http_endpoint(bool enabled, string url, bool expected)
    {
        var options = new SeqOptions { Enabled = enabled, ServerUrl = url };
        Assert.Equal(expected, options.TryGetValidatedServer(out _));
    }

    [Fact]
    public void Monitoring_thresholds_are_bounded_and_secret_bearing_links_are_hidden()
    {
        var options = new MonitoringOptions
        {
            ErrorWindowHours = 999,
            OutboxWarningThreshold = 0,
            PaymentPendingMinutes = -1,
            WorkerHeartbeatMinutes = 2000,
            ShowSeqLink = true,
            SeqUiUrl = "https://seq.example/?apiKey=secret"
        };

        options.Normalize();

        Assert.Equal(168, options.ErrorWindowHours);
        Assert.Equal(1, options.OutboxWarningThreshold);
        Assert.Equal(1, options.PaymentPendingMinutes);
        Assert.Equal(1440, options.WorkerHeartbeatMinutes);
        Assert.False(options.ShowSeqLink);
    }

    [Theory]
    [InlineData(200, false, false, OperationalLogLevel.Information)]
    [InlineData(404, false, true, OperationalLogLevel.Debug)]
    [InlineData(401, false, false, OperationalLogLevel.Warning)]
    [InlineData(429, false, false, OperationalLogLevel.Warning)]
    [InlineData(500, false, false, OperationalLogLevel.Error)]
    [InlineData(400, true, false, OperationalLogLevel.Error)]
    public void Request_log_level_policy_classifies_outcomes(
        int status, bool hasException, bool noise, OperationalLogLevel expected)
        => Assert.Equal(expected, OperationalLogLevelPolicy.ForHttpStatus(status, hasException, noise));

    [Fact]
    public void Worker_heartbeat_registry_exposes_only_bounded_safe_summary()
    {
        var registry = new WorkerHeartbeatRegistry();
        registry.Record("Outbox\r\nInjected", 3, TimeSpan.FromMilliseconds(24), "Succeeded\r\nForged");

        var heartbeat = Assert.Single(registry.Snapshot(TimeSpan.FromMinutes(1)));
        Assert.True(heartbeat.IsHealthy);
        Assert.Equal(3, heartbeat.LastBatchCount);
        Assert.DoesNotContain('\r', heartbeat.WorkerName);
        Assert.DoesNotContain('\n', heartbeat.LastOutcome!);
    }

    [Fact]
    public void Monitoring_endpoint_requires_security_diagnostics_permission()
    {
        var attribute = Assert.Single(typeof(AdminMonitoringController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal("SecurityDiagnostics", attribute.Policy);
    }

    [Fact]
    public void Monitoring_payload_contract_contains_no_seq_key_or_sensitive_fields()
    {
        var json = JsonSerializer.Serialize(new AdminMonitoringDto
        {
            SeqUiUrl = "https://seq.internal.example",
            ShowSeqLink = true
        });

        Assert.DoesNotContain("ApiKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Token", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Business_validation_is_not_logged_as_error_or_persisted()
    {
        var logger = new CapturingLogger<GlobalExceptionMiddleware>();
        var errorLog = new CountingErrorLogService();
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new BusinessException("ورودی معتبر نیست."), logger);
        var context = NewHttpContext();

        await middleware.InvokeAsync(context, errorLog);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.DoesNotContain(logger.Entries, x => x.Level >= LogLevel.Error);
        Assert.Equal(0, errorLog.Count);
    }

    [Fact]
    public async Task Unhandled_exception_is_logged_once_with_redacted_context_and_persisted_once()
    {
        var logger = new CapturingLogger<GlobalExceptionMiddleware>();
        var errorLog = new CountingErrorLogService();
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new InvalidOperationException("password=NeverCapture 09123456789"), logger);
        var context = NewHttpContext();
        context.Items[CorrelationIdPolicy.HeaderName] = "exception-correlation";

        await middleware.InvokeAsync(context, errorLog);

        var entry = Assert.Single(logger.Entries, x => x.Level == LogLevel.Error);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal(1, errorLog.Count);
        Assert.DoesNotContain("NeverCapture", entry.Message);
        Assert.DoesNotContain("09123456789", entry.Message);
        Assert.Contains(SensitiveLogData.Redacted, entry.Message);
    }

    private static DefaultHttpContext NewHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/test";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class CountingErrorLogService : IErrorLogService
    {
        public int Count { get; private set; }
        public Task LogAsync(Exception exception) { Count++; return Task.CompletedTask; }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
    }
}
