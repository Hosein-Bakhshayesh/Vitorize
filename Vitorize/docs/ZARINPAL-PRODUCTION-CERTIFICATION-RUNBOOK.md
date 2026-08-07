# Zarinpal production certification runbook

Gateway configuration is stored only in the database `Payment` settings. The
callback URL is the authoritative public API endpoint; no separate public-origin
setting is used.

| Key | Required production value |
| --- | --- |
| `ZarinpalMerchantId` | Provider-issued UUID; never paste it into tickets or logs. |
| `ZarinpalSandbox` | `false` |
| `ZarinpalBaseUrl` | `https://payment.zarinpal.com/pg/v4/payment` |
| `ZarinpalStartPayUrl` | `https://payment.zarinpal.com/pg/StartPay` |
| `ZarinpalCallbackUrl` | `https://api.hbakhshayesh.ir/api/payments/zarinpal/callback` |

Startup validates the merchant, mode, gateway URLs, and HTTPS callback path but
does not contact Zarinpal. A blank or all-zero merchant ID is a safe deployment
sentinel: the application starts, while all live gateway calls are blocked.

1. Obtain the production merchant UUID and provider enablement.
2. Set the five `Payment` settings in the administrative settings workflow.
3. Confirm readiness, then complete a low-value live order and callback.
4. Repeat delivery or provider verification and confirm idempotent financial
   records.
5. Capture provider, DNS/TLS, transaction, and approval evidence in the release
   record.
