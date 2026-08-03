[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$databaseRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $databaseRoot 'deployment-manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding utf8 | ConvertFrom-Json
$scripts = @($manifest.scripts)

function Assert-True([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}

function Get-CanonicalScriptHash([string] $Path) {
    $decoder = New-Object System.Text.UTF8Encoding($false, $true)
    $text = $decoder.GetString([System.IO.File]::ReadAllBytes($Path))
    $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($text.Replace("`r`n", "`n").Replace("`r", "`n"))
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join '') }
    finally { $sha.Dispose() }
}

Assert-True ($manifest.namingConvention -eq 'V####__description.sql') 'Unexpected versioned-script naming convention.'
Assert-True (($scripts.version | Select-Object -Unique).Count -eq $scripts.Count) 'Duplicate manifest version.'
Assert-True (($scripts.name | Select-Object -Unique).Count -eq $scripts.Count) 'Duplicate manifest script name.'
Assert-True (($scripts.order | Select-Object -Unique).Count -eq $scripts.Count) 'Duplicate manifest order.'
Assert-True (($scripts | Where-Object classification -eq 'required').Count -gt 0) 'No required scripts are defined.'

$strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
foreach ($script in $scripts) {
    $path = Join-Path $databaseRoot $script.path
    Assert-True (Test-Path -LiteralPath $path -PathType Leaf) "Missing manifest file: $($script.path)"
    $actual = Get-CanonicalScriptHash $path
    Assert-True ($actual -ceq $script.sha256) "Checksum mismatch: $($script.path)"
    [void]$strictUtf8.GetString([System.IO.File]::ReadAllBytes($path))
}

$trackedSql = @(
    Get-ChildItem -LiteralPath $databaseRoot -File -Filter '*.sql'
    Get-ChildItem -LiteralPath (Join-Path $databaseRoot 'Versioned') -File -Filter '*.sql'
)
foreach ($file in $trackedSql) {
    Assert-True ($file.Name -in $scripts.name) "SQL deployment file is missing from the manifest: $($file.Name)"
}

$baseline = Join-Path $databaseRoot 'Baseline\VitorizeDb.schema-candidate.dacpac'
$baselineHashFile = "$baseline.sha256"
Assert-True (Test-Path -LiteralPath $baseline) 'Schema-only baseline candidate is missing.'
$expectedBaselineHash = ((Get-Content -LiteralPath $baselineHashFile -Raw).Trim() -split '\s+')[0]
$actualBaselineHash = (Get-FileHash -LiteralPath $baseline -Algorithm SHA256).Hash.ToLowerInvariant()
Assert-True ($actualBaselineHash -ceq $expectedBaselineHash) 'Baseline candidate checksum mismatch.'

$preflight = Get-Content -LiteralPath (Join-Path $databaseRoot $manifest.preflight) -Raw -Encoding utf8
$postDeploy = Get-Content -LiteralPath (Join-Path $databaseRoot $manifest.postDeploy) -Raw -Encoding utf8
Assert-True ($preflight -match 'READ ONLY') 'Preflight must be labelled read-only.'
Assert-True ($preflight -match 'THROW 51090') 'Preflight must fail the runner when errors are reported.'
Assert-True ($postDeploy -match 'READ ONLY') 'Post-deployment verification must be labelled read-only.'
Assert-True ($postDeploy -match 'THROW 51100') 'Post-deployment verification must fail on errors.'

$runner = Join-Path $databaseRoot 'Deploy-Database.ps1'
$oldPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $runner -ServerInstance '.' -Database master -Environment Development -DryRun *> $null
    $guardExitCode = $LASTEXITCODE
}
finally { $ErrorActionPreference = $oldPreference }
Assert-True ($guardExitCode -ne 0) 'Runner did not reject a SQL Server system database.'

