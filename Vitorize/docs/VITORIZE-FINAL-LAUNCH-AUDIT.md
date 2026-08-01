# Vitorize final launch audit

Audit date: 2026-08-01 (Asia/Tehran)  
Release branch: `Responsive`  
Baseline reviewed: `a4d03b7cd93cbde0df8ee41ec99d28339eba13c8`; this audit's remediations are included in the final commit following this report.

## Verdict

**READY WITH NON-BLOCKING CURRENT DEFECTS**

The repository-controlled blockers found during the audit were fixed: the documented clean-database command now resolves its DACPAC correctly; API signing/encryption keys are no longer committed defaults; a newly deployed production database can bootstrap an operator before Zarinpal has been configured; and maintenance cleanup batches are deterministic.

Before a real cut-over, the hosting owner must provide the documented secret-provider values, TLS/reverse-proxy and persistent-storage configuration, CKEditor licence decision, and real Zarinpal configuration. These are intentionally external deployment inputs, not repository defects. The placeholder merchant sentinel blocks gateway initiation until the Admin configures a real merchant, so it cannot produce a false paid order.

## Evidence

| Gate | Result |
| --- | --- |
| Branch consistency | `Responsive` was clean and equal to `origin/Responsive` at audit start; its ancestry includes responsive, multi-quantity, database, Zarinpal sentinel, runtime-certification and manual commits. |
| Fresh SQL install | Disposable `Vitorize_FinalLaunchAudit_20260801`: DACPAC + manifest deployment succeeded; second deployment skipped every applied script; no customers, orders, products, categories or gift codes were seeded; roles=4 and settings=157 with no duplicate settings. |
| Deployment assets | `Database/Tests/Test-DatabaseDeploymentAssets.ps1` passed, including manifest hashes, UTF-8, baseline, target guard, deferred DACPAC default and no committed API crypto keys. |
| Published runtime | Release build: 0 warnings, 0 errors. Published API (not `dotnet run`) on the fresh database returned liveness=200 and readiness=200. It created exactly one SuperAdmin on first startup; a second startup left Users=1 and SuperAdmins=1. Direct API login succeeded. |
| Active workers | Published API logged starts for `OutboxProcessorBackgroundService` and `BackgroundJobProcessor`; the first maintenance iteration and Zarinpal reconciliation completed with zero candidates. |
| Regression tests | Unit: 438 passed. Earlier accepted final-candidate evidence covers the full integration, Playwright commerce/visual/Axe/responsive and multi-quantity suites; this audit reran the relevant source/runtime/database gates after its changes. |
| Release package | `artifacts/final-launch/Vitorize-release-candidate-20260801.zip`: 296 entries, SHA-256 `50C3B030E5D9855BCB8A673FE5FC731ADE98E714160CFD1DF0961BD55EC30426`. It contains published API/Web folders, the database package and operator documentation; it contains no source-controlled credentials. |

## Current limitations and operating controls

- The local published-runtime rehearsal used the deployment payment placeholder and did not contact SMS or Zarinpal. Gateway payment attempts correctly fail safely until Admin supplies the real, validated Production settings.
- A real host must keep Data Protection, public uploads and private KYC storage outside a replaced publish folder, protect them with least-privilege ACLs, and follow the restart/proxy rehearsal in `PRODUCTION-HOSTING-RUNBOOK.md`.
- The full customer, delivery, wallet/refund, ownership, upload and responsive paths remain covered by the accepted regression suites. No regression evidence was found in the audited changes.

## Remediations made

1. Deferred `$PSScriptRoot` DACPAC resolution in `Publish-CleanDatabase.ps1` so the documented `-File` invocation works.
2. Removed source-controlled JWT/encryption defaults and enforced this with the deployment asset test.
3. Seeded/recognized an installation-time Zarinpal sentinel, seeded reference data before Production validation, and kept the gateway hard-blocked for that sentinel.
4. Added deterministic ordering to every bounded maintenance cleanup batch.
