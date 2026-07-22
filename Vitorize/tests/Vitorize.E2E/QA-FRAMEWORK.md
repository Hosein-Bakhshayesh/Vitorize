# Vitorize Automated QA Framework

The permanent quality gate for Vitorize. One command builds the app, provisions an isolated Testing
stack, seeds deterministic data, runs a chosen suite, and produces HTML / JUnit / JSON reports with
screenshots, traces and videos on failure.

```powershell
# From tests/Vitorize.E2E
./scripts/Invoke-Qa.ps1 -Suite smoke        # ~5-min PR gate (critical path)
./scripts/Invoke-Qa.ps1 -Suite regression   # full browser suite
./scripts/Invoke-Qa.ps1 -Suite release      # full release gate (see below)
./scripts/Invoke-Qa.ps1 -Suite admin -Repeat 3
```

### The one-command release gate

`-Suite release` runs, in order, failing (non-zero exit) on the first problem:
1. **Release build** (Api + Web)
2. **All unit tests** (`Vitorize.Tests`)
3. **All integration tests** (`Vitorize.IntegrationTests`)
4. **Pristine DB reset** — `Reset-E2EDatabase.ps1` drops and recreates the E2E database from the
   checked-in schema DACPAC + versioned post-deploy scripts (deterministic known state)
5. **Start** the Testing stack, **seed** deterministic data
6. **Full Playwright suite** → HTML/JUnit/JSON reports
7. **Stop** the stack

Switches: `-Reset` (pristine reset before any suite), `-UpdateSnapshots` (re-approve visual baselines —
use **only** with `-Reset` so they capture a pristine DB), `-Project all` (3 device profiles),
`-Repeat N`, `-KeepStack`, `-Tag '@x'`.

Everything runs in the **Testing** environment against a dedicated E2E database
(`Vitorize_Phase3_Verification`) with fake SMS + fake payment providers and ephemeral keys. **It never
touches Production.**

---

## Architecture

```
tests/Vitorize.E2E/
├─ framework/                 # the reusable QA framework (not test files)
│  ├─ users.ts               # the 4 deterministic roles + credentials (env-overridable)
│  ├─ tags.ts                # tag vocabulary (@smoke, @admin, @customer, @security, …)
│  ├─ fixtures.ts            # base `test`/`expect` with page objects + auth + console guard
│  └─ pages/                 # Page Objects (BasePage, AdminLoginPage, AdminShellPage, …)
├─ tests/                     # spec files (Playwright testDir)
│  ├─ smoke.spec.ts          # @smoke critical path (framework-based)
│  ├─ auth-lifecycle.spec.ts # admin/customer/mixed-cookie login lifecycle
│  └─ … (storefront-commerce, admin-flows, seo, a11y, ui-quality, …)
│  └─ support/app.ts         # legacy helpers (reused by the framework; kept for back-compat)
├─ fixtures/seed-e2e.sql      # deterministic data incl. the 4 QA users
├─ scripts/
│  ├─ Invoke-Qa.ps1          # ⭐ single-command runner (build → seed → stack → run → report)
│  ├─ Start-E2EStack.ps1     # stack launcher used by playwright webServer (managed mode)
│  └─ Prepare-E2EDatabase.ps1# applies seed-e2e.sql
├─ playwright.config.ts       # 3 device projects, reporters, trace/video/screenshot on failure
└─ artifacts/                 # report/ (HTML), results/ (junit.xml, results.json), stack/ (logs)
```

The framework layers on top of the existing suite — it **reuses** `support/app.ts` primitives
(`monitorBrowser`, `latestOtp`, `expireOtp`, `registerCustomer`, `uniqueCustomer`) rather than
duplicating them, and existing specs keep working unchanged.

## Deterministic test users (Testing DB only)

Seeded by `fixtures/seed-e2e.sql`; credentials overridable via env for CI. Password for all: the
Testing bootstrap password (`E2E_ADMIN_PASSWORD`, default `E2E-Admin-Only-aA1!`).

