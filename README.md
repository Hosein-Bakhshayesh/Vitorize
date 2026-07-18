# Vitorize — Backend

Persian (RTL) gift card & digital-services store. Layered ASP.NET Core 8 backend.

```
Vitorize.Api             → controllers, middleware, Program.cs, background services
Vitorize.Application     → DTOs, service interfaces, FluentValidation validators
Vitorize.Domain          → EF entities (DB-First / scaffolded)
Vitorize.Infrastructure  → EF DbContext, service implementations, interceptors
Vitorize.Shared          → enums, ApiResult / PagedResult, exceptions
Vitorize.Web             → Razor UI (NOT part of the backend; do not edit for API work)
```

All API responses use `ApiResult` / `ApiResult<T>`. Controlled errors are thrown as
`BusinessException` (400), `NotFoundException` (404), `UnauthorizedException` (401) and
translated by `GlobalExceptionMiddleware`; unhandled errors return a generic message and are
written to `ErrorLogs`. Every entity change is automatically written to `AuditLogs` by
`AuditSaveChangesInterceptor`.

## Database

This solution is **DB-First**: `VitorizeDbContext` is scaffolded from an existing SQL Server
database (`VitorizeDb`). Do **not** generate EF migrations. If a change needs new
tables/columns, add a `.sql` script under `database/` and reference it here.

The recent backend work (Product Reviews module) consumes the **already-existing**
`ProductReviews` and `ProductReviewVotes` tables, so **no SQL scripts are required**.

## How to run the backend

1. Configure the connection string `ConnectionStrings:DefaultConnection` and the `Jwt`,
   `Encryption`, and `Zarinpal` sections in `Vitorize.Api/appsettings.json`
   (use `appsettings.Development.json` for local).
2. Restore & build:
   ```bash
   dotnet restore Vitorize/Vitorize.sln
   dotnet build   Vitorize/Vitorize.sln -c Debug
   ```
3. Run the API (seeds base roles + admin on startup via `SeedVitorizeInitialDataAsync`):
   ```bash
   dotnet run --project Vitorize/Vitorize.Api
   ```
4. Swagger UI is available in Development at `/swagger`.

Authorization policies (`Program.cs`): `AdminOnly` (Admin/SuperAdmin), `SuperAdminOnly`,
`SupportOnly`. Admin controllers require `AdminOnly`; customer controllers require an
authenticated user; storefront GET endpoints are anonymous.

## Product Reviews & Votes (module added in this pass)

Storefront / customer — `api/product-reviews`:

| Method & Route | Auth | Purpose |
| --- | --- | --- |
| `GET  /product/{productId}` | anonymous | Approved reviews (paged) + rating summary; includes the caller's own vote when authenticated |
| `GET  /product/{productId}/summary` | anonymous | Rating summary only (average + 1–5 star breakdown) |
| `GET  /mine` | customer | The caller's own reviews (any status) |
| `POST /` | customer | Create a review (starts pending; rating 1–5; one per product per user) |
| `PUT  /{reviewId}` | customer | Edit own review (only while not yet approved; re-enters moderation) |
| `DELETE /{reviewId}` | customer | Soft-delete own review |
| `POST /{reviewId}/vote` | customer | Vote helpful (1) / unhelpful (2); one per user, change supported |
| `DELETE /{reviewId}/vote` | customer | Remove own vote |

Admin — `api/admin/product-reviews` (`AdminOnly`):

| Method & Route | Purpose |
| --- | --- |
| `GET /` | List/filter (product, user, rating, approval/rejection, search, date range, paged) |
| `GET /{id}` | Review detail incl. product/user info and vote counts |
| `POST /{id}/approve` | Approve & publish (notifies customer) |
| `POST /{id}/reject` | Reject with required reason (notifies customer) |
| `DELETE /{id}` | Soft-delete |

Rules enforced: only **approved** reviews are shown on the storefront; customers can only
read/modify their **own** reviews; a user cannot vote on their **own** review; `LikeCount` /
`DislikeCount` are recomputed from the votes table inside a transaction; approve/reject/delete
are auto-audited and create customer notifications.

## Additional endpoints added in this pass

Storefront / customer:

| Method & Route | Auth | Purpose |
| --- | --- | --- |
| `GET api/storefront/banners?position=` | anonymous | Active banners (date-window respected), optionally filtered by position, sorted by `SortOrder` |
| `GET api/products/{id}/related?count=` | anonymous | Related active products (same category, then brand), excluding the product itself |
| `DELETE api/verification/documents/{documentId}` | customer | Delete own verification document — only while the profile is still pending |
| `GET api/notifications/unread-count` | customer | Count of the caller's unread notifications |

Admin (`AdminOnly`):

| Method & Route | Purpose |
| --- | --- |
| `GET api/admin/giftcodes/batches/{batchId}` | Batch detail |
| `GET api/admin/giftcodes/codes` | Gift codes (paged/filtered by product, variant, batch, status, masked-code/serial search) — **masked only, never raw/encrypted values** |
| `POST api/admin/notifications/send` | Send a system notification to a specific user (validates the user exists) |

## FinalPrice rule (hardened in this pass)

`FinalPrice = (DiscountPrice > 0 && DiscountPrice < BasePrice) ? DiscountPrice : BasePrice`
is now applied consistently in the backend storefront/variant DTOs **and** in `CartService`
(the price actually charged at checkout). Previously the cart used `DiscountPrice ?? BasePrice`,
which would charge a discount price even when it was greater than or equal to the base price.

