# Homepage reference implementation — 2026-09-12

Scope: only the customer homepage (`/`). Existing shared headers, footers,
product cards, other pages, API endpoints, database and application settings were
not edited. The layout selects the dedicated home components only at the root URL.

## Reference geometry

Measured directly from the supplied PNGs, not from their resized chat previews.

| Section starts (px) | Desktop, width 1728 | Mobile, width 402 |
| --- | ---: | ---: |
| Categories | 1000 | 682 |
| Products | 1486 | 1052 |
| Main banner | 2733 | 1684 |
| Benefits | 3844 | 1966 |
| Brands | 4388 | 2398 |
| Reviews | 4605 | 2462 |
| Instagram | 5284 | 2732 |
| Blog | 5942 | 2914 |
| FAQ | 6621 | 3150 |
| Footer | 7687 | 3628 |
| Total reference height | 8573 | 4265 |

These are minimum section sizes. Longer real product names, reviews, FAQ answers
or configured branding can expand their containers. Content is not cropped to
force these numbers. Intermediate widths are responsive, not additional designs.

## Data and deliberate differences from the placeholder artwork

- The existing home API and featured-product/default-sort selection are retained.
- Actual media, names, prices, currency, stock status, product redirects, categories
  and blog destinations remain API-driven. Gray surfaces are image fallbacks only.
- The desktop focus panel uses the supplied surface/blur geometry, but retains
  real category links instead of being an empty, nonfunctional panel.
- Reviews are approved public product reviews, not invented customer testimonials.
  Products with reviews supply up to four bounded review requests.
- Configured logos, brand images, social links, contact details, seal badges and
  marketing copy take precedence over presentation defaults. No fake contact
  details or trust certifications are inserted.
- The Instagram banner replaces the previous local-only newsletter form, which
  had reported success without saving a subscription.
- A failed home/catalog request displays an error and retry control. Empty
  successful responses display honest empty states.

## Verification

```powershell
dotnet build Vitorize/Vitorize.Web/Vitorize.Web.csproj --no-restore -v quiet
node manuals/tools/check-home-reference.mjs
```

The browser harness starts **only** the web frontend and an in-memory API fixture
on loopback ports 5088/5188. It does not start the real API or access a database.
It closes both processes when finished. Screenshots are fixture-based and must
not be presented as proof of a live-database connection or pixel-identical content.

Checks cover six widths (1728, 1280, 768, 402, 390, 360), exact section boundaries
at the two reference widths, no horizontal document overflow, responsive product
counts, FAQ interaction, focus/escape/keyboard containment, approved-only reviews,
error/retry/empty responses, navigation away and back, responsive API images,
stock labels, product redirects, banner switching and browser runtime errors.

Generated artifacts (git-ignored): `manuals/artifacts/home-reference/` containing
`desktop.png`, `mobile.png`, `desktop-focus.png`, and `report.json`.

Suggested commit:
`feat(storefront): align homepage with desktop and mobile references`
