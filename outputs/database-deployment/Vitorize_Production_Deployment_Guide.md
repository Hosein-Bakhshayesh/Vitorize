# Vitorize production database deployment

The canonical configured database name is `VitorizeDb`; SQL Server 2022+ is required. Take/verify a backup or snapshot policy, create a deployment login with `CREATE DATABASE` at server scope and `db_owner` only for the one-time schema deployment, then use a separate runtime login with `db_datareader`, `db_datawriter`, and `EXECUTE` in `VitorizeDb`. Do not run the application as sysadmin.

In SSMS enable **Query > SQLCMD Mode**, open `Vitorize_Production_Full.sql`, change the SQLCMD `DatabaseName` only if necessary, and execute it from the deployment folder. Then run verification again. Configure the production connection string, JWT/encryption keys, payment/SMS credentials, public origins, storage, and Data Protection keys outside source control. Publish API/Web, check `/api/health/live` then `/api/health/ready`.

No administrator is created in SQL. For exactly one API startup, supply `BootstrapAdmin__Enabled=true`, `BootstrapAdmin__Mobile`, `BootstrapAdmin__Password` (12-72 UTF-8 bytes), and `BootstrapAdmin__FullName` through the secret provider. Verify the `BootstrapSuperAdminCreated` security event and login, then remove all BootstrapAdmin values and restart. Record verification output, readiness result, and a post-deployment backup.
