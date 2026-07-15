# Vitorize Database Deployment Manifest

Status: canonical upgrade chain validated on SQL Server 2022 on 2026-07-14. The project
is Database-First and has no EF Core migrations. `deployment-manifest.json` is the
machine-readable source for execution order and immutable SHA-256 checksums. Script
checksums use strict UTF-8 decoded content with line endings normalized to LF, so Git
checkout policy cannot change an immutable hash; the DACPAC hash remains a raw-file hash.

## Required execution order

Run `Deploy-Database.ps1`; do not execute the following list by hand in Production.

| Order | Version | File | Purpose | Transaction/rerun behavior |
|---:|---|---|---|---|
| 10 | V0001 | `Versioned/V0001__create_database_script_history.sql` | Create immutable deployment ledger and indexes | Atomic table/index setup; trigger created with `CREATE OR ALTER`; safe rerun |
| 20 | V0002 | `Versioned/V0002__normalize_gift_code_reservation_status_constraint.sql` | Replace the unsafe 2026-07-07 constraint patch | Validates rows/conflicts first; atomic; safe rerun |
| 30 | H20260713-SMS-SCHEMA | `2026-07-13_create_sms_history.sql` | SMS history tables, indexes and purge procedure | Object-idempotent; no outer transaction; runner records only after success |
| 40 | H20260714-PRODUCT-SCHEMA | `2026-07-14_product_experience_schema.sql` | Product features/input/font schema | Transactional and object-idempotent; requires core commerce tables |
| 50 | V0003 | `Versioned/V0003__seed_reference_roles.sql` | Seed four non-secret role records | Atomic; inserts missing roles only; never creates users |
| 55 | V0004 | `Versioned/V0004__financial_integrity_and_security_hardening.sql` | Financial uniqueness, refunds, protected KYC/delivery metadata, recoverable outbox | Validates duplicate financial keys first; atomic additive upgrade |
| 57 | V0005 | `Versioned/V0005__seo_content_and_legacy_redirects.sql` | SEO editorial/media fields, ProductTag aliases and exact legacy redirect registry | Atomic additive upgrade; preserves existing content/settings |
| 60 | H20260708-UI | `2026-07-08_seed_settings_ui_customization.sql` | UI/branding settings metadata and defaults | Inserts missing keys only; preserves configured values |
| 70 | H20260713-SMS-SEED | `2026-07-13_seed_sms_settings.sql` | SMS settings metadata/defaults | Inserts missing keys and preserves API key/template values |
| 80 | H20260714-PRODUCT-SEED | `2026-07-14_seed_product_experience_settings.sql` | Typography/trust/branding defaults | Inserts missing keys only; preserves configured values |

## Complete historical classification

| File | Classification | Canonical action | Dependencies / conflict notes |
|---|---|---|---|
| `2026-07-07_fix_GiftCodeReservations_Status_constraint.sql` | Superseded | Never run in the canonical chain; V0002 replaces it | Drops/re-adds outside a transaction and takes a schema lock |
| `2026-07-08_data_fix_image_paths_and_names.sql` | Environment-specific | Run only with explicit `H20260708-DATA-FIX` selection after confirming the exact affected row | Clears one historical missing category image path; no schema/user/password changes |
| `2026-07-08_seed_settings_ui_customization.sql` | Required | Ledgered canonical seed | Requires `dbo.Settings`; overlaps idempotently with startup seeding |
| `2026-07-13_create_sms_history.sql` | Required | Ledgered canonical schema | Requires `dbo.Users`; inspect manually if a partial SMS table already exists |
| `2026-07-13_otpcodes_purpose_login_constraint.sql` | Environment-specific | Select `H20260713-OTP-COMPAT` only when preflight warns about a restrictive legacy constraint | Current reference schema has no Purpose constraint and needs no change |
| `2026-07-13_seed_sms_settings.sql` | Required | Ledgered canonical seed | Requires `dbo.Settings`; no real SMS.ir ID/API key is hardcoded |
| `2026-07-14_optional_normalize_legacy_lucide_icons.sql` | Optional | Select `H20260714-LUCIDE-CLEANUP` only for legacy icon-key cleanup | Transactional; affects product/category icon data only |
| `2026-07-14_product_experience_schema.sql` | Required | Ledgered canonical schema | Requires Products, CartItems, OrderItems and Users |
| `2026-07-14_seed_product_experience_settings.sql` | Required | Ledgered canonical seed | Run after product schema; preserves values |

Historical files are retained byte-for-byte in the manifest. Future schema files use
`V####__description.sql`. Once a script has a ledger row, editing it is forbidden: the
runner stops on any checksum mismatch.

## Script safety matrix

All 12 manifest scripts are valid UTF-8; Persian literals were decoded with strict UTF-8
during automated checks. “New” means after publishing the baseline candidate. “Existing”
means an upgrade from the pre-ledger reference schema.

