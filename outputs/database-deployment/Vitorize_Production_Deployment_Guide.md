# Vitorize Version 1 production deployment

## Required order

1. Confirm SQL Server 2022+ (major version 16) and compatibility level 160 support. Confirm the backup and restore policy before making changes.
2. Create a one-time deployment account with server `CREATE DATABASE` and `db_owner` only on the new `VitorizeDb`. Do not use sysadmin for the application.
3. From this directory, enable **Query > SQLCMD Mode** in SSMS (or use SQLCMD) and execute `Vitorize_Production_Full.sql`. The script uses relative `:r` includes, so its working directory must be this deployment folder.
4. Execute `Vitorize_Production_Verification.sql` in the target database. Do not proceed on any failure.
5. Create a separate runtime login/user with only the application permissions required by the installed version (normally `db_datareader`, `db_datawriter`, and `EXECUTE`); do not grant `db_owner` or server permissions to the runtime identity.
6. Edit the included API and Web `appsettings.Production.json` files. Preserve each application's `App_Data` directory during future package replacement; it holds Data Protection keys and, for the API, public and private uploads.
7. Install the API publish output, configure its HTTPS binding/reverse proxy and forwarded-header trust list, then check `/api/health/live` and `/api/health/ready`.
8. Install the Web publish output separately, configure its API base address and HTTPS binding. Check the home page and Admin login page.
9. For one API startup only, set `BootstrapAdmin:Enabled=true` and fill its mobile, password (12-72 UTF-8 bytes), and full name in the API production settings. Verify exactly one enabled SuperAdmin and the `BootstrapSuperAdminCreated` event, login, then disable bootstrap and clear its values before restarting.
10. Complete storefront/Admin smoke tests and record the results. Verify backup/restore procedures before customer acceptance.

## Payment activation gate

The package creates exactly these required protected settings: `ZarinpalMerchantId`, `ZarinpalSandbox`, `ZarinpalBaseUrl`, `ZarinpalStartPayUrl`, and `ZarinpalCallbackUrl`. The initial MerchantId is a non-live sentinel that blocks gateway calls. Before deliberately enabling live payments, set a real merchant identifier through protected configuration, set the final HTTPS callback URL (`/api/payments/zarinpal/callback`), review the live gateway URLs, complete provider certification, and obtain the operational approval. Never place gateway, SMS, or SMTP secrets in SQL or source control.

## Storefront typography

The Web publish package self-hosts Peyda, Funnel Display, and Manrope under `wwwroot/fonts/storefront/`; no font CDN or Google Fonts dependency is used. The production seed inserts the following values only when they are missing, so a previously configured value is preserved:

- `StorefrontPersianFont`: `Peyda`
- `StorefrontEnglishFont`: `Funnel Display`

Funnel Display falls back to Manrope when a Latin glyph is unavailable. An authorized Admin can change either storefront setting in **Settings → Typography** and refresh the storefront to load the new selection. These settings apply only to the customer storefront; Admin typography intentionally remains separate and unchanged.

## IIS / reverse-proxy checklist

Install the .NET 8 Hosting Bundle; use separate API and Web application pools set to **No Managed Code**; configure HTTPS certificates and bindings; set forwarded headers only for known proxies; preserve the application-owned `App_Data` directories; configure log retention; recycle one component at a time and recheck liveness/readiness. Edit the included `appsettings.Production.json` files rather than Plesk environment variables. Configure monitoring to use the health URLs and collect customer acceptance evidence after the smoke test.
