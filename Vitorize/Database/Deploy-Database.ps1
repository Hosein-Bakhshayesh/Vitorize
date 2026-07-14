[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $ServerInstance,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Database,

    [Parameter(Mandatory = $true)]
    [ValidateSet('Development', 'Staging', 'Production')]
    [string] $Environment,

    [switch] $DryRun,

    [string] $ConfirmDatabaseName,

    [string[]] $AdditionalScriptVersion = @(),

    [string] $LogDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($LogDirectory)) {
    $LogDirectory = Join-Path $PSScriptRoot 'Logs'
}

function ConvertTo-SqlLiteral([string] $Value) {
    return "N'" + $Value.Replace("'", "''") + "'"
}

function Get-CanonicalScriptHash([string] $Path) {
    $strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
    $text = $strictUtf8.GetString([System.IO.File]::ReadAllBytes($Path))
    $normalized = $text.Replace("`r`n", "`n").Replace("`r", "`n")
    $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($normalized)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join '') }
    finally { $sha.Dispose() }
}

function Resolve-SqlCmd {
    $command = Get-Command sqlcmd -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $known = 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE'
    if (Test-Path -LiteralPath $known) { return $known }
    throw 'sqlcmd was not found. Install Microsoft SQL Server command-line utilities.'
}

if ($Database -notmatch '^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$') {
    throw 'Database contains unsupported characters. Use an explicit SQL identifier containing letters, digits, underscore, dot, or hyphen.'
}
if ($ServerInstance -match "[`r`n;]") { throw 'ServerInstance contains an unsafe character.' }
if ($Database.ToLowerInvariant() -in @('master', 'model', 'msdb', 'tempdb')) {
    throw "Refusing to deploy to SQL Server system database '$Database'."
}
if (-not $DryRun -and $ConfirmDatabaseName -cne $Database) {
    throw 'Execution requires -ConfirmDatabaseName with an exact, case-sensitive match. Use -DryRun for inspection.'
}

$manifestPath = Join-Path $PSScriptRoot 'deployment-manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding utf8 | ConvertFrom-Json
$allScripts = @($manifest.scripts | Sort-Object order)

if ($AdditionalScriptVersion.Count -gt 0) {
    $unknown = @($AdditionalScriptVersion | Where-Object { $_ -notin $allScripts.version })
    if ($unknown.Count -gt 0) { throw "Unknown additional script version(s): $($unknown -join ', ')" }
    $invalid = @($allScripts | Where-Object {
        $_.version -in $AdditionalScriptVersion -and $_.classification -notin @('optional', 'environment-specific')
    })
    if ($invalid.Count -gt 0) { throw 'Only optional or environment-specific scripts may be selected with -AdditionalScriptVersion.' }
}

$selected = @($allScripts | Where-Object {
    ($_.classification -eq 'required' -or $_.version -in $AdditionalScriptVersion) -and
    $Environment -in $_.environments
})

foreach ($script in $allScripts) {
    $path = Join-Path $PSScriptRoot $script.path
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Manifest file is missing: $($script.path)" }
    $actualHash = Get-CanonicalScriptHash $path
    if ($actualHash -cne $script.sha256) {
        throw "Checksum mismatch for $($script.name). Expected $($script.sha256), found $actualHash. Historical scripts are immutable."
    }
}

$sqlcmd = Resolve-SqlCmd
New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$safeDatabase = $Database -replace '[^A-Za-z0-9_.-]', '_'
$logPath = Join-Path $LogDirectory "$timestamp-$safeDatabase-$Environment.log"

function Write-DeploymentLog([string] $Message) {
    $line = "[$([DateTime]::UtcNow.ToString('o'))] $Message"
    Write-Host $line
    Add-Content -LiteralPath $logPath -Value $line -Encoding utf8
}

function Invoke-SqlQuery([string] $Query, [switch] $Capture) {
    $arguments = @('-S', $ServerInstance, '-d', $Database, '-E', '-b', '-I', '-f', '65001', '-W', '-h', '-1', '-Q', $Query)
    if ($Capture) {
        $oldPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try { $result = & $sqlcmd @arguments 2>&1; $exitCode = $LASTEXITCODE }
        finally { $ErrorActionPreference = $oldPreference }
        if ($exitCode -ne 0) { throw ($result -join [Environment]::NewLine) }
        return ($result | ForEach-Object { $_.ToString().Trim() } | Where-Object { $_ })
    }
    $oldPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $sqlcmd @arguments 2>&1 | Tee-Object -FilePath $logPath -Append; $exitCode = $LASTEXITCODE }
    finally { $ErrorActionPreference = $oldPreference }
    if ($exitCode -ne 0) { throw 'sqlcmd query failed. Review the deployment log.' }
}