$rehearsalFiles = @(
    'Rehearsal\Publish-CleanDatabase.ps1',
    'Rehearsal\Upgrade-ExistingDatabase.ps1',
    'Rehearsal\Test-DatabaseUpgradeRehearsal.ps1',
    'Rehearsal\README.md'
)
foreach ($relativePath in $rehearsalFiles) {
    Assert-True (Test-Path -LiteralPath (Join-Path $databaseRoot $relativePath) -PathType Leaf) "Missing database rehearsal asset: $relativePath"
}
$cleanPublish = Get-Content -LiteralPath (Join-Path $databaseRoot 'Rehearsal\Publish-CleanDatabase.ps1') -Raw -Encoding utf8
Assert-True ($cleanPublish -notmatch '(?s)\[string\]\s+\$DacpacPath\s*=\s*\(Join-Path') 'Clean publish script resolves $PSScriptRoot too early in a parameter default.'
Assert-True ($cleanPublish -match 'IsNullOrWhiteSpace\(\$DacpacPath\)') 'Clean publish script lacks deferred default DACPAC resolution.'

$repositoryRoot = Split-Path -Parent $databaseRoot
$apiSettings = Get-Content -LiteralPath (Join-Path $repositoryRoot 'Vitorize.Api\appsettings.json') -Raw -Encoding utf8 | ConvertFrom-Json
Assert-True ($null -eq $apiSettings.Jwt.PSObject.Properties['SecretKey']) 'API appsettings.json must not contain a JWT signing secret.'
Assert-True ($null -eq $apiSettings.Encryption.PSObject.Properties['Key']) 'API appsettings.json must not contain an encryption key.'
$rehearsalReadme = Get-Content -LiteralPath (Join-Path $databaseRoot 'Rehearsal\README.md') -Raw -Encoding utf8
Assert-True ($rehearsalReadme -match 'DBA sign-off') 'Rehearsal pack lacks a DBA sign-off template.'
Assert-True ($rehearsalReadme -match 'checksum') 'Rehearsal pack lacks immutable checksum guidance.'

$allDeploymentText = ($trackedSql | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw -Encoding utf8 }) -join "`n"
Assert-True ($allDeploymentText -notmatch '(?im)^\s*INSERT\s+(?:INTO\s+)?(?:dbo\.)?Users\b') 'A deployment SQL script creates application users.'
Assert-True ($allDeploymentText -notmatch '(?im)^\s*UPDATE\s+(?:dbo\.)?Users\s+SET\s+Password') 'A deployment SQL script updates user passwords.'

$workspaceRoot = Split-Path -Parent $repositoryRoot
$productionSeedPath = Join-Path $workspaceRoot 'outputs\database-deployment\Vitorize_Production_Seed.sql'
$productionVerificationPath = Join-Path $workspaceRoot 'outputs\database-deployment\Vitorize_Production_Verification.sql'
Assert-True (Test-Path -LiteralPath $productionSeedPath -PathType Leaf) 'Production seed package is missing.'
Assert-True (Test-Path -LiteralPath $productionVerificationPath -PathType Leaf) 'Production verification package is missing.'
$productionSeed = Get-Content -LiteralPath $productionSeedPath -Raw -Encoding utf8
$productionVerification = Get-Content -LiteralPath $productionVerificationPath -Raw -Encoding utf8
foreach ($expected in @(
    @{ Key = 'StorefrontPersianFont'; Value = 'Peyda' },
    @{ Key = 'StorefrontEnglishFont'; Value = 'Funnel Display' }
)) {
    $keyPattern = [regex]::Escape("N'$($expected.Key)'")
    Assert-True (([regex]::Matches($productionSeed, $keyPattern)).Count -eq 1) "Production seed must contain exactly one $($expected.Key) row."
    $valuePattern = "\(N'$($expected.Key)',N'$([regex]::Escape($expected.Value))',N'Typography',N'font'"
    Assert-True ($productionSeed -match $valuePattern) "Production seed default for $($expected.Key) is incorrect."
    Assert-True ($productionVerification -match [regex]::Escape("N'$($expected.Key)'")) "Production verification does not require $($expected.Key)."
}
Assert-True ($productionVerification -match '53116') 'Production verification does not reject missing or duplicate storefront typography settings.'

Write-Host "Database deployment asset tests passed: $($scripts.Count) scripts, checksums, UTF-8, baseline, validators, and target guard."