## Backend smoke-test checklist

There is no automated test project. After `dotnet build` succeeds, verify manually:

- **Auth** — register → login → refresh → current-user → change password.
- **Storefront** — `GET api/products`, product detail, `GET api/storefront/home`,
  `GET api/product-reviews/product/{id}` (anonymous).
- **Reviews (customer)** — create review (pending), it is **not** visible on the storefront;
  cannot create a second review for the same product; edit while pending; vote on **another**
  user's approved review; cannot vote your own.
- **Reviews (admin)** — list/filter, approve (now visible on storefront + customer notified),
  reject with reason (customer notified), delete.
- **Pricing** — add a product with `DiscountPrice >= BasePrice` to the cart and confirm the
  cart unit price equals `BasePrice` (not the discount).
- **Authorization** — a customer token is rejected (403) on any `api/admin/*` route; one
  customer cannot read another customer's reviews/orders/tickets.

What could **not** be tested in this environment: runtime execution (no SQL Server / `VitorizeDb`
instance available here). Verification was limited to a clean `dotnet build` of the full
solution (0 errors).

---

## Development Secret Configuration

The API validates two secrets at startup and **refuses to start** without them — by design, so real
secrets never live in source control. `appsettings.json` intentionally ships them empty:

```jsonc
"Jwt":        { "SecretKey": "" },
"Encryption": { "Key": "" }
```

### Required secrets

| Config key | Minimum length | Notes |
|---|---|---|
| `Jwt:SecretKey` | **≥ 32 bytes** (UTF‑8) | HMAC signing key for JWTs |
| `Encryption:Key` | **exactly 32 bytes** (UTF‑8) | AES‑256 key for encrypting stored secrets (gift codes, KYC, sensitive inputs) |

> Use ASCII/hex values so **1 character = 1 byte**. `Encryption:Key` must be *exactly* 32 characters
> of ASCII; `Jwt:SecretKey` must be at least 32.

### Configuration priority

The app reads configuration in this order (**later sources win**), so an environment variable always
overrides a User Secret:

- **Development:** `appsettings*.json` → **User Secrets** → **Environment Variables** → launch‑profile env vars
- **Production:** `appsettings*.json` → **Environment Variables** → external secret provider (if configured)

User Secrets are loaded automatically **only in the Development environment**. Local Visual Studio
debugging (F5) now defaults to Development when no environment is set (see *How it works* below), so
User Secrets are the recommended local mechanism.

### Configure with `dotnet user-secrets` (recommended)

Run once per machine from the API project folder. The keys below are throwaway **local‑dev** values —
generate your own:

```powershell
cd Vitorize/Vitorize.Api

# Generate secrets (PowerShell) — hex, so length == byte count
$jwt = -join ((1..64) | ForEach-Object { '{0:x}' -f (Get-Random -Max 16) })   # 64 bytes
$enc = -join ((1..32) | ForEach-Object { '{0:x}' -f (Get-Random -Max 16) })   # 32 bytes exactly

dotnet user-secrets set "Jwt:SecretKey"  $jwt
dotnet user-secrets set "Encryption:Key" $enc
dotnet user-secrets list
```

### Configure in Visual Studio

Right‑click **Vitorize.Api → Manage User Secrets**, then add:

```json
{
  "Jwt:SecretKey": "<64+ hex chars>",
  "Encryption:Key": "<exactly 32 hex chars>"
}
```

### Configure via `launchSettings.json` (dev‑only env vars — do NOT commit real secrets)

`Vitorize.Api/Properties/launchSettings.json` already sets `ASPNETCORE_ENVIRONMENT=Development` per
profile. You *may* add local secret values here for convenience, but **launchSettings.json is
source‑controlled**, so treat any value committed here as throwaway‑local only:

```jsonc
"environmentVariables": {
  "ASPNETCORE_ENVIRONMENT": "Development",
  "Jwt__SecretKey": "<64+ hex chars>",
  "Encryption__Key": "<exactly 32 hex chars>"
}
```

> Note the `__` (double underscore) separator for nested keys in environment variables.

### Configure via Windows Environment Variables

```powershell
# User-scoped, persists across sessions (new terminals/VS must be restarted to pick them up)
setx Jwt__SecretKey  "<64+ hex chars>"
setx Encryption__Key "<exactly 32 hex chars>"
```

### Configure via PowerShell (current session only)

```powershell
$env:Jwt__SecretKey  = "<64+ hex chars>"
$env:Encryption__Key = "<exactly 32 hex chars>"
dotnet run --project Vitorize/Vitorize.Api
```

### Production

Do **not** use User Secrets in Production. Supply the two keys via **environment variables** (or your
host's secret provider). The startup validation is identical in every environment — the keys are
mandatory and length‑checked.

### How local Development startup works

The Visual Studio *multi‑project* launch ("New Profile") does not always apply a per‑project launch
profile, which left `ASPNETCORE_ENVIRONMENT` unset (→ Production) and skipped User Secrets. To fix the
local experience without weakening Production, `Program.cs` (API and Web) sets the environment to
**Development only when a debugger is attached and no environment was explicitly chosen**. Deployed
Production hosts run without a debugger, so they are unaffected and the secret validation stays fully
enforced.
