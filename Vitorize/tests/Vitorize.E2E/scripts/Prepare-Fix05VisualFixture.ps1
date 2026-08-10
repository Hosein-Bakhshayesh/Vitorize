[CmdletBinding()]
param(
    [string] $ServerInstance = '.',
    [Parameter(Mandatory = $true)] [string] $Database
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($Database -notmatch '^Vitorize_[A-Za-z0-9_]+$') { throw 'Database must be a disposable Vitorize_ database.' }

$fixture = Join-Path $PSScriptRoot '..\fixtures\seed-fix05-visual.sql'
& sqlcmd -S $ServerInstance -d $Database -E -C -b -f 65001 -i $fixture
if ($LASTEXITCODE -ne 0) { throw 'FIX-05 visual fixture preparation failed.' }
Write-Host "Prepared FIX-05 Testing-only visual fixture in $Database."