| Role | Mobile | Notes |
|---|---|---|
| `SuperAdmin` | 09120000011 | full admin, `SecurityDiagnostics` permission |
| `Admin` | 09120000012 | plain Admin role (not SuperAdmin) — role-separation coverage |
| `Customer` | 09120000013 | standard customer |
| `CustomerVIP` | 09120000014 | verified (KYC approved) + wallet funded 5,000,000 for wallet-payment flows |

Access from a test via the framework: `USERS.SuperAdmin`, or `await loginAs('Customer')`.

## Writing a test

```ts
import { test, expect, TAG } from '../framework/fixtures';

test('admin can open the products list', { tag: [TAG.admin] }, async ({ loginAs, adminShell, page }) => {
  await loginAs('SuperAdmin');       // signs in through the correct scheme + asserts the landing area
  await adminShell.open('products');
  await expect(page).toHaveURL(/\/admin\/products/);
  await adminShell.expectAuthenticated();
});
```

Fixtures available on every test: `adminLogin`, `adminShell`, `storeLogin`, `storefront`,
`consoleGuard` (console/pageerror/requestfailed capture — call `consoleGuard.assertClean()`), and
`loginAs(role)`. Each test gets an isolated browser context (fresh cookie jar) so tests are
independent and parallel-safe.

**Add a Page Object** under `framework/pages/` extending `BasePage`; expose intent-revealing methods,
keep selectors inside the page object. **Add a fixture** in `framework/fixtures.ts` to make it
injectable.

## Suites & execution

