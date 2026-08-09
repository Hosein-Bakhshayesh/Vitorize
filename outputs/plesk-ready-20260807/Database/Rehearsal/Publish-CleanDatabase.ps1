[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string] $ServerInstance,
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string] $Database,
    [Parameter(Mandatory = $true)][ValidateSet('Development', 'Staging', 'Production')][string] $Environment,
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string] $ConfirmDatabaseName,
    [string] $DacpacPath,
    [string] $LogDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Parameter default expressions run before $PSScriptRoot is populated when the
# script is invoked with -File. Resolve the documented default only after the
# script context exists, so a release operator can use the command as written.
if ([string]::IsNullOrWhiteSpace($DacpacPath)) {
    $DacpacPath = Join-Path $PSScriptRoot '..\Baseline\VitorizeDb.schema-candidate.dacpac'
}

if ($Database -notmatch '^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$' -or $ServerInstance -match "[`r`n;]") {
    throw 'The target database or server instance contains unsupported characters.'
}
if ($Database.ToLowerInvariant() -in @('master', 'model', 'msdb', 'tempdb')) { throw 'System databases are never valid targets.' }
if ($ConfirmDatabaseName -cne $Database) { throw 'ConfirmDatabaseName must exactly match Database.' }
if (-not (Test-Path -LiteralPath $DacpacPath -PathType Leaf)) { throw "DACPAC not found: $DacpacPath" }

$sqlpackage = (Get-Command sqlpackage -ErrorAction SilentlyContinue).Source
if (-not $sqlpackage) { throw 'sqlpackage was not found. Install the SQL Server DACPAC tooling.' }
$sqlcmd = (Get-Command sqlcmd -ErrorAction SilentlyContinue).Source
if (-not $sqlcmd) { throw 'sqlcmd was not found. Install SQL Server command-line utilities.' }
if ([string]::IsNullOrWhiteSpace($LogDirectory)) { $LogDirectory = Join-Path $PSScriptRoot '..\Logs' }
New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null
$logPath = Join-Path $LogDirectory ("{0}-clean-publish-{1}.log" -f (Get-Date -Format 'yyyyMMdd-HHmmss'), $Database)

# This script deliberately does not drop or overwrite an existing database. It is for a clean,
# production-like target selected by a release operator; existing-database upgrades use the separate wrapper.
$escapedDatabase = $Database.Replace("'", "''")
$exists = & $sqlcmd -S $ServerInstance -d master -E -b -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'$escapedDatabase') IS NULL THEN 0 ELSE 1 END;"
if ($LASTEXITCODE -ne 0) { throw 'Unable to determine whether the clean database target already exists.' }
if ((@($exists | ForEach-Object { $_.ToString().Trim() } | Where-Object { $_ }) | Select-Object -Last 1) -ne '0') {
    throw "Refusing to publish a clean database over existing target '$Database'. Use Upgrade-ExistingDatabase.ps1 instead."
}
$connection = "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True"
$arguments = @(
    '/Action:Publish', "/SourceFile:$DacpacPath", "/TargetConnectionString:$connection",
    '/p:BlockOnPossibleDataLoss=True', '/p:DropObjectsNotInSource=False', '/p:CreateNewDatabase=True',
    "/Profile:$([System.IO.Path]::GetTempFileName())"
)
# sqlpackage does not need a publish profile here; remove the temporary placeholder before execution.
$arguments = $arguments | Where-Object { $_ -notlike '/Profile:*' }

Write-Host "Publishing reviewed DACPAC to clean target $ServerInstance/$Database. Environment=$Environment."
if ($PSCmdlet.ShouldProcess("$ServerInstance/$Database", 'Publish clean database DACPAC')) {
    & $sqlpackage @arguments 2>&1 | Tee-Object -FilePath $logPath
    if ($LASTEXITCODE -ne 0) { throw "sqlpackage publish failed. Review $logPath" }
}

Write-Host "Clean publish completed. Next run Database\Deploy-Database.ps1 with -ConfirmDatabaseName $Database. Log: $logPath"
