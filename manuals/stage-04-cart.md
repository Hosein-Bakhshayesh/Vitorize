# Stage 4 — customer cart

## Scope

The `/cart` page follows the white/gray/mint presentation of stages 1–3. No
separate cart mockup was supplied; this extends the established design language,
not a claim of pixel matching an unprovided cart design.

The stylesheet is scoped to `.site-shell .cart-page`. Homepage, shared chrome,
catalog, product detail, checkout, customer account and admin styles are unchanged.

## Changes

- Clear heading/item count, rectangular item cards, square contained images and
  the shared image fallback for missing/broken thumbnails.
- Product title, customer-facing variant, unit price and line total remain visible.
  Existing implicit default SKU suppression is retained.
- Accessible item names, quantity/remove controls, live quantities and totals.
- Responsive quantity/total placement, including a separate total row at narrow
  320/360px widths so long prices and controls do not collide.
- Desktop sticky order summary; stacked summary on tablet/mobile, with matching
  coupon controls, server totals, VAT and the existing checkout/shop actions.
- Coupon can be submitted with Enter. Existing API validation and totals remain
  unchanged. Coupon feedback is announced as a status message.
- Related controls are disabled during pending cart/coupon operations. Existing
  loading, retry, merge-failure and empty states retain their behavior.

The Cart.razor `@code` logic is unchanged. Quantity PUT, item/whole-cart DELETE,
cart GET, coupon validation and checkout routing use the same endpoints and
payloads. No browser-side monetary/VAT calculation or fake store data was added.
No backend, database, authentication or checkout flow changes are included.

## Verification

```powershell
dotnet build Vitorize/Vitorize.Web/Vitorize.Web.csproj --no-restore -v quiet
node manuals/tools/check-home-reference.mjs --shell --catalog --product --cart
```

The cart browser suite covers 1728, 1280, 1100, 1024, 768, 402, 360 and 320px;
horizontal overflow; quantity/total overlap; square images and broken-media
fallback; long titles/variants; quantity changes; backend-rejected mutation;
delete/decrement-to-remove/clear; initial read failure; preservation of last known
items after refresh failure; retry; empty and merge-error states; valid/invalid
coupon, Enter submission and coupon removal; authoritative server VAT/payable;
VAT-disabled response; dark theme; and guest login routing before checkout.

All reads and mutations use a separate in-memory API fixture. Cart and coupon
test data live only in `manuals/tools/cart-fixture.mjs`. No production API,
database, customer cart, real coupon or payment is used. The fixture intentionally
returns non-round totals to verify that the UI displays server amounts unchanged.
This checks UI/API contracts, not the backend's pricing/inventory algorithms.
Authenticated checkout and real payment are not exercised.

Result: web build passed with zero warnings/errors. The combined home, shell,
catalog, product and cart browser suite passed with zero page JavaScript errors.
The Cart.razor code block was also compared with HEAD and is unchanged.

Screenshots and report are generated in the git-ignored
`manuals/artifacts/home-reference/` directory: `cart-1728.png`, `cart-402.png`,
their `-viewport.png` versions, `cart-coupon.png`, `cart-empty.png`, `cart-dark.png`
and `report.json`.

Suggested commit:

`feat(storefront): redesign cart and order summary UI`

Stage 5 (checkout and payment result pages) is not included.
