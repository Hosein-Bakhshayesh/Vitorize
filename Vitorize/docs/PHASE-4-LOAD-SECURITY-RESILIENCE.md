# Phase 4 — Load, Security & Resilience

Status date: 2026-07-18 · Branch: `AddTests`

This report documents the Phase 4 work completed so far. It is **honest about scope**: the fast,
deterministic gaps (concurrency, input-security) and the load-test infrastructure are built and
verified; the long real-time runs (load stages, soak, live chaos) and the resilience/ZAP/large-data
work are **scaffolded or documented for manual execution**, not yet executed. Nothing here fakes a
result. Phase 5 (final production-readiness audit) remains separate.

> **Do not declare "Phase 4 complete" yet.** See [Readiness](#readiness-summary) and
> [Remaining work](#remaining-work).

---

## 1. Existing infrastructure reused

- **Unit + real-SQL concurrency/security** — `Vitorize.Tests` (309 tests; `SqlServerFinancialConcurrencyTests`, `FinancialSecurityHardeningTests`, `OtpSecurityTests`, monitoring/logging).
- **SQL integration harness** — `tests/Vitorize.IntegrationTests` (`IntegrationTestFixture`: DACPAC-provisioned isolated SQL Server 2022 DB, `WebApplicationFactory`, **fake Zarinpal gateway + fake SMS sender**, `Testing` environment, `SqlServerIntegrationCollection`).
- **Playwright E2E** — `tests/Vitorize.E2E` (deterministic seed, stack scripts).
- **Load tooling** — `tests/load/` already had a k6 script (`public-and-seo.js`) and a pure-PowerShell runner (`Test-PublicLoad.ps1`). **k6 is the established load tool**, so it was extended (NBomber deliberately **not** added — no non-overlapping justification).

## 2. Tools selected and why

- **k6** for load — already present in the repo; single-tool policy honoured.
- **xUnit + real SQL Server** for concurrency and input-security — the production application locks
  (`sp_getapplock`), Serializable isolation, and EF query translation are the code under test, so
  in-memory fakes would not prove safety. Reuses the existing fixture.
- **Pure PowerShell** fallback runner remains for environments without k6.

## 3. Files created

| File | Purpose |
|---|---|
| `tests/Vitorize.IntegrationTests/Phase4ConcurrencyIntegrationTests.cs` | Wallet debit/overdraw, mixed credit+debit, per-user coupon, concurrent cart creation, concurrent add-to-cart merge |
| `tests/Vitorize.IntegrationTests/Phase4InputSecurityIntegrationTests.cs` | SQLi/XSS/traversal/oversized/malformed-JSON/invalid-GUID fuzzing with no-500 / no-leak invariants |
| `Vitorize.Infrastructure/Services/Testing/TestingFaultInjectionOptions.cs` | Testing-only fault-injection options (Off by default) |
| `Vitorize.Tests/TestingFaultInjectionTests.cs` | Fault-injection unit tests incl. the Production/Development guard |
| `tests/load/vitorize-load.js` | k6 — all 7 MVP profiles in one script |
| `tests/load/Run-LoadProfiles.ps1` | Orchestrator: runs profiles, exports summaries, prints Part-4 metric table, refuses non-local hosts |
| `tests/load/README.md` | Profiles, targets, exact commands, manual long-run guidance |
| `docs/PHASE-4-LOAD-SECURITY-RESILIENCE.md` | This report |

## 4. Files modified (production fixes)

| File | Change |
|---|---|
| `Vitorize.Infrastructure/Services/CartService.cs` | Serialize `AddItemAsync` read-modify-write per user with the existing `SqlServerTransactionLock` (Serializable) — fixes duplicate cart lines under concurrency |
| `Vitorize.Infrastructure/Services/ProductService.cs` | Cap the product search term to 100 chars (narrowest searched column) — fixes truncation `SqlException` → 500 |
| `Vitorize.Infrastructure/Services/Sms/TestingSmsSender.cs` | Testing-only SMS fault injection (guarded by `IsEnvironment("Testing")`) |
| `Vitorize.Infrastructure/Services/ZarinpalGatewayService.cs` | Testing-only payment fault injection (guarded by `IsEnvironment("Testing")`) |
| `Vitorize.Infrastructure/DependencyInjection.cs` | Bind `TestingFaultInjectionOptions` from config |
| `tests/Vitorize.IntegrationTests/PaymentDeliveryIntegrationTests.cs` | New: failed gateway verification leaves order unpaid, no wallet side effects |

## 5. Load scenarios implemented (Part 2–4)

Seven profiles in `vitorize-load.js`, selected via `PROFILE`: `baseline` (3 VUs/5m), `normal`
(10/10m), `busy` (25/10m), `peak` (5→50→5), `checkout` (contention), `auth` (login/OTP/invalid),
`admin` (dashboard/orders/products/payments/monitoring), plus `smoke` and `soak`. Endpoints cover
public catalog/search/settings/health, authenticated cart/account/wallet/orders, checkout+payment,
and admin reads. Part-4 targets encoded as **k6 thresholds** (public p95<1s, authed p95<1.5s, cart
p95<2s, checkout p95<3s, error rate <1%).

**Measured numbers: not yet collected.** k6 is not installed in the build environment and the stages
are long real-time runs — see [Remaining work](#remaining-work) for the exact commands.

## 6. Security scenarios implemented (Part 6)

Input security (all green): SQL-injection payloads, stored/reflected XSS markers, `javascript:` URLs,
path/file traversal, double extension, RTL override, unicode/emoji, oversized input, malformed JSON,
invalid GUID route values — asserted against **public search, product-slug, product-by-id, and login**
with invariants: status `< 500`, no SQL/stack/connection-string/"truncated" leakage, JSON responses
never served as `text/html`. Authn/authz boundaries, IDOR/ownership, security headers, and secret
masking were **already covered** by `ApiSecurityIntegrationTests`, `OwnershipIsolationIntegrationTests`,
and `FinancialSecurityHardeningTests` and were not duplicated.

## 7. Concurrency scenarios implemented (Part 5)

New (all green): concurrent wallet debits never overdraw / no negative balance; mixed concurrent
credit+debit stays consistent (no lost update); per-user coupon limit has exactly one winner;
concurrent cart creation yields exactly one cart; **concurrent identical add-to-cart merges to a
single line** (regression for the fixed defect). Already covered previously: gift-code reservation
race, coupon global-limit single winner, wallet credit idempotency, duplicate gateway callback.

## 8. Resilience scenarios — **partially implemented** (Part 8)

**Testing-only fault injection added** (`TestingFaultInjectionOptions`, section `Testing:FaultInjection`),
consumed by `TestingSmsSender` (SMS) and `ZarinpalGatewayService` (payment). Every mode is **Off by
default** and hard-guarded by `IHostEnvironment.IsEnvironment("Testing")` — proven inert in
Production/Development by unit tests. This lets a live Testing stack simulate SMS `Network`/`Timeout`/
`Unavailable`/`Fail` and payment `CreateFail`/`VerifyFail`, plus artificial latency (`DelayMs`).

Automated resilience coverage (green): SMS transient failure → retried then succeeds, and
non-transient → not retried (existing `SmsServiceTests`); **failed gateway verification → order stays
unpaid, payment not Paid, zero wallet side effects** (new); duplicate gateway callback verifies/
completes exactly once and fulfillment-failure compensates to wallet (existing). Not yet covered: DB
connection-drop/timeout recovery, file-storage failures, background-worker restart mid-item.

### Fault-injection usage (for Part 9 chaos, manual)

Run the API in the **Testing** environment and supply config (env vars shown; all Off by default):

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Testing'
$env:Testing__UseFakeSms = 'true'
$env:Testing__FaultInjection__Sms = 'Timeout'      # Off | Network | Timeout | Unavailable | Fail
$env:Testing__FaultInjection__Payment = 'VerifyFail' # Off | CreateFail | VerifyFail
$env:Testing__FaultInjection__DelayMs = '250'
dotnet run --project Vitorize.Api --no-launch-profile
```

## 9. Chaos scenarios — **documented, manual** (Part 9)

Stop-API-mid-request, restart-Web-mid-session, stop-SQL-locally, kill/restart workers. Provider
latency/failure is now injectable via the Testing-only fault toggle above. Chaos runs remain manual
against a local Testing stack; no permanent random-failure code exists in the app.

## 10. Soak test — **not yet executed** (Part 10)

`PROFILE=soak` (8 VUs / 25m mixed) is defined; run manually per the README and monitor memory/handles/
threads/SQL connections/error+latency drift.

## 11. Dataset scale — **not yet generated** (Part 12)

Large-dataset generator (10k products / 50k orders / 100k gift codes) not yet built. Pagination/
filtering/sorting are currently exercised only at seed scale.

## 12–15. Test totals (this session, verified)

| Suite | Passed | Failed | Skipped |
|---|---|---|---|
| Unit (`Vitorize.Tests`) | 317 | 0 | 0 |
| Integration (`Vitorize.IntegrationTests`) | 103 | 0 | 0 |
| — of which new Phase 4 (5 concurrency + 31 input-security + 8 fault-injection + 1 resilience) | 45 | 0 | 0 |
| **Total (automated, this session)** | **420** | **0** | **0** |

Release build: **succeeded, 0 errors**. Playwright E2E (44) unchanged — not re-run this session.

## 16–21. Latency / throughput / error rate

**Not measured** — load runs pending (k6 not installed; long real-time). Targets are enforced as k6
thresholds; numbers to be filled after the runs in [Remaining work](#remaining-work).

## 22–25. CPU / memory / SQL Server / blocking-deadlock observations

- **CPU/memory**: not profiled under load yet (pending load runs).
- **SQL Server**: concurrency tests confirm the app-lock + Serializable strategy serializes wallet,
  coupon, cart-creation, and now cart add-item per user with exactly-one-winner semantics and no
  overdraw/lost-update. No deadlocks observed in the 36 concurrency/security executions.
- Blocking/deadlock analysis under sustained load (Part 11) pending the load runs.

## 26. Security vulnerabilities discovered

- **Unhandled 500 on over-long search term** (`GET /api/products?search=…`). A term longer than the
  narrowest searched column (`ProductTag.Title`, nvarchar(100)) raised `SqlException 8152 "String or
  binary data would be truncated"`. **Production-reachable** with a normal short URL (~250 chars).
  Severity: medium (DoS/error-leak surface). **Fixed** + regression-tested at 101/250/10 000 chars.

## 27. Production bugs discovered

1. **Cart duplicate-line race** (concurrency/correctness) — six concurrent identical add-to-cart
   calls produced six lines instead of one merged line (unguarded read-modify-write; non-unique
   `IX_CartItems_Identity`). Severity: medium (cart/checkout integrity). **Fixed** + regression-tested
   (deterministic across repeated runs).
2. **Over-long search 500** — see §26.

## 28. Bugs fixed

Both of the above, using patterns already established in the codebase (transaction-scoped
`sp_getapplock` for the cart; input capping for search). No schema change, no new EF migration, no
constraint disabled, no retry/timeout band-aid.

## 29. Regression tests added

- `Concurrent_identical_add_to_cart_merges_into_a_single_line`
- `Oversized_search_input_does_not_fault_the_server` (Theory: 101 / 250 / 10 000 chars)

## 30. Remaining risks

- Load/soak numbers unmeasured — the safe concurrency envelope is **estimated, not proven**.
- Resilience/chaos/dependency-failure behaviour unverified by automated tests.
- Large-dataset pagination/aggregation performance unverified.
- Remaining concurrency surfaces not yet covered: OTP creation/latest-valid/reuse, outbox-worker
  dedup, gift-code reservation expiry/release, concurrent cart quantity-update vs. removal.
- Remaining security surfaces not yet covered: file-upload matrix (MIME spoof, exe-as-image, SVG
  active content, filename traversal), mass-assignment/over-posting on write endpoints, ZAP baseline.

## 31. Recommended safe concurrency level (interim)

Until measured: begin at **10–15 concurrent users** on the Plesk MVP host, with checkout/payment
kept to **≤ 15 concurrent** per the `checkout` profile. Revise after running `baseline`→`normal`→
`busy` and confirming thresholds hold.

## 32–33. Hosting / IIS-Plesk recommendations (interim)

- Confirm Kestrel/IIS request limits keep the 8 KB request-line cap (defence-in-depth already added
  in the app for search length).
- Single app-pool worker to start; enable overlapped recycling; set an idle timeout appropriate for
  Blazor Server circuits; ensure the SQL connection-pool max is sized for the chosen concurrency.
  Finalise after load measurement.

## 34. Exact commands to run all tests locally

```powershell
# Unit
dotnet test Vitorize.Tests/Vitorize.Tests.csproj -c Release -p:NuGetAudit=false

# SQL integration (needs local SQL Server 2022 + sqlpackage + sqlcmd)
dotnet test tests/Vitorize.IntegrationTests/Vitorize.IntegrationTests.csproj -c Release -p:NuGetAudit=false

# Just the Phase 4 concurrency + input-security suites
dotnet test tests/Vitorize.IntegrationTests/Vitorize.IntegrationTests.csproj -p:NuGetAudit=false `
  --filter "FullyQualifiedName~Phase4ConcurrencyIntegrationTests|FullyQualifiedName~Phase4InputSecurityIntegrationTests"

# Playwright E2E
cd tests/Vitorize.E2E; npx playwright test

# Load (requires: winget install k6.k6). Bring up the Testing stack first.
cd tests/load
./Run-LoadProfiles.ps1 -Profiles smoke -BaseUrl http://localhost:5177
./Run-LoadProfiles.ps1 -Profiles baseline,normal -BaseUrl http://localhost:5177 -LoginMobile 09120000000 -LoginPassword 'Secret-Test-Password-123!'
```

## 35. Tests requiring manual execution

- All load profiles beyond `smoke` (long real-time; need k6 + running Testing stack).
- Soak (`PROFILE=soak`, 25m).
- Chaos scenarios (stop/restart API·Web·SQL·workers).
- ZAP baseline scan (Part 7).

## 36. Production monitoring recommendations

Watch (via the existing Seq/monitoring stack): `UnhandledException` events (the search 500 would have
surfaced here), wallet `WalletDebitFailed`/duplicate-operation events, `sp_getapplock` timeouts
(THROW 51000), SQL connection-pool exhaustion, and Blazor circuit failure rate. Alert on any
`ExceptionType=SqlException` for public GET routes.

---

## Readiness summary

| Area | Status |
|---|---|
| Load readiness | 🟡 Infrastructure built; **numbers not yet measured** |
| Security readiness | 🟢 Input-security + authz/IDOR/headers green; 🟡 upload matrix + ZAP pending |
| Concurrency safety | 🟢 Financial + cart races proven safe (2 defects fixed); 🟡 OTP/outbox/reservation-expiry pending |
| Dependency resilience | 🟡 Fault injection built + SMS/payment failure paths tested; 🔴 DB-drop/file-storage recovery pending |
| Background-worker safety | 🔴 Not yet tested (restart-mid-item pending) |
| Financial integrity | 🟢 No overselling / no double debit / no duplicate delivery / no duplicate finalization in tested paths |
| **Overall production readiness** | 🟡 **Partial — not yet Phase-4-complete** |

## Remaining work

To reach the "✅ PHASE 4 COMPLETE" bar: run and record the load/soak profiles; extend resilience to
DB connection-drop/timeout recovery, file-storage failures, and background-worker restart-mid-item
(the fault-injection harness is now in place); run the chaos scenarios; add the remaining concurrency
(OTP/outbox/reservation) and security (upload/mass-assignment/ZAP) suites; generate the large dataset
and verify pagination/aggregation. Each remaining defect must follow the same fix + regression-test +
rerun discipline used for the two fixes above.
