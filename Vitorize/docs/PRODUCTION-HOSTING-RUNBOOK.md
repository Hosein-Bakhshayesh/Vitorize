# Plesk production deployment

A normal Plesk deployment uses no Vitorize-specific environment variables, no
physical path settings, and no hand-edited `web.config`. Publish the API and
Web projects, edit their included `appsettings.Production.json` files, upload
the results, and start the applications.

## API configuration

Before upload, edit `Vitorize.Api/appsettings.Production.json`:

1. Set `ConnectionStrings:DefaultConnection` to the production SQL Server
   connection string.
2. Replace `Jwt:SecretKey` with a random value of at least 32 UTF-8 bytes.
3. Replace `Encryption:Key` with exactly 32 UTF-8 bytes. Do not change this
   value after protected data has been written.
4. `Cors:AllowedOrigins` is preconfigured for `https://hbakhshayesh.ir`; amend
   it only if the storefront origin changes.
5. To create the initial administrator, set `BootstrapAdmin:Enabled` to `true`
   and fill its mobile, password, and full name. After the first successful
   start, set `Enabled` back to `false` and clear those values.

The database `Payment` and SMS settings remain their respective authoritative
sources. A new database starts with the non-live Zarinpal sentinel, which
blocks payment-gateway calls until an authorized operator configures it.

## Web configuration

`Vitorize.Web/appsettings.Production.json` is preconfigured to call
`https://api.hbakhshayesh.ir/api/` and serve media from
`https://api.hbakhshayesh.ir`. Change both together if the API origin changes.
It deliberately enables CKEditor GPL mode; leave the required CKEditor badge in
place. A commercial key may replace `CkEditor:LicenseKey`, after which
`AllowGplInProduction` should be set to `false`.

## Persistent application data

Each application creates and owns its own `App_Data` directory beneath its
published content root:

| Directory | Purpose |
| --- | --- |
| `App_Data/DataProtection` | Persistent Data Protection keys |
| API `App_Data/PublicMedia` | Files served by the API at `/uploads` |
| API `App_Data/PrivateDocuments` | Authorization-only documents; never static files |

Preserve `App_Data` whenever replacing published files. No server path is put
in configuration. The default forwarded-header policy trusts loopback, which is
the normal IIS/Plesk in-process path; deployments with an additional reverse
proxy may configure `Hosting:TrustedProxies` or `Hosting:TrustedProxyNetworks`
in appsettings with that proxy's known IP address or network.

## Plesk flow

1. Create the database and run the reviewed production SQL deployment.
2. Edit the API production settings as above.
3. Publish and upload the API, preserving its `App_Data` directory during later
   updates.
4. Edit and upload the Web production settings.
5. Start both applications. The published `web.config` needs no
   `environmentVariables` section.
6. Check API liveness at `/api/health/live` and readiness at
   `/api/health/ready`.
7. Log in with the bootstrap administrator, then disable bootstrap.
8. Check storefront login, a public upload, and an authorized private-document
   download. Restart the applications and confirm cookies and files still work.

Environment variables remain normal ASP.NET Core overrides, but none are
required by Vitorize for this deployment path.