function Invoke-SqlFile([string] $Path) {
    $arguments = @('-S', $ServerInstance, '-d', $Database, '-E', '-b', '-I', '-f', '65001', '-i', $Path)
    $oldPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $sqlcmd @arguments 2>&1 | Tee-Object -FilePath $logPath -Append; $exitCode = $LASTEXITCODE }
    finally { $ErrorActionPreference = $oldPreference }
    if ($exitCode -ne 0) { throw "SQL script failed: $Path. Review the deployment log." }
}

$databaseExists = Invoke-SqlQuery -Capture -Query "SET NOCOUNT ON; SELECT CASE WHEN DB_ID($(ConvertTo-SqlLiteral $Database)) IS NULL THEN '0' ELSE '1' END;"
if (($databaseExists | Select-Object -Last 1) -ne '1') { throw "Database '$Database' does not exist on '$ServerInstance'." }

Write-DeploymentLog "Target server=$ServerInstance database=$Database environment=$Environment dryRun=$($DryRun.IsPresent). Integrated authentication only."
Write-DeploymentLog "Manifest=$manifestPath selectedScripts=$($selected.Count)."

$preflightPath = Join-Path $PSScriptRoot $manifest.preflight
Write-DeploymentLog 'Running read-only preflight.'
Invoke-SqlFile $preflightPath

foreach ($script in $selected) {
    $scriptPath = Join-Path $PSScriptRoot $script.path
    $ledgerState = Invoke-SqlQuery -Capture -Query @"
SET NOCOUNT ON;
IF OBJECT_ID(N'dbo.DatabaseScriptHistory', N'U') IS NULL
    SELECT 'LEDGER_MISSING';
ELSE
    SELECT CASE
        WHEN EXISTS
        (
            SELECT 1 FROM dbo.DatabaseScriptHistory
            WHERE ScriptName = $(ConvertTo-SqlLiteral $script.name)
              AND ScriptVersion = $(ConvertTo-SqlLiteral $script.version)
              AND ScriptHash = '$($script.sha256)'
        ) THEN '$($script.sha256)'
        WHEN EXISTS
        (
            SELECT 1 FROM dbo.DatabaseScriptHistory
            WHERE ScriptName = $(ConvertTo-SqlLiteral $script.name)
               OR ScriptVersion = $(ConvertTo-SqlLiteral $script.version)
        ) THEN 'HISTORY_CONFLICT'
        ELSE 'NOT_APPLIED'
    END;
"@
    $ledgerValue = $ledgerState | Select-Object -Last 1

    if ($ledgerValue -eq $script.sha256) {
        Write-DeploymentLog "SKIP $($script.version) $($script.name): already applied with matching checksum."
        continue
    }
    if ($ledgerValue -notin @('LEDGER_MISSING', 'NOT_APPLIED')) {
        throw "Immutable history mismatch for $($script.version) $($script.name): ledger hash $ledgerValue, manifest hash $($script.sha256)."
    }

    if ($DryRun) {
        Write-DeploymentLog "PENDING $($script.version) $($script.name) [$($script.classification)] checksum=$($script.sha256)."
        continue
    }

    if (-not $PSCmdlet.ShouldProcess("$ServerInstance/$Database", "Apply $($script.version) $($script.name)")) { continue }
    Write-DeploymentLog "APPLY $($script.version) $($script.name) checksum=$($script.sha256)."
    Invoke-SqlFile $scriptPath

    $notes = if ($script.classification -eq 'required') { 'Canonical deployment chain' } else { "Explicit $($script.classification) selection" }
    $insertHistory = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.DatabaseScriptHistory', N'U') IS NULL
    THROW 51200, 'Deployment ledger was not created by V0001.', 1;
IF EXISTS (SELECT 1 FROM dbo.DatabaseScriptHistory WHERE ScriptName = $(ConvertTo-SqlLiteral $script.name) OR ScriptVersion = $(ConvertTo-SqlLiteral $script.version))
    THROW 51201, 'A deployment history row appeared concurrently; aborting.', 1;
INSERT dbo.DatabaseScriptHistory (ScriptName, ScriptVersion, ScriptHash, Environment, Success, Notes)
VALUES ($(ConvertTo-SqlLiteral $script.name), $(ConvertTo-SqlLiteral $script.version), '$($script.sha256)', $(ConvertTo-SqlLiteral $Environment), 1, $(ConvertTo-SqlLiteral $notes));
"@
    Invoke-SqlQuery -Query $insertHistory
    Write-DeploymentLog "RECORDED $($script.version) in immutable deployment history."
}

if (-not $DryRun) {
    Write-DeploymentLog 'Running read-only post-deployment verification.'
    Invoke-SqlFile (Join-Path $PSScriptRoot $manifest.postDeploy)
    Write-DeploymentLog 'Deployment and verification completed successfully.'
} else {
    Write-DeploymentLog 'Dry run completed. No scripts or ledger rows were changed.'
}

Write-Host "Deployment log: $logPath"
