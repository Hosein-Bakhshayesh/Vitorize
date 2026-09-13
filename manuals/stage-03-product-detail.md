# Stage 3 — product details

## Scope

The customer `/product/{slug}` page and its ProductReviews component now follow
the white/gray/mint language established in stages 1 and 2. No separate product
detail mockup was supplied, so this extends the approved style rather than
claiming a pixel match to an unprovided design.

The new rules are scoped to `.site-shell .product-page`. Related products reuse
the stage-2 catalog cards within the product page. Homepage, listing pages,
shared navigation, admin, cart and checkout layouts remain unchanged.

## Changes

- Square, uncropped product media with a horizontal thumbnail rail and plain
  backgrounds. Thumbnails are keyboard-operable buttons with selected states.
- Rectangular purchase card and SKU options, matching prices and mint actions.
  Long SKU lists remain bounded and scrollable; sold-out options stay disabled.
- On mobile: gallery, purchase card, features, then content. Both existing sticky
  purchase actions remain reachable above the 68px bottom navigation and safe area.
- Responsive features, description/reviews/FAQ navigation, review layout, sharing
  controls and related-product grid. Review rating controls are keyboard buttons;
  title/comment inputs have accessible names. Review eligibility logic is unchanged.
- Discount badge reflects the selected SKU's existing computed discount, including
  products where only a variant is discounted. No price calculation was changed.
- Public copy-link action no longer depends on admin.js, which is not loaded on
  public product pages. Clipboard failure is reported without breaking the page.
- Existing SEO/structured data, gallery deduplication, FAQ data, availability,
  min/max quantity, SKU query links, cart payloads and buy-now flow are preserved.

No backend, database, API contract, authentication or order logic was changed.
Existing product marketing text is retained; no new commercial guarantees or
demo content were introduced into application code.

## Verification

```powershell
dotnet build Vitorize/Vitorize.Web/Vitorize.Web.csproj --no-restore -v quiet
node manuals/tools/check-home-reference.mjs --shell --catalog --product
```

Product checks cover 1728, 1280, 1024, 1000, 768, 402, 360 and 320px widths;
overflow and square media; sticky action/nav separation; actual fixture images
and alt text; keyboard gallery; selected/default/unavailable SKU prices; min/max
quantity buttons; isolated cart request payloads and error recovery; FAQ and
public reviews; clipboard; many, implicit and absent variants; variant query links;
force-out-of-stock; currency; dark theme; missing/redirected products; buy-now routing.

All browser data and cart writes go to an in-memory API fixture, never a real
database or customer cart. Authenticated wishlist/review submission and a real
checkout/payment are not exercised. The fixture verifies UI/request contracts,
not the backend's inventory or pricing algorithm. Earlier home, shared-shell and
catalog regression suites can run in the same command.

Result: the combined home/shell/catalog/product browser suite passed with zero
page JavaScript errors. The web build passed with zero warnings and zero errors.

Generated screenshots/report are git-ignored under
`manuals/artifacts/home-reference/`: `product-1728.png`, `product-402.png`,
their `-viewport.png` versions, `product-reviews-mobile.png` and `product-dark.png`.

Suggested commit:

`feat(storefront): redesign product detail and responsive purchase UI`

Stage 4 (cart redesign) is not part of this change.
