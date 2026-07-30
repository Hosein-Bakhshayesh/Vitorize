# Vitorize Version 1 production deployment

## Required order

1. Confirm SQL Server 2022+ (major version 16) and compatibility level 160 support. Confirm the backup and restore policy before making changes.
2. Create a one-time deployment account with server `CREATE DATABASE` and `db_owner` only on the new `VitorizeDb`. Do not use sysadmin for the application.
3. From this directory, enable **Query > SQLCMD Mode** in SSMS (or use SQLCMD) and execute `Vitorize_Production_Full.sql`. The script uses relative `:r` includes, so its working directory must be this deployment folder.
4. Execute `Vitorize_Production_Verification.sql` in the target database. Do not proceed on any failure.
5. Create a separate runtime login/user with only the application permissions required by the installed version (normally `db_datareader`, `db_datawriter`, and `EXECUTE`); do not grant `db_owner` or server permissions to the runtime identity.
6. Configure all environment variables/secrets outside the package. Persist Data Protection keys in a protected shared directory and grant the API/Web pool identities only the required read/write access. Create protected public-media and private-document directories and grant only their owning process identity write access.
7. Install the API publish output, configure its HTTPS binding/reverse proxy and forwarded-header trust list, then check `/api/health/live` and `/api/health/ready`.
8. Install the Web publish output separately, configure its API base address, HTTPS binding, trusted proxy configuration and the same persistent cookie/Data Protection policy. Check the home page and Admin login page.
9. For one API startup only, provide `BootstrapAdmin__Enabled=true`, `BootstrapAdmin__Mobile`, `BootstrapAdmin__Password` (12-72 UTF-8 bytes), and `BootstrapAdmin__FullName` through the secret provider. Verify exactly one enabled SuperAdmin and the `BootstrapSuperAdminCreated` event, login, then remove all BootstrapAdmin values and restart.
10. Complete storefront/Admin smoke tests and record the results. Verify backup/restore procedures before customer acceptance.

## Payment activation gate

The package creates exactly these required protected settings: `ZarinpalMerchantId`, `ZarinpalSandbox`, `ZarinpalBaseUrl`, `ZarinpalStartPayUrl`, and `ZarinpalCallbackUrl`. The initial MerchantId is a non-live sentinel that blocks gateway calls. Before deliberately enabling live payments, set a real merchant identifier through protected configuration, set the final HTTPS callback URL (`/api/payments/zarinpal/callback`) and matching `Hosting:PublicOrigin`, review the live gateway URLs, complete provider certification, and obtain the operational approval. Never place gateway, SMS, SMTP, JWT, encryption, connection-string, or Data Protection secrets in SQL or source control.

## IIS / reverse-proxy checklist

Install the .NET 8 Hosting Bundle; use separate API and Web application pools set to **No Managed Code**; configure HTTPS certificates and bindings; set forwarded headers only for known proxies; set persistent writable storage and key directories; configure log retention; recycle one component at a time and recheck liveness/readiness. Place environment variables in the server secret/configuration facility, not in `appsettings.json`. Configure monitoring to use the health URLs and collect customer acceptance evidence after the smoke test.