| Version | Type | Idempotent | Transactional | Safe direct rerun | Backup / downtime | New | Existing |
|---|---|---:|---:|---:|---|---:|---:|
| V0001 | Ledger schema | Yes | Table/index transaction; trigger separate batch | Yes | Backup; seconds, no planned outage | Required | Required |
| V0002 | Corrective constraint patch | Yes | Yes | Yes | Backup + maintenance window; takes schema lock | Required | Required |
| H20260713-SMS-SCHEMA | Schema/index/procedure patch | Object-level | No outer transaction | Only after inspecting partial failures | Backup + maintenance window; table/index locks | Required | Required |
| H20260714-PRODUCT-SCHEMA | Schema/index/seed patch | Yes for known shapes | Yes | Yes on coherent schema | Backup + maintenance window; schema/index locks | Required | Required |
| V0003 | Reference role seed | Yes | Yes | Yes | Backup; seconds, online | Required | Required |
| V0004 | Financial/security schema | Yes | Yes | Yes after duplicate preflight | Backup + rehearsal; index creation may lock busy tables | Required | Required |
| V0005 | SEO/content/redirect schema | Yes | Yes | Yes | Backup + rehearsal; additive columns/indexes | Required | Required |
| H20260708-UI | Settings seed | Yes | No | Yes; preserves values | Backup; seconds, online | Required | Required |
| H20260713-SMS-SEED | Settings seed | Yes | No | Yes; preserves secrets/values | Backup; seconds, online | Required | Required |
| H20260714-PRODUCT-SEED | Settings seed | Yes | No | Yes; preserves values | Backup; seconds, online | Required | Required |
| H20260707-GIFT-LEGACY | Legacy constraint patch | Recreates object | No | Unsafe operationally | Backup + maintenance; superseded | No | No |
| H20260708-DATA-FIX | Narrow data repair | Yes for exact row | No | Yes only on confirmed affected data | Backup; seconds, online | No | Conditional |
| H20260713-OTP-COMPAT | Legacy constraint compatibility | Best-effort | Yes | Conditional; detection has legacy-shape limits | Backup + maintenance; schema lock | No | Conditional |
| H20260714-LUCIDE-CLEANUP | Optional data cleanup | Yes | Yes | Yes | Backup; seconds, online | No | Optional |

Expected durations assume the current small reference database. Table/index operations
scale with production row counts and lock waits; obtain actual timings from a restored
production-size rehearsal rather than treating these estimates as an SLA.

## Per-script verification

The runner performs final aggregate verification. Operators can additionally run these
read-only checks after each stage:

| Version | Verification query / expected result |
|---|---|
| V0001 | `SELECT COUNT(*) FROM sys.tables WHERE object_id=OBJECT_ID(N'dbo.DatabaseScriptHistory');` → `1` |
| V0002 | Query `sys.check_constraints` for trusted, enabled `CK_GiftCodeReservations_Status`; invalid row count for `Status NOT BETWEEN 0 AND 3` → `0` |
| H20260713-SMS-SCHEMA | `OBJECT_ID(N'dbo.SmsMessages',N'U')`, `OBJECT_ID(N'dbo.SmsMessageAttempts',N'U')`, and `OBJECT_ID(N'dbo.usp_PurgeSmsHistory',N'P')` are non-null |
| H20260714-PRODUCT-SCHEMA | `ProductFeatures`, `ProductInputFields`, `CartItemInputValues`, `OrderItemInputValues`, and `FontAssets` all exist |
| V0003 | `SELECT COUNT(*) FROM dbo.Roles WHERE Name IN (N'SuperAdmin',N'Admin',N'Support',N'Customer');` → `4` |
| V0005 | `LegacyRedirects` exists; `Products.FocusKeyword`, `Products.ThumbnailAltText`, and `ProductTags.Aliases` exist; `UX_LegacyRedirects_SourcePath` is unique |
| H20260708-UI | `HeaderLogoPath` and `FaviconPath` each exist exactly once in `dbo.Settings` |
| H20260713-SMS-SEED | `Sms.OtpTemplateId` and `Sms.NotificationTemplateId` each exist exactly once; configured values remain unchanged |
| H20260714-PRODUCT-SEED | `Typography.FontFamily`, `Branding.AssetVersion`, and `TrustSeal.Enamad.Enabled` each exist exactly once |

## New environment

1. Provision SQL Server 2019+ and an empty application database name. Never target a
   system database.
2. Verify `Baseline/VitorizeDb.schema-candidate.dacpac` against its `.sha256` file.
3. Review `MODEL-SCRIPT-MISMATCHES.md`. The DACPAC is a schema-only candidate extracted
   from the local reference database; it contains no table data or secrets.
4. Publish the DACPAC with SqlPackage using `BlockOnPossibleDataLoss=True`.
5. Run the canonical PowerShell runner. It seeds non-secret reference records and writes
   all eight ledger entries.
