# Vitorize product experience implementation

## Deployment order (Database-First)

There are no EF migrations. Back up `VitorizeDb`, then execute these scripts with an account allowed to create tables, indexes, constraints and foreign keys:

1. `Database/2026-07-14_product_experience_schema.sql`
2. `Database/2026-07-14_seed_product_experience_settings.sql`
3. Deploy API and Web binaries.
4. Ensure `Vitorize.Api/wwwroot/uploads/fonts` and `.../uploads/settings` are writable and persistent between deployments.

Both scripts are idempotent. The settings script inserts missing keys only and does not overwrite production branding. The schema script is additive and contains rollback notes; dropping snapshot tables loses historical order input data and must never be done without an export.

The scripts are UTF-8. Use SSMS/Azure Data Studio or pass `-f 65001` to `sqlcmd` so Persian setting metadata is preserved.

## Product features

Product Create/Edit contains a structured feature builder. Title and value are required, title/value lengths are bounded, icons come only from `ProductIconCatalog`, and row order is normalized deterministically. Only active features are exposed by public product details. The storefront renders semantic `dl/dt/dd` cards and hides the section when empty.

## Dynamic customer information

Definitions support text, email, mobile, number, textarea, select, radio, checkbox, Telegram username, URL, date and an exceptional secret field. Required/type/length/options/regex rules execute on the server. Regex evaluation has a 200 ms timeout and patterns are length-limited.

Values entered on the product page are included in the cart fingerprint, so items with different inputs do not merge. The cart permits editing until checkout. Checkout revalidates active definitions and saves label/key/type snapshots in the same transaction as the order. Definition edits therefore do not make historical orders unreadable.

Sensitive values are AES-encrypted with the existing `Encryption:Key`, never stored in `Value`, never logged, and masked in cart/customer/admin responses. A SuperAdmin-only explicit reveal endpoint records `OrderSensitiveInputRevealed` in `SecurityLogs` without recording the plaintext. Do not collect third-party passwords. Define a lawful retention period before enabling any sensitive field; deletion should be handled through the store's data-retention process after fulfillment/dispute windows expire.

## Rich product descriptions

The editor is self-hosted **Quill 2.0.3**, distributed under the BSD license. It was selected because it is framework-neutral, works with Blazor interop, can be self-hosted, has RTL/alignment/history support and avoids a cloud API key or unclear commercial editor terms. Vendored files and license are under `Vitorize.Web/wwwroot/lib/quill`.

The toolbar provides headings, emphasis, lists, links, quotes, code, alignment/direction, a 2×2 table action, horizontal rule, undo/redo and fullscreen. Image insertion is deliberately absent until it can use the authenticated upload pipeline. The server sanitizes every write with HtmlSanitizer 9.x and a strict tag/attribute/scheme allowlist; scripts, event handlers, JavaScript URLs, iframes and styles are removed. The product service also sanitizes before rendering as defense in depth.

References: [Quill documentation](https://quilljs.com/docs), [Quill formats](https://quilljs.com/docs/formats/), [HtmlSanitizer package](https://www.nuget.org/packages/HtmlSanitizer/).

## Typography and branding

Typography Settings accepts WOFF2, WOFF and TTF. The API checks extension, MIME, binary signature, configured size (`Typography.MaxUploadMb`, clamped to 1–20 MB), friendly-family syntax and duplicate names. It generates a GUID filename under the canonical API media host. The active font emits a safe generated `@font-face`, uses `font-display: swap`, and retains Vazirmatn/Tahoma/system fallbacks. Scope values are: 1 storefront, 2 admin, 3 entire application. Selecting built-in Vazirmatn restores the default.

Logo settings retain the existing keys: `LogoPath`, `LogoDarkPath`, `LogoSmallPath`, `HeaderLogoPath`, `FooterLogoPath`, `FaviconPath`, `AppleTouchIconPath`, and OpenGraph/Twitter images. Settings uploads accept PNG/JPEG/WEBP; favicon additionally accepts signature-checked ICO. SVG is intentionally not accepted because uploaded active SVG content requires a separate sanitizer. `Branding.AssetVersion` is bumped after logo changes and appended to rendered URLs.

Recommended assets: header/logo 4:1 transparent PNG or WebP; mobile logo 1:1; favicon 32×32/48×48 PNG or ICO; Apple Touch 180×180 PNG; OpenGraph 1200×630.

## Trust seals

Enamad, ecunion.ir and samandehi.ir use generated anchors plus uploaded images. Destination URLs must be HTTPS and match the provider's official host/subdomain. Enabled seals with a missing/invalid URL or image are omitted. External links use `noopener noreferrer`, images lazy-load and include configured alt text. Script/embed settings are not supported. The former public raw custom-head/footer rendering was removed, and the `Scripts` settings group is no longer exposed by the public settings endpoint.

## Context menus and themes

All current admin three-dot menus (products, orders, users, coupons) use `AdminContextMenu`. The menu uses the browser top layer, fixed viewport positioning, RTL alignment, horizontal clamping and vertical flipping. Native popover behavior handles outside click and Escape; first-action focus and trigger focus restoration are implemented.

Admin and storefront styles now expose semantic background/text/border/focus/shadow aliases. Native selects/options explicitly use theme surfaces and `color-scheme`; placeholders, disabled states, editor toolbar/dropdowns/tooltips, builders, feature cards, forms and trust seals use the dedicated light/dark palettes. Browser-native `<option>` hover rendering remains controlled by each operating system, but text/background and selected/disabled contrast are explicitly supplied.

## Verification checklist

- Run `dotnet restore Vitorize.sln`, `dotnet build Vitorize.sln`, and `dotnet test Vitorize.Tests/Vitorize.Tests.csproj`.
- In both themes and desktop/mobile widths: open all four admin row menus near every viewport edge; use keyboard and Escape.
- Upload/activate a font in each scope, reload and confirm cache/version behavior.
- Replace light/dark/mobile/footer logos, favicon, Apple icon and OpenGraph image.
- Configure each trust seal with its official HTTPS verification URL and image.
- Create a product with reordered features, formatted HTML and product/checkout-stage fields.
- Add two copies with different values; confirm separate cart rows, edit values, checkout, customer order display and admin masked display.
- As SuperAdmin only, reveal a sensitive value and confirm a `SecurityLogs` audit row.
