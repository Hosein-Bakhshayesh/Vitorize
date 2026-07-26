# CKEditor 5 licensing configuration

The admin product description editor uses a self-hosted **CKEditor 5** build.
CKEditor 5 is dual-licensed (GPL **or** commercial). Vitorize is a proprietary,
commercial application, so Production must run under a **commercial license key**.

## Configuration key

| Setting | Value |
| --- | --- |
| Configuration path | `CkEditor:LicenseKey` |
| Environment variable | `CkEditor__LicenseKey` |
| Type | string (CKEditor commercial key, or the literal `GPL`) |

The key is resolved and validated at Web host startup by
`Vitorize.Web.Services.CkEditorOptions.Resolve(...)`.

## Behaviour by environment

| Environment | Configured value | Result |
| --- | --- | --- |
| **Production** | commercial key | ✅ used |
| **Production** | empty / missing | ❌ **startup fails fast** with a configuration error |
| **Production** | `GPL` | ❌ **startup fails fast** (GPL is not permitted in Production) |
| **Development** (and other non-Production) | `GPL` | ✅ used (explicitly configured in `appsettings.Development.json`) |
| **Development** (and other non-Production) | commercial key | ✅ used |
| **Development** (and other non-Production) | empty / missing | falls back to `GPL` for local convenience |

The startup error message names the environment variable to set and points here.

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
