# Monitoring and structured logging

## Architecture and ownership

Both `Vitorize.Api` and `Vitorize.Web` emit through `Microsoft.Extensions.Logging.ILogger<T>`. Serilog is installed only in the two composition roots and writes to console, a bounded rolling file, and optionally Seq. Domain and Application do not reference Serilog.

- `AuditLogs` and `FinancialAuditLogs`: durable business/admin changes, permission changes, financial actions, and sensitive-value reveal audit.
- `SecurityLogs`: authentication, authorization, bootstrap, and protected-content access.
- `ErrorLogs`: selected failures requiring an Admin action, not every technical exception.
- Seq: searchable technical/operational events, performance, request completion, and cross-service diagnosis.

Do not add a SQL technical-log sink. This phase has no database or EF migration change.

## Packages and host behavior

Both hosts pin `Serilog.AspNetCore` 8.0.3, `Serilog.Sinks.Seq` 8.0.0, `Serilog.Sinks.Console` 6.0.0, `Serilog.Sinks.File` 6.0.0, `Serilog.Enrichers.Environment` 3.0.1, and `Serilog.Enrichers.Thread` 4.0.0.

Each host creates a bootstrap console logger before building, replaces it once using configuration and DI, records fatal startup failures, and flushes during shutdown. Final events include `Application`, `Service`, `Environment`, `MachineName`, `InstanceId`, `Version`, `ThreadId`, `TraceId`, and `SpanId` where available.

Committed configuration enables console and daily files only. Runtime Seq configuration is private:

```text
Seq__Enabled=true
Seq__ServerUrl=http://seq:5341
Seq__ApiKey=<environment-specific ingestion key>
Seq__ApplicationName=Vitorize.Api
```

Use `Vitorize.Web` for the Web host. URLs must be absolute HTTP(S) endpoints with no user-info, query, or fragment. Invalid configuration disables the sink with a safe warning. The sink uses a bounded 10,000-event in-memory queue and never blocks business traffic. If Seq is unavailable, console and rolling files continue; queued events can be lost on process termination, so files are the outage fallback.

## Correlation, requests, and redaction

`X-Correlation-ID` accepts only 1–64 ASCII letters, digits, dot, underscore, or hyphen. Invalid input is replaced by a random 128-bit Activity trace ID. It is written to response headers, LogContext, Activity tags/baggage, error responses, and Web-to-API `ApiClient` calls. Outbox processing establishes an explicit message scope.

Request completion events contain method, path, route/controller/action, status, elapsed time, trace identifier, correlation ID, and safe authenticated identity metadata. Bodies, query strings, authorization, cookies, tokens, OTPs, KYC data, uploads, and callback payloads are excluded. Static assets, health probes, and expected 404s are Debug; authorization/rate failures are Warning; 5xx is Error.

`SensitiveLogData` provides recursive property-name redaction, bounded control-character stripping, masked mobile/email, and free-text secret/JWT/personal-data redaction. Do not destructure DTOs or EF entities. Generic `Code` is not automatically removed because it can be a public SKU—OTP and gift-code contexts must use sensitive property names or omit the value.

Stable `EventType` values cover lifecycle, checkout, payments, SMS, outbox, and workers. Extend `OperationalEventNames` for new events. Debug is temporary/idle detail; Information is meaningful success; Warning is recoverable abnormal behavior; Error requires investigation; Fatal is startup/severe host failure. Business validation is not Error.

## Admin monitoring

`/admin/monitoring` and `GET /api/admin/monitoring` require `SecurityDiagnostics` (`security.diagnostics`). The page reads safe aggregate state from existing operational tables plus this process's in-memory worker heartbeat registry. It never returns provider credentials, Seq API keys, customer content, gift codes, or KYC data.

```text
Monitoring__SeqUiUrl=https://seq.internal.example
Monitoring__ShowSeqLink=true
Monitoring__ErrorWindowHours=24
Monitoring__OutboxWarningThreshold=20
Monitoring__PaymentPendingMinutes=30
Monitoring__WorkerHeartbeatMinutes=15
```

The UI URL must be HTTP(S) with no embedded credentials, query, or fragment. Heartbeats are instance-local; use Seq alerts for multi-instance fleet health.

## Storage and privacy

API files are `logs/vitorize-api-*.log`; Web files are `logs/vitorize-web-*.log`. They are outside `wwwroot`, roll daily, retain 14 files, roll at 100 MiB, and allow shared readers. Grant the service identity write access only to this directory.

Recommended Seq retention is 14–30 days in production and 3–7 days elsewhere. Size from measured daily ingestion plus 30% headroom, alert before 75/85/95% disk use, and back up Seq through a supported consistent-volume process. Operational logs are not a financial system of record.