`Invoke-Qa.ps1 -Suite <name>` (or the npm scripts). Smoke/security select by **tag**; the rest map to
**file sets** (existing specs aren't re-tagged, avoiding churn):

| Suite | Selection | npm |
|---|---|---|
| `smoke` | `--grep @smoke` | `npm run test:smoke` |
| `auth` | auth-lifecycle + authentication | `npm run test:auth` |
| `admin` | admin-flows + monitoring | `npm run test:admin` |
| `customer` | customer-account + authentication + storefront-commerce | `npm run test:customer` |
| `business` | storefront-commerce (purchase/delivery/coupon/wallet) | `npm run test:business` |
| `security` | auth boundaries (E2E); see also the .NET security suites | `npm run test:security` |
| `seo` | seo | `npm run test:seo` |
| `ui` | ui-quality + console-quality + accessibility | `npm run test:ui` |
| `a11y` | accessibility | `npm run test:a11y` |
| `performance` | performance | `npm run test:performance` |
| `visual` | visual-regression | `npm run test:visual` |
| `regression` / `all` | everything × 3 device projects | `npm run test:regression` |

Custom tag: `./scripts/Invoke-Qa.ps1 -Tag '@business'` or `npm run test:tag @business`.
Projects: `-Project desktop-light` (default) or `all` (desktop-light, desktop-dark, mobile-dark).
Stability gate: `-Repeat N`. Keep the stack up for debugging: `-KeepStack`.

## Reports

- **HTML**: `artifacts/report/index.html` (`npm run report` to open)
- **JUnit**: `artifacts/results/junit.xml` (CI)
- **JSON**: `artifacts/results/results.json` (metrics)
- On failure: screenshot + trace (`npx playwright show-trace <trace.zip>`) + video.

## CI/CD

```yaml
# example step
- run: npm ci && npx playwright install --with-deps chromium
  working-directory: Vitorize/tests/Vitorize.E2E
- run: pwsh ./scripts/Invoke-Qa.ps1 -Suite smoke        # gate on PRs
- run: pwsh ./scripts/Invoke-Qa.ps1 -Suite regression -Project all   # nightly
```
`Invoke-Qa.ps1` returns a non-zero exit code on any failure. The E2E database must exist with the
schema deployed (see `scripts/Prepare-E2EDatabase.ps1`); the runner starts the app itself.

## Deterministic data & visual baselines

`Invoke-Qa.ps1` re-seeds before every run, and `seed-e2e.sql` is **self-healing**: it removes volatile
entities left by prior UI-create tests (e.g. timestamped `e2e-category-*`) so a partial failure can't
poison the next run. Functional suites are fully deterministic.

`visual-regression.spec.ts` pixel-compares screenshots against committed baselines. **Data-driven
pages (the admin dashboard metrics) drift when the deterministic dataset changes** (e.g. new seed
users, or accumulated orders on a long-lived E2E DB). When such a change is intentional, re-approve on
a **pristine** database: reset/redeploy the E2E DB, then
`playwright test tests/visual-regression.spec.ts --update-snapshots`. Layout-only baselines
(storefront) are stable. Prefer masking volatile numbers over re-approving where possible.

## Coverage matrix (business workflow → spec → status)

| Workflow | Spec file | Status |
|---|---|---|
| Admin / SuperAdmin / Customer / mixed-cookie login · refresh · navigate · logout · authz boundaries | `auth-lifecycle.spec.ts` | ✅ full |
| Customer register · password login · OTP login · forgot/reset password | `authentication.spec.ts` | ✅ full |
| Admin: nav every surface · category create/filter/delete · product editor (rich text/variants/fields/Lucide) · settings (branding/typography/trust seals/SMS) · gift-code import/remove | `admin-flows.spec.ts` | ✅ core (CRUD depth in progress) |
| Monitoring route auth + safe diagnostics + Seq link | `monitoring.spec.ts` | ✅ full |
| Storefront browse/search/filter/sort · product detail (variant/gallery/features/related/dynamic fields) · cart merge/separate/qty/coupon · **gateway checkout** · **wallet checkout** · failed payment · **manual delivery** · **instant gift-code delivery** | `storefront-commerce.spec.ts` | ✅ full |
| Customer account surfaces (orders/wallet/profile) | `customer-account.spec.ts` · `smoke.spec.ts` | 🟡 partial |
| Cross-persona critical path (both personas, 4 roles incl. CustomerVIP) | `smoke.spec.ts` | ✅ full |
| **Support/Ticket delivery** end-to-end: buy SupportRequired product → open ticket from order → admin delivers via reply + closes → customer verifies + reply-blocked; + anon/not-found/IDOR/XSS negatives; DB invariants via `/api/testing/support-state` | `support-delivery.spec.ts` | ✅ full |
| SEO: title/meta/canonical/JSON-LD/OG/robots/sitemap/404/410/redirects | `seo.spec.ts` | ✅ full |
| UI quality: RTL · no-overflow · dialogs · responsive | `ui-quality.spec.ts` | 🟡 partial |
| Accessibility (axe) | `accessibility.spec.ts` | 🟡 partial |
| Console / Blazor / failed-resource errors | `console-quality.spec.ts` | 🟡 partial |
| Visual baselines (storefront + admin), **deterministic via pristine reset** | `visual-regression.spec.ts` | ✅ full |
| Input-security (SQLi/XSS/traversal/oversized) · financial concurrency · ownership/IDOR · upload security | `Vitorize.IntegrationTests` (.NET) | ✅ full (API-level) |

**Delivery types (gated):** Instant ✅, Manual ✅ (`storefront-commerce.spec.ts`) and **Support/Ticket ✅**
(`support-delivery.spec.ts`) all pass end-to-end.

### Remaining coverage (extension backlog, use the framework's building blocks)
- **Admin master-data depth** (Phase 1): child categories, parent change, image/logo upload, brand + tag full CRUD, duplicate-slug validation, storefront visibility of inactive/deleted.
- **Product-type matrix** (Phase 2): each product/delivery type + variant SKU/stock/default/duplicate-SKU.
- **Cart/checkout matrix** (Phase 5): out-of-stock, price-change-before-checkout, per-user coupon limit, wallet-partial, duplicate-callback via UI.
- **Customer account depth** (Phase 6): addresses, notifications read, wishlist, reviews+vote, KYC submit/approve/reject.
- **Admin operations depth** (Phase 7): refunds, wallet adjustments, roles/permissions UI, reviews/SMS/logs/reports.
- **Negative/security UI** (Phase 8): cross-customer IDOR via UI, price/discount/wallet manipulation, XSS in review/ticket.

## Regression policy

Every fixed bug gets a permanent regression test. Recent examples already in the suite:
`auth-lifecycle.spec.ts` (dual-cookie admin/customer authentication + token-scheme selection),
storefront-commerce cart-merge and payment-idempotency flows. When you fix a bug: add a test that
fails before the fix and passes after, tag it `@regression`, and keep it here.
