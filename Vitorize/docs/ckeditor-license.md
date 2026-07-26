# CKEditor 5 licensing configuration

The admin product description editor uses a self-hosted **CKEditor 5** build.
CKEditor 5 is dual-licensed (GPL **or** commercial). Vitorize is a proprietary,
commercial application, so Production must run under a **commercial license key**.

## Configuration keys

| Setting | Configuration path | Environment variable | Type / default |
| --- | --- | --- | --- |
| License key | `CkEditor:LicenseKey` | `CkEditor__LicenseKey` | string (commercial key, or the literal `GPL`) |
| Allow GPL in Production | `CkEditor:AllowGplInProduction` | `CkEditor__AllowGplInProduction` | bool, default **`false`** |

Both are resolved and validated at Web host startup by
`Vitorize.Web.Services.CkEditorOptions.Resolve(...)`.

## Behaviour by environment

| Environment | License key | `AllowGplInProduction` | Result |
| --- | --- | --- | --- |
| **Production** | commercial key | any | ✅ used (the flag is ignored) |
| **Production** | `GPL` | `true` | ✅ used, with a startup **warning** (temporary GPL mode) |
| **Production** | `GPL` | `false` / missing | ❌ **startup fails fast** |
| **Production** | empty / missing | any | ❌ **startup fails fast** |
| **Development** (and other non-Production) | `GPL` | n/a | ✅ used (explicit in `appsettings.Development.json`) |
| **Development** (and other non-Production) | commercial key | n/a | ✅ used |
| **Development** (and other non-Production) | empty / missing | n/a | falls back to `GPL` for local convenience |

The startup error messages name the environment variable(s) to set and point here.

## Temporary GPL mode in Production

To run Production under CKEditor's GPL licence **temporarily** (e.g. before a
commercial key is purchased), supply **both** environment variables:

```
CkEditor__LicenseKey=GPL
CkEditor__AllowGplInProduction=true
```

On startup the host logs the warning:

> CKEditor 5 is running in GPL mode in Production. Ensure the application complies with the applicable GPL license obligations.

The **"Powered by CKEditor"** badge remains visible in this mode (see below).

Keep `AllowGplInProduction=false` in source (`appsettings.json`); the `true`
override is a runtime environment variable only and must **not** be committed.

## Switching to a commercial key later

1. Obtain a CKEditor 5 commercial licence key.
2. Set `CkEditor__LicenseKey=<commercial-key>` in the Production environment /
   secret store.
3. **Remove** `CkEditor__AllowGplInProduction` (or set it to `false`). The flag is
   ignored for a commercial key, but removing it restores the default fail-fast
   guard so a future accidental `GPL` value cannot silently ship.
4. Redeploy. CKEditor removes the "Powered by CKEditor" badge automatically under
   a valid commercial licence.

> **Recommendation:** once the commercial licence is in place, delete the
> `CkEditor__AllowGplInProduction` override entirely so GPL can never be used in
> Production again by mistake.

## Secret handling

- **Do not** put the commercial key in `appsettings.json` — it ships an empty
  placeholder only. No secret is committed to source control.
- Supply the key in Production through the `CkEditor__LicenseKey` environment
  variable (container/orchestrator secret, CI/CD secret store, or the .NET secret
  manager for local commercial testing).

## GPL "Powered by CKEditor" branding

When running under the `GPL` key, CKEditor renders the required **"Powered by
CKEditor"** badge. Vitorize does **not** hide or remove it:

- no `config.ui.poweredBy` override is set in `wwwroot/js/ckeditor-interop.js`;
- no CSS in `wwwroot/css/ckeditor-theme.css` hides `.ck-powered-by`.

When a commercial key is configured, CKEditor removes the badge automatically as
part of its own licensing — the application does not manipulate it.

> This document describes the **technical safeguards** implemented in the code. It
> is not legal advice and does not itself constitute a statement of legal
> compliance; obtain an appropriate CKEditor commercial license for production use.
