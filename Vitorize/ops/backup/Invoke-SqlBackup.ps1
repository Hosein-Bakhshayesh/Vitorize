[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string] $ServerInstance,
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string] $Database,
    [Parameter(Mandatory = $true)][ValidateSet('Full', 'Differential', 'Log')][string] $BackupType,
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string] $BackupDirectory,
    [string] $EncryptionCertificateName,
    [string] $LogDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Database -notmatch '^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$' -or $ServerInstance -match "[`r`n;]") { throw 'Unsafe server or database identifier.' }
if ($Database.ToLowerInvariant() -in @('master', 'model', 'msdb', 'tempdb')) { throw 'System databases cannot be backed up by this release script.' }
if ($EncryptionCertificateName -and $EncryptionCertificateName -notmatch '^[A-Za-z_][A-Za-z0-9_]{0,127}$') { throw 'EncryptionCertificateName must be a SQL identifier.' }
$sqlcmd = (Get-Command sqlcmd -ErrorAction SilentlyContinue).Source
if (-not $sqlcmd) { throw 'sqlcmd was not found.' }

$resolvedBackupDirectory = [IO.Path]::GetFullPath($BackupDirectory)
New-Item -ItemType Directory -Path $resolvedBackupDirectory -Force | Out-Null
if ([string]::IsNullOrWhiteSpace($LogDirectory)) { $LogDirectory = Join-Path $PSScriptRoot 'Logs' }
New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$extension = switch ($BackupType) { 'Full' { 'bak' } 'Differential' { 'dif' } 'Log' { 'trn' } }
$backupFile = Join-Path $resolvedBackupDirectory ("{0}-{1}-{2}.{3}" -f $Database, $BackupType, $stamp, $extension)
if (Test-Path -LiteralPath $backupFile) { throw "Refusing to overwrite backup file: $backupFile" }

$kind = switch ($BackupType) { 'Full' { 'DATABASE' } 'Differential' { 'DATABASE' } 'Log' { 'LOG' } }
$options = @('CHECKSUM', 'COMPRESSION', 'STATS = 10', 'NOINIT', 'COPY_ONLY')
if ($BackupType -eq 'Differential') { $options += 'DIFFERENTIAL'; $options = $options | Where-Object { $_ -ne 'COPY_ONLY' } }
if ($BackupType -eq 'Log') { $options = $options | Where-Object { $_ -ne 'COPY_ONLY' } }
if ($EncryptionCertificateName) { $options += "ENCRYPTION (ALGORITHM = AES_256, SERVER CERTIFICATE = [$EncryptionCertificateName])" }
$escapedPath = $backupFile.Replace("'", "''")
$query = "BACKUP $kind [$Database] TO DISK = N'$escapedPath' WITH $($options -join ', '); RESTORE VERIFYONLY FROM DISK = N'$escapedPath' WITH CHECKSUM;"

& $sqlcmd -S $ServerInstance -d master -E -b -I -Q $query 2>&1 | Tee-Object -FilePath (Join-Path $LogDirectory "backup-$stamp.log")
if ($LASTEXITCODE -ne 0) { throw 'Backup or VERIFYONLY failed. Treat this as an alert condition and preserve the log.' }
$hash = (Get-FileHash -LiteralPath $backupFile -Algorithm SHA256).Hash.ToLowerInvariant()
[pscustomobject]@{ BackupFile = $backupFile; Sha256 = $hash; BackupType = $BackupType; VerifiedAtUtc = [DateTime]::UtcNow } |
    ConvertTo-Json | Set-Content -LiteralPath "$backupFile.sha256.json" -Encoding utf8
Write-Host "BACKUP VERIFIED: $backupFile SHA256=$hash"
