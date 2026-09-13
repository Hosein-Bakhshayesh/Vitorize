# Stage 2 — customer catalog

## Scope

Shop (`/shop`), category directory (`/categories`), category and brand listings
(`/category/{slug}`, `/brand/{slug}`), search start and search results (`/search`).
These pages now share the white/gray/mint presentation established in stage 1.
There are no separate listing-page reference images: this stage extends the
approved homepage's visual language; it is not a pixel-match claim for unprovided designs.

The new stylesheet is scoped to `.site-shell .catalog-page`. The homepage, admin,
product detail, cart, checkout and customer account page designs are not changed.
StoreProductCard's purchase, wishlist, variant routing, availability and pricing
code is unchanged; its new visual rules only apply inside catalog pages.

## Changes

- Square category tiles with a selected state and existing category media.
- Responsive product grids: four columns on large desktops, three on medium
  desktops and two on smaller/tablet/mobile viewports.
- Right-hand desktop filter sidebar; an in-toolbar mobile trigger opens the
  filter sheet above the shared header and bottom navigation.
- Native keyboard-accessible sort select retaining all existing sort options.
- Simple rectangular product cards, mint purchase buttons and red discounts.
  Real product images, currency, stock, wishlist and redirect links are preserved.
- Category hierarchy and links retained in the updated category directory.
- Explicit product/category loading failures with retry, separate from empty data.
- Pager current-page labels, live result status and explicit ARIA filter states.
- Mobile dialog scroll lock, inert background, Tab/Shift+Tab containment, Escape
  dismissal and focus restoration; JavaScript loaded only when opening filters.
- Only the latest listing response updates the view during rapid filter changes.
  Price query values use invariant formatting; price-chip removal is awaited.

## Data contract

No API endpoints, database, authentication, pricing rules or order logic changed.
Product listing still requests 24 items per page. Sorting is omitted until an
explicit sort is provided, preserving the administrator's configured default.
Category, brand, type, price, delivery, availability and discount filters continue
to be sent to the existing server endpoint, not applied to products in the browser.
No demo products or fixed commercial data were added to application code.

## Verification

```powershell
dotnet build Vitorize/Vitorize.Web/Vitorize.Web.csproj --no-restore -v quiet
node manuals/tools/check-home-reference.mjs --shell --catalog
```

The browser harness uses an isolated in-memory API and a local frontend process.
It does not start the production API or access the real database. Fixture data
is confined to `manuals/tools`. The catalog checks cover 1728, 1280, 1024, 960,
900, 768, 402 and 360px widths; grid columns, overflow and sidebar visibility;
API query parameters; page resets on filtering/sorting; category/brand/search
routes; category hierarchy; stock/redirect links; mobile dialog keyboard behavior;
failure, empty and retry states; and the dark palette.

The fixture checks request contracts, not the backend's full filtering algorithm.
Live customer login, wishlist/cart writes and payments are not exercised.
Existing homepage and stage-1 shell regression checks run in the same command.
Result: the combined browser suite passed with zero page JavaScript errors;
the web build passed with zero warnings and zero errors.
Browser screenshots and `report.json` are generated under the git-ignored
`manuals/artifacts/home-reference/` folder, including `catalog-1728.png`,
`catalog-402.png`, `catalog-filter-mobile.png` and `catalog-categories.png`.

Suggested commit:

`feat(storefront): redesign catalog listings and responsive filters`

Stage 3 (product detail) is not included in this change.
