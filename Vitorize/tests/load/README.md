# Vitorize Load & Performance Tests (Phase 4, Parts 2–4)

Realistic MVP load profiles for a **resource-limited Plesk host**. The goal is not to generate
unrealistic traffic — it is to find the **safe operating envelope**. Start at `baseline` and only
move up once a stage is stable.

> **Never run these against Production.** `Run-LoadProfiles.ps1` refuses any non-local `BaseUrl`.
> Auth/cart/checkout profiles require the **Testing** environment (fake SMS + fake payment
> providers) and the deterministic seed. Real SMS.ir / Zarinpal are never involved.

## Files

| File | Purpose |
|---|---|
| `vitorize-load.js` | k6 script — all 7 profiles in one file, selected via `PROFILE` |
| `Run-LoadProfiles.ps1` | Orchestrator: runs profiles, exports k6 JSON summaries, prints the Part 4 metric table |
| `public-and-seo.js` | Pre-existing public/SEO k6 smoke (kept) |
| `Test-PublicLoad.ps1` | Pre-existing pure-PowerShell public smoke — runs with **no k6 install** |

## Prerequisites

k6 is required for `vitorize-load.js` (it is **not** bundled). Install once:

```powershell
winget install k6.k6      # or: choco install k6
k6 version
```

Without k6, `Test-PublicLoad.ps1` still exercises the public routes in pure PowerShell.

## Profiles (Part 2)

| PROFILE | Load | Duration | Endpoints |
|---|---|---|---|
| `smoke` | 2 VUs | 30s | public (CI / post-install check) |
| `baseline` | 3 VUs | 5m | public read |
| `normal` | 10 VUs | 10m | mixed read (80% public / 20% authed) |
| `busy` | 25 VUs | 10m | mixed read |
| `peak` | 5→50→5 VUs | 1m ramp / 3m hold / 1m ramp | mixed read |
| `checkout` | 15 VUs | 3m | add-to-cart + checkout contention |
| `auth` | 12 VUs | 3m | login / invalid login / OTP request+verify |
| `admin` | 8 VUs | 5m | dashboard / orders / products / payments / monitoring |
| `soak` | 8 VUs | 25m | mixed read (Part 10 stability) |

## Acceptance targets (Part 4, enforced as k6 thresholds)

| Class | p95 | Error rate |
|---|---|---|
| Public read | < 1000 ms | < 1% |
| Authenticated read | < 1500 ms | < 1% |
| Cart write | < 2000 ms | < 1% |
| Checkout / payment | < 3000 ms | < 1% |

A breached threshold **fails** the run. Do not widen thresholds to pass — investigate the
bottleneck (see Part 11 DB analysis) and re-run the exact same profile after each fix.

## Running

```powershell
# In-session-safe smoke (short):
./Run-LoadProfiles.ps1 -Profiles smoke -BaseUrl http://localhost:5177

# Baseline then normal (the recommended first real measurement):
./Run-LoadProfiles.ps1 -Profiles baseline,normal -BaseUrl http://localhost:5177 `
    -LoginMobile 09120000000 -LoginPassword 'Secret-Test-Password-123!'

# Checkout contention (needs a real seeded product id):
./Run-LoadProfiles.ps1 -Profiles checkout -BaseUrl http://localhost:5177 `
    -LoginMobile 09120000000 -LoginPassword 'Secret-Test-Password-123!' -ProductId <guid>

# Admin read pressure (needs an admin account):
./Run-LoadProfiles.ps1 -Profiles admin -BaseUrl http://localhost:5177 `
    -AdminMobile 09120000001 -AdminPassword 'Secret-Test-Password-123!'
```

Summaries land in `TestResults/load/summary-<profile>.json` plus an `aggregate.json`.

## Manual / long-running (documented, not auto-run)

`normal`, `busy`, `peak`, and especially `soak` (25m) are **long real-time runs** and are executed
manually against a running Testing stack — they are not part of the fast automated suite. Bring the
stack up with the existing E2E stack scripts (`tests/Vitorize.E2E/scripts/Start-E2EStack.ps1`)
pointed at the Testing environment, seed the deterministic data, then run the profiles above.
