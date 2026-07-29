# Backup and restore operational pack

All scripts use integrated SQL authentication, reject system/unknown restore targets, create timestamped output, verify checksums, and return non-zero on failure. A scheduler or monitoring agent must treat any non-zero exit, missing `BACKUP VERIFIED` / `RESTORE VERIFIED` line, or missing manifest as a paging alert.

## Backup policy template

| Asset | Frequency | Retention | Encryption / access | Evidence |
|---|---|---|---|---|
| SQL full backup | `<set RPO>` | `<set retention>` | SQL backup encryption certificate + encrypted immutable storage | `.sha256.json`, `VERIFYONLY` log |
| SQL differential | `<set RPO>` | `<set retention>` | Same as full | `.sha256.json`, `VERIFYONLY` log |
| SQL transaction log (FULL recovery only) | `<set RPO>` | `<set retention>` | Same as full | `.sha256.json`, `VERIFYONLY` log |
| Public media | `<set RPO>` | `<set retention>` | Encrypted object/file storage, least privilege | media manifest |
| Private KYC/documents | `<set RPO>` | `<set retention>` | Separate encrypted restricted storage; never public media | media manifest |

The DBA must confirm recovery model before scheduling differential/log backups. Store the SQL encryption certificate and private-document backups separately from the database backup; a backup without its certificate is not recoverable.

## Operator flow

1. Run `Invoke-SqlBackup.ps1` for the approved database and write to encrypted backup storage. Supply `-EncryptionCertificateName` after the DBA has provisioned a SQL Server backup-encryption certificate.
2. Run `Backup-Media.ps1` with both public media and private-document roots. The script never deletes a source or prior backup.
3. Copy the returned SHA-256 evidence and backup IDs into the release/operations record.
4. At the agreed drill interval, select a newly-created `VitorizeRestore_*` database and isolated data/log paths. Run `RESTORE FILELISTONLY` as the DBA, then run `Test-SqlRestore.ps1` with the reviewed logical names and backup hash. It refuses to overwrite a database.
5. Point an isolated API instance at the restore database and pass its authenticated-safe health endpoint with `-ApplicationHealthUrl`. Compare restored public/private media with `media-manifest.sha256.json` before allowing the instance any egress.

## Restore drill sign-off

| Field | Evidence |
|---|---|
| Backup ID, SHA-256, encryption certificate escrow reference | |
| Restore target and isolated network confirmation | |
| `RESTORE VERIFYONLY` / DBCC CHECKDB result | |
| Restored media manifest match | |
| Application smoke result | |
| Measured RPO / RTO versus target | |
| Backup failure alert recipient and test alert timestamp | |
| DBA/operator name, date, approval | |

No production restore is authorized by this repository. A failed backup/restore drill is an incident: preserve logs, notify the listed owner, and remediate before marking recovery readiness complete.
