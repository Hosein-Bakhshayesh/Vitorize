# CKEditor 5 licensing configuration

The admin editor uses a self-hosted CKEditor 5 build. Configure it in
`Vitorize.Web/appsettings.Production.json`:

| Setting | Purpose |
| --- | --- |
| `CkEditor:LicenseKey` | Commercial key, or the literal `GPL` |
| `CkEditor:AllowGplInProduction` | Required and `true` only for intentional GPL deployment |

Production rejects a missing license key and rejects `GPL` unless
`AllowGplInProduction` is `true`. The supplied production settings intentionally
select GPL mode and the required “Powered by CKEditor” badge remains visible.

To switch to commercial licensing, replace `LicenseKey` in
`appsettings.Production.json`, set `AllowGplInProduction` to `false`, and
publish the Web application. The key is delivered to the browser by CKEditor's
own design; protect access to the production configuration file appropriately.

This document describes technical safeguards, not legal advice.
