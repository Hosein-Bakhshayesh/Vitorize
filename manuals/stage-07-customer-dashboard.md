# Stage 7 — customer dashboard and account frame

## Scope

The shared `CustomerLayout`/`CustomerSidebar` and `/customer/dashboard` follow the
white/gray/mint storefront style. No separate account mockup was provided, so this
extends the established design language rather than claiming an exact mockup match.
All styles are scoped to `.customer-shell` or `.customer-dashboard` inside
`.site-shell`. Admin and homepage are untouched. Other customer pages receive the
shared account frame only; their individual bodies are deferred to later stages.

## Changes

- Rectangular account identity/navigation panel with mint active state and existing
  account links/logout form. Desktop sidebar is sticky and height-bounded.
- At 1100px and below, account navigation becomes a disclosure above the content.
  Native button keyboard support, Escape with restored toggle focus, and automatic
  collapse after navigation. Location-change subscription is removed on disposal.
- Dashboard heading, shopping shortcut and four linked status cards: wallet,
  orders, open tickets and verification. Existing server amounts/status labels and
  count rules are retained.
- Six most recent orders retain descending server timestamps, actual amounts,
  payment/order statuses, Persian dates and detail URLs. Semantic desktop table,
  labeled detail links and mobile order cards; clear no-orders state.
- Explicit partial/full/unavailable response state and read-only retry using the
  same four concurrent API requests. Failed/null responses no longer masquerade
  as successful zero balance/count or an empty order history.

The dashboard adds presentation-level loading/error/retry state. The sidebar adds
UI-only keyboard/navigation state. Endpoints (`auth/me`, `wallet`, `orders`,
`tickets`), data models, currency formatter, count/sort rules and logout POST remain
unchanged. CustomerOnly authorization and all authentication/payment/backend code
are unchanged. No fake data is added to the application.

## Verification

```powershell
dotnet build Vitorize/Vitorize.Web/Vitorize.Web.csproj --no-restore -v quiet
node manuals/tools/check-home-reference.mjs --shell --catalog --product --cart --checkout --auth --customer
```

The customer suite checks 1728, 1280, 1100, 1024, 768, 767, 402, 360 and 320px,
horizontal overflow, sidebar/table/stat breakpoints, actual fixture balance/counts,
six-order limit and sort order, detail links, keyboard disclosure/Escape/focus,
navigation collapse, short-desktop logout reachability, long name/balance,
empty/partial/full/null failures, loading/retry, dark theme and admin exclusion.
Guest dashboard access redirects to login; a synthetic local login returns to the
dashboard, and the existing logout clears the temporary customer session.

Result: build passed with zero warnings/errors. The combined home, shared-shell,
catalog, product, cart, checkout, auth and customer browser suite passed with zero
page JavaScript errors (1059 fixture requests). Desktop/mobile screenshots were
visually reviewed. `git diff --check` passed.

Only an in-memory API fixture and disposable local browser context are used.
No real credentials, SMS, customer account, wallet mutation, order, database or
payment is involved. Tests validate presentation and request contracts, not live
backend authorization or financial logic.

Screenshots/report are generated under git-ignored
`manuals/artifacts/home-reference/`: `customer-dashboard-1728.png`,
`customer-dashboard-402.png`, their viewport versions, `customer-menu-402.png`,
`customer-empty.png`, `customer-partial-error.png`, `customer-dark.png`, `report.json`.

Suggested commit: `feat(storefront): redesign customer dashboard and account layout`

Individual order, wallet, profile, ticket and other account page redesigns are not
included in this stage.
