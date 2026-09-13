# Stage 5 — checkout and payment result

## Scope

Customer `/checkout` and `/payment-result` presentation extends the established
white/gray/mint style. No separate checkout mockup was supplied. Styles are scoped
to `.site-shell .checkout-page` and `.site-shell .payment-page`; admin is untouched.

## Changes

- Breadcrumb and semantic progress steps; responsive order-information cards,
  accessible dynamic fields, shared product images and coupon form.
- Native keyboard-operable gateway/wallet buttons with selected/disabled states.
- Sticky, height-bounded desktop summary and stacked mobile summary, displaying
  existing server amounts unchanged; busy, empty and pending-order messages.
- Consistent success/failure cards and existing retry/order/KYC actions.

Both Razor `@code` blocks are unchanged. Existing API calls, field validation,
coupon pricing, order idempotency, wallet/gateway flow and KYC logic are preserved.
No backend, database or authentication changes are included.

## Verification

```powershell
dotnet build Vitorize/Vitorize.Web/Vitorize.Web.csproj --no-restore -v quiet
node manuals/tools/check-home-reference.mjs --shell --catalog --product --cart --checkout
```

Build: zero warnings/errors. Combined browser suite: passed, zero page errors.
Checkout covers 1728/1280/1100/1024/768/402/360/320px, field column geometry,
horizontal overflow and short-desktop payment-button reachability. Also tested:
required/confirmation validation, no mutation for invalid input, input/order API
failure, server VAT/payable, coupon add/remove/rejection, insufficient wallet,
keyboard wallet selection, payment failure and retry without duplicate order,
result states, retry eligibility/failure, empty checkout and dark theme.

All requests use an isolated in-memory API fixture. External navigation is to a
loopback test URL. No real order, payment, SMS, customer account or production API
is used. These are UI/request-contract tests, not payment-provider verification.
The existing query-driven result display is unchanged; live authentication, KYC
and authoritative provider settlement were not exercised.

Screenshots/report: git-ignored `manuals/artifacts/home-reference/`, including
`checkout-1728.png`, `checkout-402.png`, payment success/failure at both sizes and
`report.json`.

Suggested commit: `feat(storefront): redesign checkout and payment result UI`
