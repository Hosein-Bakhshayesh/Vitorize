# Zarinpal production certification runbook

This repository validates the shape and consistency of the payment configuration at startup. It does not contact Zarinpal during startup and it does not substitute for merchant onboarding or provider certification.

## Authoritative configuration

The database `Payment` settings are the only gateway source of truth:

| Key | Required production value |
|---|---|
| `ZarinpalMerchantId` | Provider-issued UUID; never paste it into tickets, logs, or source control. |
| `ZarinpalSandbox` | `false` |
| `ZarinpalBaseUrl` | `https://payment.zarinpal.com/pg/v4/payment` |
| `ZarinpalStartPayUrl` | `https://payment.zarinpal.com/pg/StartPay` |
| `ZarinpalCallbackUrl` | `https://<public-api-origin>/api/payments/zarinpal/callback` |

Set `Hosting__PublicOrigin=https://<public-api-origin>` through the deployment secret/configuration provider. It is not a database setting because it describes the externally routed host. Startup rejects missing, non-HTTPS, mixed sandbox/live, wrong-host, malformed merchant, or callback-origin-mismatch configurations in Production. Diagnostics expose only booleans and safe validation messages; no merchant ID is logged or returned.

## Certification execution (external)

1. Obtain a production merchant UUID and provider enablement from the Vitorize finance owner.
2. Configure the five Payment settings and `Hosting__PublicOrigin` using the approved secret/configuration channel.
3. Deploy behind the final TLS reverse proxy. Confirm `/api/health/details` as a Finance/Security Diagnostics user reports payment `Healthy: true`; do not attach the detailed response to public tickets.
4. Create a low-value real order, complete the provider payment, and confirm the callback uses the public HTTPS origin and one payment/order ledger entry is produced.
5. Repeat callback delivery or provider verification; confirm the financial result is idempotent.
6. Execute the finance team's manual gateway-refund workflow, then reconcile the payment. Preserve provider reference, application audit event, and sanitized logs as evidence.
7. Record provider contact, merchant onboarding confirmation, callback DNS/TLS evidence, transaction references, and finance/release approvals in the release record.

Stop and roll back traffic changes if startup validation fails, the callback is not HTTPS/public, a provider request reaches sandbox from Production, or a payment result cannot be reconciled. Missing production merchant credentials, final DNS/TLS, or provider certification keeps RB-005 blocked.
