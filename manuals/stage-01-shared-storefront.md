# Stage 1 — shared storefront shell

## Scope

The `site-shell` class is applied to non-home StoreLayout routes and CustomerLayout.
The approved homepage continues to use `home-shell`, HomeHeader and HomeFooter.
Admin layouts and page-specific storefront/customer components are not redesigned.
No API endpoint, database, authentication policy or checkout logic was changed.

## Changes

- Shared white/gray/mint presentation tokens, rectangular controls, form focus,
  disabled/error states, responsive spacing and a preserved dark-theme option.
- Desktop logo/navigation/actions and a mobile logo-centered header with four
  consistently positioned controls.
- Visible real category navigation, subcategories and lazy-loaded products.
  The category panel opens by click or keyboard, with a blurred backdrop.
- One desktop/mobile search dialog retaining category suggestions and the existing
  encoded `/search?q=` route. Escape, focus return and Tab containment are supported.
- Mobile menu includes real root categories and the existing theme toggle.
- Footer restores configured descriptions, social icons, contact links and trust
  badges. No fictitious contacts or certifications are added.
- Mobile bottom navigation supports safe-area spacing and one active destination,
  updated during in-circuit navigation.
- Configured branding and custom/light/dark logos remain supported.

## Verification

```powershell
dotnet build Vitorize/Vitorize.Web/Vitorize.Web.csproj --no-restore -v quiet
node manuals/tools/check-home-reference.mjs --shell
```

The existing homepage regression checks run first. Shared-shell checks cover
1728, 1280, 768, 402 and 360px widths, header overlap/overflow, footer links,
search submission/escaping, category content, mobile menu, bottom navigation,
keyboard behavior, login-form presentation and theme switching.

Homepage isolation is checked by comparing every element's geometry and key
computed styles with the new stylesheet enabled and disabled at 1728 and 402px.
Both versions are also captured for visual inspection. PNG byte equality is not
used as the criterion: Chromium can resample the small logo differently on
successive full-page captures even when computed styles and geometry agree.
Admin login is checked for absence of either storefront shell.

Tests run against an isolated in-memory public API fixture, not the live database.
Authenticated customer pages and a real payment were **not** exercised; no customer
credentials were used. Styling CustomerLayout does not constitute completing its
later dashboard/account-page redesign stages.

Artifacts: `manuals/artifacts/home-reference/shell-1728.png`,
`shell-402.png`, `shell-category-focus.png`, `shell-mobile-search.png`,
`shell-dark-controls.png` and `report.json` (generated and git-ignored).

Suggested commit:

`feat(storefront): unify shared customer-facing shell and controls`