6. Configure application secrets outside the database scripts, then bootstrap the first
   SuperAdmin through the separately documented one-time secret-based flow.

Example DACPAC publish:

```powershell
SqlPackage /Action:Publish `
  /SourceFile:Database\Baseline\VitorizeDb.schema-candidate.dacpac `
  /TargetServerName:<server> /TargetDatabaseName:<database> `
  /p:BlockOnPossibleDataLoss=True /p:DropObjectsNotInSource=False
```

## Existing environment

1. Take and test a full backup. Record database name, SQL version, compatibility and
   collation. Schedule a maintenance window: V0002 takes a schema modification lock and
   table/index creation may block writers.
2. Run `Preflight/validate_database_state.sql` read-only and resolve every `ERROR`.
3. Run a dry run with the exact target.
4. Execute with an exact `-ConfirmDatabaseName` value.
5. Archive the runner log and execute `PostDeploy/verify_database_deployment.sql` again
   from the deployment account if independent evidence is required.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Database\Deploy-Database.ps1 `
  -ServerInstance <server> -Database <database> -Environment Production -DryRun

powershell -NoProfile -ExecutionPolicy Bypass -File Database\Deploy-Database.ps1 `
  -ServerInstance <server> -Database <database> -Environment Production `
  -ConfirmDatabaseName <database>
```

The runner uses Windows integrated authentication only. It accepts no username or
password, verifies every manifest checksum before connecting, refuses system database
names, requires explicit confirmation for writes, enables `QUOTED_IDENTIFIER`, stops on
SQL errors, records only successful scripts, and runs read-only post-verification.

Optional/environment-specific scripts require an exact opt-in, for example:

```powershell
... -AdditionalScriptVersion H20260714-LUCIDE-CLEANUP
```

Never select `H20260708-DATA-FIX` or `H20260713-OTP-COMPAT` based only on the filename;
first confirm the affected data/constraint reported by preflight.

Required permissions are: connect to the target database; read catalog/data for
preflight; create/alter tables, constraints, indexes, procedures and triggers; and
insert non-secret reference rows plus ledger entries. Grant these to a time-bounded
deployment principal rather than the application runtime principal. DACPAC publication
also requires database creation/DDL rights appropriate to the target.

## Ledger semantics

`dbo.DatabaseScriptHistory` records script name, version, lowercase SHA-256 hash, UTC
application time, SQL login, environment, success and notes. Only successful rows are
allowed. A trigger rejects update/delete. A same-name or same-version row with another
hash is an immutable-history failure; it is not overwritten or silently skipped.

The baseline DACPAC includes the empty ledger schema. It intentionally contains no
ledger data, so the runner records a fresh history for every new database.

## Rollback and failure recovery

- Database backup/restore is the authoritative rollback for schema releases.
- Do not delete ledger rows to force reruns.
- Required seed scripts only insert missing defaults, so rollback normally leaves benign
  reference rows; restore if exact pre-release state is required.
- V0002 is atomic. If validation fails, correct the conflicting data/constraint manually
  and rerun; do not weaken the constraint.
- SMS schema lacks one outer transaction. If it stops mid-file, retain the failure log,
  inspect created objects, and rerun only after comparison with the manifest/reference
  schema. The runner will not record it until the whole file succeeds.
- Product schema is transactional; SQL Server rolls it back on error.

## Validation evidence

The chain was tested on disposable databases for: schema-only baseline publish, full
canonical deployment, recovery after an interrupted unledgered schema script, clean
second-run skips, checksum tamper rejection, V0002 conflict rejection/rollback, and
post-deploy verification. The source application database was only queried.

Run the repository asset checks with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Database\Tests\Test-DatabaseDeploymentAssets.ps1
```

All SQL files are UTF-8. Use `sqlcmd -f 65001` if a script must be inspected manually.

## Troubleshooting and production checklist

- **Checksum mismatch:** stop. Restore the committed immutable file or create a new
  versioned corrective script; never edit the ledger.
- **Preflight core object error:** publish/reconcile the reviewed baseline before patches.
- **Known legacy user warning/error:** verify ownership, disable/remove unsafe accounts
  through the approved account process, revoke refresh tokens, then rerun verification.
- **Constraint conflict:** export the offending rows/definition, correct them explicitly,
  and rerun. Do not use `NOCHECK` as a workaround.
- **Partial SMS schema:** compare the failed scratch/target schema with the baseline and
  create a new corrective script if object shapes differ.
- **Persian mojibake:** stop before committing data; confirm UTF-8 file bytes and use
  `sqlcmd -f 65001` plus Unicode `N''` literals.

Production release is ready only after: tested backup/restore; preflight has no errors;
target name and environment are independently reviewed; secrets are supplied through a
secret provider; dry run checksums pass; maintenance/rollback owners are present; runner
and post-deploy verification pass; logs are archived; the application smoke test passes;
and the deployment principal is revoked or reduced afterward.
