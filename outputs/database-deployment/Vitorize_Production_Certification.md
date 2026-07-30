# Vitorize Version 1 runtime certification

Certification date: 2026-07-30 UTC  
Certified source: `74d35fdcd3b59e012983e56c927c80fcfd3af124`  
Certification database: `VitorizeDb_Certification_20260730_02`  
Platform: SQL Server 2022 (major version 16), compatibility level 160; .NET 8 Release publish.

## Database package

`Vitorize_Production_Full.sql` deployed a new database successfully from the deployment folder, including its SQLCMD schema, seed and verification includes. Standalone verification passed before and after an idempotent seed rerun. The final database had 58 user tables, 355 non-heap indexes, 86 foreign keys, four required roles, 12 script-history records, and no untrusted/disabled foreign keys or checks. The difference from earlier 359-index reporting is the precise final count of `sys.indexes` rows with `index_id > 0`; no schema drift was found against the deployed SQL package.

All five canonical Zarinpal settings existed exactly once and the legacy misspelled setting did not exist. The all-zero deployment MerchantId sentinel is configuration-valid but is blocked in the gateway service before any outbound request. No payment request was made during certification. Live activation still requires a real MerchantId, final callback/public-origin configuration, provider certification and an explicit operational approval.

## Runtime and restart

Clean Release API and Web outputs were published. The API started in `Production`; liveness and readiness both returned HTTP 200. The Web root and public entry routes returned HTTP 200. One enabled SuperAdmin was bootstrapped. The existing Playwright SuperAdmin lifecycle scenario passed against the published stack (login, refresh, Admin navigation and logout). After a clean API/Web restart, API liveness/readiness and Web root again returned HTTP 200; SuperAdmin/ledger/payment-setting counts remained 1/12/5.

## Quality gates

Release build: passed, 0 warnings, 0 errors.  
Unit tests: 423 passed.  
SQL-backed integration tests: 124 passed.  
Focused payment/configuration tests: 13 passed.  
Published-stack Playwright SuperAdmin lifecycle: 1 passed.  
NuGet direct/transitive vulnerability scan: no vulnerable packages reported.  
`npm audit` was not executable because the E2E package has no lockfile; no lockfile was generated during certification.

## Scope and recommendation

**Repository and runtime certified** for the executed database, API, Web, bootstrap, payment-sentinel and restart gates. The browser-controller host was unavailable locally; browser coverage was instead provided by the repository's existing Playwright scenario. The broader Playwright authentication suite is designed for the managed Testing database and exceeded the local execution timeout when aimed at the intentionally empty Production certification database; it is not represented as a pass.

**Ready for controlled UAT deployment.** It is not a declaration of live commercial-payment readiness. External owners must complete hosted CI execution, production secret provisioning, DNS/HTTPS and reverse-proxy approval, live Zarinpal certification, a backup/restore drill, production-host rehearsal, and customer acceptance. These do not block a controlled UAT installation with the payment sentinel retained; they do block live payment and final production sign-off.
