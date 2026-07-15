# Logging operations runbook

## First response

1. Capture UTC window, environment, service/version, public order/ticket reference, payment authority if applicable, and `X-Correlation-ID`.
2. Check `/admin/monitoring` for database readiness, payment/refund state, outbox/SMS dead letters, and worker heartbeat.
3. Search Seq by correlation first; use rolling files if Seq is unavailable.
4. Cross-check durable Audit, Security, Error, and Financial Audit records. Seq is not the business ledger.
5. Never paste OTPs, credentials, gift codes, KYC documents, request bodies, or tokens into incidents.

## Recommended Seq signals

| Signal | Query |
|---|---|
| All errors | `@Level = 'Error' or @Level = 'Fatal'` |
| Payment verification | `EventType = 'PaymentVerificationFailed'` |
| Duplicate callbacks | `EventType = 'PaymentCallbackDuplicate'` |
| Refund failures | `EventType = 'RefundFailed'` |
| Wallet compensation | `EventType = 'WalletCompensationCreated'` |
| Gift delivery failures | `EventType = 'GiftCodeDeliveryFailed'` |
| SMS dead letters | `EventType = 'SmsDeadLettered'` |
| Outbox recovery/dead letter | `EventType in ['OutboxMessageRecovered','OutboxMessageDeadLettered']` |
| Permission denied | `EventType = 'AdminPermissionDenied'` |
| Login failures | `EventType = 'LoginFailed'` |
| Schema mismatch | `EventType = 'DatabaseSchemaMismatch'` |
| Slow request | `EventType = 'HttpRequestCompleted' and ElapsedMs > 2000` |
| Slow checkout | `EventType = 'CheckoutCompleted' and DurationMs > 3000` |
| Slow provider/SQL | `DurationMs > 3000 and (Provider is not null or OperationType is not null)` |
| Correlation | `CorrelationId = '<validated-id>'` |
| Order | `OrderNumber = '<public-order-number>'` |
| Ticket | `TicketNumber = '<public-ticket-number>'` |
| Startup/fatal | `EventType in ['ApplicationStarted','ApplicationStopping'] or @Level = 'Fatal'` |

Build signals through the documented Seq UI/query language rather than modifying internal storage.

## Alerts

Alert on Fatal events, error/500 spikes, payment/refund/gift failures, wallet compensation, outbox/SMS dead letters, missing heartbeat, database/schema failure, repeated Admin authorization failures, encryption failure, and Seq disk pressure. Prefer email/webhook. Use SMS only for severe infrastructure failure through an independent provider. Deduplicate, add cooldowns, and never alert through the failing component.

## Investigations

- Correlation: search the exact property across Web and API, sort by time, then follow TraceId/public references.
- Order: search `OrderNumber`, follow checkout → payment → delivery, then confirm durable financial/audit rows.
- Authority: search `Authority` with provider/time, then inspect duplicate callback, verification, reconciliation, and refund.
- Seq outage: confirm health/file writes; inspect container, volume, TLS, and firewall. Do not restart business services just to recover a volatile queue.
- Worker: identify affected instance/fleet, inspect latest iteration failure/database readiness, then stuck leases/dead letters.

## Privacy and production checklist

Logs must not contain clear mobile/email, OTP, password, token/JWT, API/encryption key, gift/delivery content or hash, KYC/national data, upload content/path, cookie, raw callback, or dynamic sensitive values. A finding is a security incident: restrict logs, rotate secrets, fix the event, and apply deletion policy.

- Both hosts work with Seq disabled and with Seq unreachable.
- Seq URL/key come from secrets; UI is authenticated/TLS/internal/retained/backed up/disk-monitored.
- Services can write `logs/`; web servers cannot serve it.
- Web/API correlation is present end-to-end.
- `/admin/monitoring` is denied without `security.diagnostics`.
- Error/Security/Audit links work and the Seq link contains no credential.
- Secret and generated-log scans pass after deployment.
