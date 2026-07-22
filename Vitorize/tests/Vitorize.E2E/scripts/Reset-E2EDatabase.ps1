<#
.SYNOPSIS
    Deterministic PRISTINE reset of the isolated E2E database.

.DESCRIPTION
    Drops and recreates the dedicated Testing database from the checked-in schema DACPAC and the
    versioned post-deploy scripts - the same deployment the SQL integration tests use. After this the
    database is in a known, reproducible state, so data-driven pages (dashboard metrics) and visual
    baselines are deterministic. The bootstrap admin is created when the stack next starts; the E2E
    fixture (Prepare-E2EDatabase.ps1) then seeds the deterministic catalog + users on top.

    Refuses to run against anything that is not a dedicated Vitorize_* Testing database, and never
    against the Development database (VitorizeDb) or Production.
#>
[CmdletBinding()]
param(
    [string] $ServerInstance = '.',
    [string] $Database = 'Vitorize_Phase3_Verification'
)

$ErrorActionPreference = 'Stop'

if ($Database -notmatch '^Vitorize_[A-Za-z0-9_]+$') {
    throw "Refusing to reset '$Database': the E2E database name must start with 'Vitorize_' and contain only letters/digits/underscore."
}
if ($Database -ieq 'VitorizeDb') { throw 'Refusing to reset the Development database (VitorizeDb).' }

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$dacpac = (Resolve-Path (Join-Path $repoRoot 'Database\Baseline\VitorizeDb.schema-candidate.dacpac')).Path
$deploy = (Resolve-Path (Join-Path $repoRoot 'Database\Deploy-Database.ps1')).Path
$logDir = Join-Path $PSScriptRoot '..\artifacts\db-reset'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$conn = "Server=$ServerInstance;Database=$Database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"

Write-Host "-- Dropping $Database for a pristine reset --" -ForegroundColor Yellow
& sqlcmd -S $ServerInstance -d master -E -C -b -Q "IF DB_ID(N'$Database') IS NOT NULL BEGIN ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Database]; END"
if ($LASTEXITCODE -ne 0) { throw 'Failed to drop the E2E database.' }

Write-Host "-- Publishing schema DACPAC --" -ForegroundColor Yellow
& sqlpackage /Action:Publish "/SourceFile:$dacpac" "/TargetConnectionString:$conn" /p:BlockOnPossibleDataLoss=False /p:DropObjectsNotInSource=False /Quiet:True
if ($LASTEXITCODE -ne 0) { throw 'DACPAC publish failed.' }

Write-Host "-- Running versioned post-deploy scripts --" -ForegroundColor Yellow
& $deploy -ServerInstance $ServerInstance -Database $Database -Environment Development -ConfirmDatabaseName $Database -LogDirectory $logDir

Write-Host "Pristine E2E database recreated: $Database" -ForegroundColor Green
