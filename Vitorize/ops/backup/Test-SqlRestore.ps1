[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string] $ServerInstance,
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string] $BackupFile,
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string] $RestoreDatabase,
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string] $DataFilePath,
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string] $LogFilePath,
    [string] $ExpectedSha256,
    [string] $ApplicationHealthUrl
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($RestoreDatabase -notmatch '^VitorizeRestore_[A-Za-z0-9_]{1,100}$') { throw 'RestoreDatabase must be a newly-created VitorizeRestore_* verification database.' }
if (-not (Test-Path -LiteralPath $BackupFile -PathType Leaf)) { throw "Backup file not found: $BackupFile" }
if ($ExpectedSha256) {
    $actualHash = (Get-FileHash -LiteralPath $BackupFile -Algorithm SHA256).Hash
    if ($actualHash -cne $ExpectedSha256) { throw 'Backup checksum mismatch; do not restore this file.' }
}
$sqlcmd = (Get-Command sqlcmd -ErrorAction SilentlyContinue).Source
if (-not $sqlcmd) { throw 'sqlcmd was not found.' }
$exists = & $sqlcmd -S $ServerInstance -d master -E -b -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'$RestoreDatabase') IS NULL THEN 0 ELSE 1 END;"
if ($LASTEXITCODE -ne 0 -or ((@($exists | ForEach-Object { $_.ToString().Trim() } | Where-Object { $_ }) | Select-Object -Last 1) -ne '0')) { throw 'Restore target already exists or could not be inspected; refusing to overwrite any database.' }

# Logical names are read before restore and supplied explicitly so the DBA can review them; automatic
# replacement is deliberately not supported.
$logicalDataName = Read-Host 'Logical data file name from RESTORE FILELISTONLY'
$logicalLogName = Read-Host 'Logical log file name from RESTORE FILELISTONLY'
if ([string]::IsNullOrWhiteSpace($logicalDataName) -or [string]::IsNullOrWhiteSpace($logicalLogName)) { throw 'Both logical file names are required.' }
$backupLiteral = $BackupFile.Replace("'", "''"); $dataLiteral = $DataFilePath.Replace("'", "''"); $logLiteral = $LogFilePath.Replace("'", "''")
$restore = "RESTORE DATABASE [$RestoreDatabase] FROM DISK = N'$backupLiteral' WITH MOVE N'$($logicalDataName.Replace("'", "''"))' TO N'$dataLiteral', MOVE N'$($logicalLogName.Replace("'", "''"))' TO N'$logLiteral', CHECKSUM, RECOVERY, STATS = 10;"
& $sqlcmd -S $ServerInstance -d master -E -b -I -Q $restore
if ($LASTEXITCODE -ne 0) { throw 'Restore failed. The source backup is unchanged; preserve SQL output for DBA recovery.' }

& $sqlcmd -S $ServerInstance -d $RestoreDatabase -E -b -I -Q 'DBCC CHECKDB WITH NO_INFOMSGS;'
if ($LASTEXITCODE -ne 0) { throw 'DBCC CHECKDB failed on restored verification database.' }
if ($ApplicationHealthUrl) {
    $health = Invoke-WebRequest -UseBasicParsing -Uri $ApplicationHealthUrl -TimeoutSec 30
    if ($health.StatusCode -ne 200) { throw "Application smoke test returned HTTP $($health.StatusCode)." }
}
Write-Host "RESTORE VERIFIED: database=$RestoreDatabase. The target was newly created and has not replaced production."
