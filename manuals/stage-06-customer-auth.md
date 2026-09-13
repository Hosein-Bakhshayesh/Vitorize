# Stage 6 — customer authentication UI

## Scope

Customer `/login`, `/register`, `/forgot-password` and `/reset-password`, including
password/OTP login and registration verification. No separate auth mockups were
provided: these pages extend the approved white/gray/mint design language.
Styles are scoped to `.site-shell .auth-page`; admin login and customer-account
settings are excluded. No homepage/shared-shell design changes are included.

## Changes

- Consistent centered rectangular cards, headings, logo/status icon, form spacing,
  mint method selection and primary actions, mobile and dark-theme styles.
- Explicit input/label associations, appropriate autocomplete and input direction,
  localized existing validation, and alert/status announcements.
- Native keyboard-operable password/OTP method buttons with pressed states.
- OTP request and password recovery/reset support Enter through forms calling the
  same existing handlers. Pending interactive requests disable related controls.
- Registration pending-code, resend, change-mobile and error/notice layouts remain
  connected to the existing server-side forms and pending registration cookie.
- Recovery toast presentation is scoped to the sibling toast host only while an
  auth page is rendered; it wraps inside the viewport and clears mobile navigation.

All four Razor `@code` blocks are byte-equivalent after newline normalization to
the pre-stage versions. API URLs/payloads, password rules, authentication cookies,
return URLs, OTP timers/outcomes, registration flow and guest-cart merge are
unchanged. No backend or auth-endpoint changes, new account data or mock store
content were added to the application.

## Verification

```powershell
dotnet build Vitorize/Vitorize.Web/Vitorize.Web.csproj --no-restore -v quiet
node manuals/tools/check-home-reference.mjs --shell --catalog --product --cart --checkout --auth
```

The auth suite tests seven widths from 320 to 1728px across all four pages and
the OTP login mode, checking overflow, labels and minimum input heights. It also
covers required/email validation without API writes, failed/unknown password
login, return URL propagation, OTP empty/unknown/blocked/error states, invalid
codes, resend/change-mobile, keyboard selection, registration pending-state gate,
resend/rejection/completion, reset mismatch/server failure/success and dark theme.
Recovery toast bounds and dismissal are also checked.

Result: build passed with zero warnings/errors. The combined home, shared-shell,
catalog, product, cart, checkout and auth browser suite passed with zero page
JavaScript errors (914 fixture requests). All four auth code blocks were compared
with HEAD and are unchanged after newline normalization.

All requests target the isolated in-memory `auth-fixture.mjs` server. Passwords,
codes, phone numbers and tokens are synthetic test literals. Successful flows
create only a temporary local browser session. No live SMS, real registration,
password change, production account, database or payment is used. These tests
verify UI and request contracts, not backend security or provider integration.

Screenshots and reports are generated in git-ignored
`manuals/artifacts/home-reference/`: `auth-login-402.png`, `auth-register-1728.png`,
all other auth pages at desktop/mobile widths, login/registration code states,
`auth-dark.png` and `report.json`.

Suggested commit: `feat(storefront): redesign customer authentication UI`

Stage 7 (customer account/dashboard) is not included.
