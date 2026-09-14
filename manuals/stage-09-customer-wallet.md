# Stage 9 — customer wallet and transactions

Scope: `/customer/wallet` only. Extends the existing white/gray/mint customer
design; there is no separate wallet reference mockup to claim pixel equality with.
Homepage, admin, backend, persistence and payment settlement remain unchanged.

## Changes

- Responsive balance/top-up cards, two-column amount presets, labeled numeric
  input and accessible form submission, transaction table above 900px and cards
  below it. Styles are scoped to `.site-shell .customer-wallet-page`.
- Same balance, transaction amounts, reference fallback, dates, credit/debit
  mapping and newest-first sorting. No invented transactions or calculated balance.
- Read failures/null data have separate balance/transaction error states and a
  read-only retry, rather than misleading zero/empty success states.
- Inline success/error feedback remains visible near the top-up controls instead
  of a transient toast. Presets/input/submit are disabled while submitting.

## Preserved contracts

`GET wallet`, `GET wallet/transactions`, `POST wallet/topup` with `{ Amount }`,
the supplied gateway URL (full navigation), and the existing mock verification
endpoint/paid predicate are unchanged. Success reloads authoritative server data.
Preset amounts (200000, 500000, 1000000, 2000000), default (500000), positive-amount
client validation, callback query handling and existing message wording remain.
The form uses `novalidate` so the original input min/step attributes do not add
new purchase rules; backend validation is still authoritative.

## Verification

```powershell
dotnet build Vitorize/Vitorize.Web/Vitorize.Web.csproj --no-restore -v quiet
node manuals/tools/check-home-reference.mjs --shell --catalog --product --cart --checkout --auth --customer --order-pages --customer-wallet
```

Wallet checks exercise guest protection, nine viewport widths (320–1728px),
server values and ordering, empty/long/failed/null data, independent read errors,
retry/loading, presets, nonpositive validation, Enter submission, busy controls,
start rejection, verify rejection/unpaid, mock success and server-balance reload,
loopback gateway redirect without mock verify, callback messages and dark theme.

All API reads/writes use `wallet-page-fixture.mjs`, an in-memory server. Synthetic
login tokens cannot authenticate to the real API. No actual balance, charge,
gateway, SMS or database is touched; this is UI/request-contract QA, not live
payment settlement verification.

Build: passed with zero warnings/errors. Focused home + wallet suite: passed,
zero page JavaScript errors, 283 fixture requests. Desktop/mobile screenshots
were visually inspected. The complete home, shell, catalog, product, cart,
checkout, auth, customer dashboard, orders and wallet suite passed with zero
page JavaScript errors (1408 fixture requests). Dark capture waits for the
existing theme color transition to finish before asserting balance contrast.

Generated, git-ignored artifacts: `manuals/artifacts/home-reference/wallet-1728.png`,
`wallet-402.png`, `wallet-success-402.png`, `wallet-dark-402.png`, `report.json`.

Suggested commit: `feat(storefront): redesign customer wallet and transaction UI`

## Stop point and remaining scope

Stage 9 is the current stop point; do not start another stage without the user.
Completed stage groups: shared shell, catalog, product, cart, checkout/result,
authentication, customer dashboard/frame, orders/details, wallet/transactions.
The homepage redesign predates these stage groups.

Remaining page bodies (admin excluded): profile/security, verification, tickets
(list/create/details), delivered gift codes, wishlist, notifications, reviews,
blog/list/article, contact/FAQ/about/terms/privacy/CMS pages, and public error,
access-denied/not-found states. These already inherit the shared shell but still
need their individual redesign and tests. Final cross-page/mobile/accessibility
QA follows those changes. This inventory is not authorization to implement them.
