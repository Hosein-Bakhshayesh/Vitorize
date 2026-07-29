# Database production rehearsal and approval pack

This pack turns the existing immutable deployment chain into an auditable release rehearsal. It does **not** grant DBA approval and must be run by an approved operator against a production-like SQL Server.

## Required evidence before applying a production upgrade

1. Record the reviewed Git commit and the SHA-256 in `Baseline/VitorizeDb.schema-candidate.dacpac.sha256`.
2. Capture a successful `Database/Tests/Test-DatabaseDeploymentAssets.ps1` result.
3. Capture a tested backup identifier and restore location; do not rely on this rehearsal as a backup.
4. Run the read-only existing-database preflight:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Database\Rehearsal\Upgrade-ExistingDatabase.ps1 `
  -ServerInstance <sql-server> -Database <production-like-db> -Environment Staging
```

Expected output includes `Running read-only preflight`, a manifest checksum count, and `Dry run completed. No scripts or ledger rows were changed.` Any checksum mismatch, preflight ERROR, or ledger conflict is a stop condition.

## Clean and upgrade rehearsal

Run only on a disposable non-production SQL Server. The script generates a GUID-suffixed database and only cleans up that database.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Database\Rehearsal\Test-DatabaseUpgradeRehearsal.ps1 `
  -ServerInstance <sql-server> -Environment Staging -KeepDatabase
```

Expected completion: `REHEARSAL PASSED`, followed by `Vitorize database deployment verification passed.` Retain the SQL logs and, when `-KeepDatabase` is used, let the DBA inspect the schema before removing the named rehearsal database.

## Production upgrade and recovery

The release owner must approve a maintenance window, a tested backup, and this checklist before applying:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Database\Rehearsal\Upgrade-ExistingDatabase.ps1 `
  -ServerInstance <sql-server> -Database <production-db> -Environment Production -Apply `
  -ConfirmDatabaseName <production-db>
```

If preflight, publish, script application, or post-deployment verification fails: stop application traffic, preserve the runner log, do not hand-edit `DatabaseScriptHistory`, and restore the tested backup into an isolated database first. Validate that restore with the post-deployment verifier and application smoke test. A DBA decides whether to restore production or proceed with a forward-only corrective versioned script; historical scripts remain immutable.

## Release owner / DBA sign-off

| Field | Evidence |
|---|---|
| Release commit and baseline SHA-256 | |
| Production-like rehearsal database and timestamp | |
| Clean publish result | |
| Existing upgrade dry run and post-deployment result | |
| Backup ID, recovery model, RPO/RTO | |
| Lock/downtime assessment | |
| Rollback/restore owner and communication channel | |
| DBA name, date, approval | |
| Release owner name, date, approval | |

Blank fields are an explicit no-go. The candidate DACPAC remains a candidate until the last two sign-offs are recorded.
