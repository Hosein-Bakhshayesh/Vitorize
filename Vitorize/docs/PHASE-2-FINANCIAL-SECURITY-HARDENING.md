# Phase 2 financial integrity and security hardening

## Deployment order

This solution remains Database-First. There are no EF Core migrations.

1. Back up and restore production into an isolated rehearsal SQL Server database.
2. Run `Database/Deploy-Database.ps1 -DryRun` and review preflight findings.
3. Run the canonical deployment runner. It applies required manifest entry `V0004`.
4. Run `Database/Tests/verify_phase2_financial_security.sql` on the rehearsal database.
5. Configure `Jwt__SecretKey` (at least 32 bytes) and `Encryption__Key` (exactly 32 bytes) from a secret provider. No usable keys are committed.
6. Give the service identity write access to `Vitorize.Api/private/verification-documents`; never map this directory as static content.
7. Deploy one application instance first. The background job converts legacy plaintext KYC and delivery rows to authenticated AES-GCM in bounded batches.
8. Verify health at `/api/health`; privileged diagnostic detail is `/api/health/details` and requires `security.diagnostics`.

## Financial controls

- Checkout reloads and reprices Product/Variant data inside a serializable transaction and validates product, variant, stock mode, quantity, delivery type, mobile, and KYC state.
- SQL transaction application locks serialize checkout, coupon consumption, wallet mutation, payment callbacks, refunds, OTP use, and gift-code reservation/expiration.
- Filtered unique indexes enforce gateway authority, callback key, wallet financial reference, active OTP, and manual-delivery uniqueness.
- Payment refunds are idempotent. Wallet refunds complete atomically; external gateway refunds remain Pending until an authorized operator records the provider reference.
- If a provider confirms capture but fulfillment cannot complete, callback and reconciliation paths roll back the partial order state and issue an idempotent wallet compensation with a financial audit entry.
- Gift-code and manual-delivery values are stored as AES-GCM ciphertext with a SHA-256 audit fingerprint. API responses decrypt only after ownership/authorization checks.
- `FinancialAuditLogs` is append-only; SQL rejects update/delete.
- Pending Zarinpal payments are reconciled automatically. Outbox processing uses recoverable five-minute leases.

## Security controls

- KYC uploads use opaque owner-bound tokens and private storage. The download route checks ownership or `kyc.review`; legacy public KYC URLs are blocked.
- KYC PII is encrypted as one authenticated payload and legacy plaintext columns are cleared after conversion.
- Fine-grained claims cover finance, fulfillment, KYC, security diagnostics, settings, and user management.
- OTP records have one-active-code uniqueness, transaction-safe consume/attempt logic, secure random generation, constant-time verification, expiry/attempt bounds, and rate limits.
- API and Web send CSP, framing, MIME-sniffing, referrer, and permissions headers. Auth cookies are always Secure/HttpOnly and HSTS is one year with subdomains/preload.
- Audit serialization excludes credentials, tokens, encrypted values, KYC/PII, file paths, payment raw payloads, and delivered content.

## Operational limitations

- Zarinpal does not expose an automatic refund implementation in the current gateway adapter. Provider refunds use the two-step `GatewayManual` workflow and require an operator to execute the provider-side refund before completion.
- CSP allows inline styles/scripts in the Blazor Web host for current component compatibility. Removing those allowances requires nonce/hash support across the UI bundle.
- Financial audit rows are intentionally immutable. Retention requires archival rather than deletion.

## Verification

- Run SQL Server integration tests by setting `VITORIZE_SQL_TEST_CONNECTION` to an isolated SQL Server database on which the canonical deployment chain has completed, then run `dotnet test Vitorize.sln -c Release`.
- Run `powershell -NoProfile -ExecutionPolicy Bypass -File Database/Tests/Test-DatabaseDeploymentAssets.ps1` to validate manifest checksums and deployment assets.
- Run `sqlcmd -S <server> -d <database> -E -b -i Database/Tests/verify_phase2_financial_security.sql` after deployment.
- The repository does not currently contain a browser E2E harness. Health authorization, private KYC-path blocking, and response headers must therefore also be included in deployment smoke tests.
