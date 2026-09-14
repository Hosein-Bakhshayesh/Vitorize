# Stage 8 — customer order list and details

## Scope

`/customer/orders` and `/customer/orders/{id}` extend the white/gray/mint customer
style. No separate order mockups were provided. Page styles and dialog behavior
are limited to `.customer-orders-page`; the shared/admin modal components, backend
and authorization are unchanged.

## Changes

- Consistent headings, rectangular list/detail cards, responsive desktop table and
  mobile records, accessible detail links, empty/loading and list read-error/retry.
- Product/variant, quantity, submitted input values, delivery content, KYC notices,
  ticket link and purchase-time payment summary remain connected to existing data.
- Long delivery content wraps without horizontal overflow. Only server-visible
  deliveries are displayed and the existing clipboard action is preserved.
- Desktop height-bounded sticky summary; stacked summary at tablet/mobile widths.
- Styled cancel/hide confirmations and review form. Keyboard rating buttons,
  explicit field labels and disabled inputs/dismissal during pending review submit.
- Order-only dialog focus containment, Escape safe-cancel, inert background, scroll
  lock and restoration on dismissal/navigation. Order action toasts clear mobile
  navigation and wrap inside the viewport.

CustomerOrderPresenter, cancel/hide eligibility flags, payment retry eligibility,
currency/VAT snapshots, KYC links, review gates/payloads and delivery visibility
rules are unchanged. Review errors are presented inside the dialog, and its
dismissal is guarded in markup while sending. Orders adds presentation-level read
failure/loading state; cancel/hide POST handlers and re-fetch behavior are retained.
No order/payment/security endpoint or real customer data is changed.

## Verification

```powershell
dotnet build Vitorize/Vitorize.Web/Vitorize.Web.csproj --no-restore -v quiet
node manuals/tools/check-home-reference.mjs --shell --catalog --product --cart --checkout --auth --customer --order-pages
```

Order tests cover eight viewport widths (320–1728px), responsive table/summary,
server amounts/VAT, canonical unpaid/delivered/KYC states, masked values, hidden
delivery exclusion, copy, original KYC/ticket links, server-blocked actions,
confirmation dismissal without mutation, cancel/hide success and rejection,
review validation/rating/payload, retry failure/mock success/loopback navigation,
empty/error/missing states and dark theme.

All mutations occur only in `order-pages-fixture.mjs`, an in-memory server. Login
uses a synthetic JWT-shaped token with an expiry and an intentionally invalid
signature, solely for the web client's local expiry preflight; no real API accepts
it. Clipboard is stubbed inside the disposable test browser. No actual order,
payment, review publication, SMS, document upload, database or system clipboard
is modified. This is UI/request-contract verification, not a backend security audit.

Screenshots/report: git-ignored `manuals/artifacts/home-reference/`, including
`orders-list-1728.png`, `orders-list-402.png`, `order-detail-1728.png`,
`order-detail-402.png`, `order-kyc-402.png`, `order-confirm-402.png`,
`order-review-402.png`, `order-dark.png` and `report.json`.

Suggested commit: `feat(storefront): redesign customer order pages and dialogs`

Other individual customer account pages are deferred to subsequent stages.
