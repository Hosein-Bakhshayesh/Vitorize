[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string] $ServerInstance,
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string] $Database,
    [Parameter(Mandatory = $true)][ValidateSet('Development', 'Staging', 'Production')][string] $Environment,
    [switch] $Apply,
    [string] $ConfirmDatabaseName,
    [string] $LogDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $Apply) {
    Write-Host 'Read-only upgrade rehearsal: running immutable checksum validation, preflight, and deployment dry run.'
    & (Join-Path $PSScriptRoot '..\Deploy-Database.ps1') -ServerInstance $ServerInstance -Database $Database -Environment $Environment -DryRun -LogDirectory $LogDirectory
    exit $LASTEXITCODE
}

if ($ConfirmDatabaseName -cne $Database) { throw 'Apply requires ConfirmDatabaseName to exactly match Database.' }
Write-Host 'Applying the canonical immutable upgrade chain after preflight. A tested backup and approved change record are prerequisites.'
if ($PSCmdlet.ShouldProcess("$ServerInstance/$Database", 'Apply existing-database upgrade')) {
    & (Join-Path $PSScriptRoot '..\Deploy-Database.ps1') -ServerInstance $ServerInstance -Database $Database -Environment $Environment -ConfirmDatabaseName $ConfirmDatabaseName -LogDirectory $LogDirectory
    exit $LASTEXITCODE
}
