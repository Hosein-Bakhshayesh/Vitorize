[CmdletBinding()]
param(
    [string] $ServerInstance = '.',
    [string] $Database = 'Vitorize_Phase3_Verification'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Database -notmatch '^Vitorize_[A-Za-z0-9_]+$') { throw 'The E2E database name must start with Vitorize_ and contain only letters, digits, or underscore.' }
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$fixture = (Resolve-Path (Join-Path $PSScriptRoot '..\fixtures\seed-e2e.sql')).Path
& sqlcmd -S $ServerInstance -d $Database -E -C -b -f 65001 -i $fixture
if ($LASTEXITCODE -ne 0) { throw 'E2E fixture failed.' }
Write-Host "Prepared deterministic E2E data in $Database."
