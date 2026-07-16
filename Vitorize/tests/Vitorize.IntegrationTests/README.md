# Vitorize integration tests

This project contains HTTP/API and SQL Server integration tests only. It does not contain
unit, Playwright, load, or CI tests.

## Database isolation

Every test run creates a database named `VitorizeIntegration_<process>_<random>` and
drops it during fixture disposal. The fixture never reads the application's committed
development connection string and never accepts a database name from the environment.

The database is built through the same Database-First deployment chain used by a new
environment:

1. publish `Database/Baseline/VitorizeDb.schema-candidate.dacpac` with SqlPackage;
2. run `Database/Deploy-Database.ps1` and its immutable manifest;
3. let the API reference-data seeder run in the `Testing` host environment;
4. run the Phase 2 and Phase 3 SQL verification suites from the tests;
5. drop the disposable database.

The DACPAC targets SQL Server 2022. Docker is not required. By default the fixture uses
the local default SQL Server instance (`.`). Set `VITORIZE_INTEGRATION_SQL_SERVER` to an
isolated SQL Server 2022+ instance when needed. The generated database name cannot be
overridden. Integrated authentication, encryption, and certificate trust are used.

The deployment runner calls this scratch deployment `Development` because the immutable
manifest intentionally recognizes only deployment environments. The application itself
runs as `Testing`; bootstrap admin, development demo users, Seq connectivity, and hosted
workers are disabled. Required JWT/encryption values are ephemeral process environment
variables and are restored at fixture teardown.

## External providers

`WebApplicationFactory<Program>` replaces SMS.ir and Zarinpal adapters with deterministic
in-process fakes. All controllers, application/infrastructure services, EF Core mappings,
middleware, authentication, validation, and SQL Server behavior remain real. Background
workers are removed from the HTTP host so tests invoke no external network service and do
not race test assertions.

## Covered integration boundaries

The suite covers authentication and OTP persistence, admin catalog CRUD, cart/checkout
repricing, coupon/wallet/gift-code concurrency, payment callbacks, refunds and compensation,
instant and manual delivery, order ownership/history, tickets, reviews, KYC, SMS history and
legacy payload compatibility, outbox retry/dead-letter/lease recovery/heartbeat behavior,
SEO sitemap/redirect persistence, upload isolation, security headers/authorization, and SQL
Server foreign keys, checks, precision, delete behavior, deployment scripts, and uniqueness.

## Run

```powershell
dotnet restore tests\Vitorize.IntegrationTests\Vitorize.IntegrationTests.csproj
dotnet test tests\Vitorize.IntegrationTests\Vitorize.IntegrationTests.csproj -c Release
```

Prerequisites: .NET 8 SDK/runtime, SQL Server 2022+, `sqlpackage`, `sqlcmd`, PowerShell,
and permission to create/drop databases on the isolated test instance.

The suite is serialized within one disposable database. Individual scenarios use unique
keys and public references. Parallel tasks are used inside the wallet, coupon, and gift
reservation race tests to exercise real locking and unique constraints.
