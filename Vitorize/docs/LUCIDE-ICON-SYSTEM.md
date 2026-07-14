# Vitorize Lucide icon system

## Package and license

Vitorize vendors the official `lucide-static` package at the exact version **1.24.0**. The generated catalog contains **1,746** official icon keys. Runtime rendering does not use a CDN or require internet access.

- Upstream: <https://lucide.dev>
- Package: <https://www.npmjs.com/package/lucide-static>
- Vendored license: `Vitorize.Web/wwwroot/lib/lucide/LICENSE`
- Pinned generator dependency: `tools/lucide/package.json` and `package-lock.json`

To regenerate after an intentional package upgrade:

```powershell
cd tools/lucide
npm.cmd ci --ignore-scripts
npm.cmd run generate
```

The generator reads the package's official `icon-nodes.json` and `tags.json`, then produces:

- `Vitorize.Shared/Icons/LucideIconCatalog.Generated.cs`
- `Vitorize.Web/wwwroot/lib/lucide/lucide-sprite.svg`
- Version and license files under `wwwroot/lib/lucide`

Review the Lucide changelog and run all tests before committing an upgrade.

## Storage and security

Configurable records store only a normalized official kebab-case key such as `gamepad-2`. Raw SVG, HTML, JavaScript, URLs and uploaded SVG are not accepted. The renderer resolves a key through `LucideIconCatalog` and references only a symbol bundled in the local sprite. Unknown historical values render the configured fallback rather than becoming markup.

No schema change was required: `Categories.Icon`, `ProductFeatures.IconKey`, and JSON setting `icon` properties already hold strings. `Database/2026-07-14_optional_normalize_legacy_lucide_icons.sql` is an optional, idempotent data cleanup; the application does not require it and does not silently rewrite existing data.

## Shared components

### Renderer

```razor
<LucideIcon IconKey="gamepad-2" Size="24" StrokeWidth="1.8" />
<LucideIcon IconKey="@databaseValue" FallbackIconKey="shapes"
            Decorative="false" AriaLabel="نوع محصول" />
```

`LucideIcon` supports `IconKey`, `Size`, `StrokeWidth`, `CssClass`, `Title`, `AriaLabel`, `FallbackIconKey`, and `Decorative`. It preserves `currentColor`. The old `Icon` component is now a compatibility façade over this renderer so fixed application icons also use the official sprite.

### Picker

```razor
<LucideIconPicker @bind-Value="model.IconKey"
                  Required="true"
                  Placeholder="انتخاب آیکون" />
```

Optional parameters include `Disabled`, `ReadOnly`, `Compact`, `AllowedCategories`, and `ExcludedIcons`. The picker provides search, categories, preview, clear/confirm/cancel, double-click confirmation, copy, favorites, recent icons and invalid-legacy feedback.

Future configurable icon fields must use `LucideIconPicker`; do not add a text input for icon names.

## Catalog, search and Persian aliases

`Vitorize.Shared.Icons.LucideIconCatalog` is the shared source of truth for rendering, validation, picker results, search and fallbacks. Official keys/tags are generated from the pinned package. Hand-maintained Persian aliases, popularity and category heuristics live in `LucideIconCatalog.cs`.

Search normalizes Persian/Arabic `ی/ي`, `ک/ك`, zero-width joiners, spaces, underscores and hyphens. It supports multi-token, partial, compact and fuzzy-subsequence matching, with exact keys ranked first. To add a Persian term, add it to the `PersianAliases` entry for an existing official key and add a unit test.

Brand searches intentionally return semantic Lucide alternatives:

- PlayStation/Xbox/Steam → `gamepad-2`
- Telegram → `send`
- Discord → `message-circle` or `messages-square`

Lucide does not provide an official brand-icon catalog. Vitorize does not invent brand icons or bundle third-party brand SVGs. A separately licensed and separately validated brand library can be added later without weakening the Lucide allowlist.

## Legacy mappings

Known internal aliases are normalized on the next explicit save, including:

- `grid` → `layout-grid`
- `cart` → `shopping-cart`
- `dashboard` → `layout-dashboard`
- `check-circle` → `circle-check`
- `edit` → `pencil`
- `refresh` → `refresh-cw`
- `logout` → `log-out`
- `message` → `message-circle`

Unknown existing keys remain stored unchanged, are marked invalid in Admin, and render a safe fallback. An administrator must explicitly replace them.

## Performance and preferences

The picker searches cached normalized metadata in memory. It renders only the first 120 matching icons and incrementally exposes additional batches, avoiding a 1,746-node initial render. The SVG sprite is one static cacheable file; individual icons do not require SVG parsing or network requests.

Favorites (maximum 100) and recent selections (maximum 24) are stored in browser local storage using a key scoped to the authenticated admin identifier. No database change is required.

## Accessibility and themes

The native modal `dialog` supplies the top layer and focus trap. Escape, outside click, focus restoration, Tab navigation, grid arrow keys, Enter/Space, visible focus, selected state, screen-reader labels and reduced-motion preferences are supported. The picker inherits Vitorize semantic tokens, Vazirmatn, RTL, and light/dark palettes. On mobile it becomes a full-screen dialog with sticky actions and large targets.

## Server validation

`LucideIconRules` performs server normalization and validation for categories, product features, and JSON-backed homepage/trust cards. Optional values may be empty; required values must resolve to an official key. Settings JSON is bounded to 24 cards and validates title/text lengths. Client selection is never treated as sufficient validation.
