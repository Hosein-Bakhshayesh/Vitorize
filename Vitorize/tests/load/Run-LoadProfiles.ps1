[CmdletBinding()]
<#
.SYNOPSIS
    Runs the Vitorize Phase 4 k6 load profiles and captures the Part 4 acceptance metrics.

.DESCRIPTION
    Thin orchestrator around tests/load/vitorize-load.js. It verifies k6 is installed, runs one or
    more profiles against the Testing environment, writes the k6 JSON summary per profile under
    TestResults/load, and prints a compact latency/throughput/error table.

    This NEVER targets Production. Pass an explicit BaseUrl for the Testing API (default 5177).

.EXAMPLE
    ./Run-LoadProfiles.ps1 -Profiles baseline,normal -BaseUrl http://localhost:5177

.EXAMPLE
    ./Run-LoadProfiles.ps1 -Profiles auth,checkout -LoginMobile 09120000000 -LoginPassword 'Secret-123!' -ProductId 00000000-0000-0000-0000-000000000000
#>
param(
    [ValidateSet('smoke', 'baseline', 'normal', 'busy', 'peak', 'checkout', 'auth', 'admin', 'soak')]
    [string[]] $Profiles = @('smoke'),
    [string] $BaseUrl = 'http://localhost:5177',
    [string] $ProductSlug = 'e2e-seo-product',
    [string] $ProductId = '',
    [string] $LoginMobile = '',
    [string] $LoginPassword = '',
    [string] $AdminMobile = '',
    [string] $AdminPassword = '',
    [string] $OtpCode = '11111'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($BaseUrl -notmatch '^http://(localhost|127\.0\.0\.1)') {
    throw "Refusing to run: BaseUrl '$BaseUrl' is not a local Testing host. Phase 4 load tests must never target Production."
}

$k6 = Get-Command k6 -ErrorAction SilentlyContinue
if (-not $k6) {
    Write-Warning 'k6 is not installed. Install it, then re-run:'
    Write-Warning '  winget install k6.k6      (or)      choco install k6'
    Write-Warning 'Meanwhile, Test-PublicLoad.ps1 provides a pure-PowerShell smoke of the public routes.'
    throw 'k6 executable not found on PATH.'
}

$script = Join-Path $PSScriptRoot 'vitorize-load.js'
$resultsDir = Join-Path $PSScriptRoot '..\..\TestResults\load'
New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null

$rows = New-Object System.Collections.Generic.List[object]
foreach ($profile in $Profiles) {
    $summaryPath = Join-Path $resultsDir "summary-$profile.json"
    Write-Host "=== Running profile: $profile against $BaseUrl ===" -ForegroundColor Cyan

    $env:BASE_URL = $BaseUrl
    $env:PROFILE = $profile
    $env:PRODUCT_SLUG = $ProductSlug
    $env:PRODUCT_ID = $ProductId
    $env:LOGIN_MOBILE = $LoginMobile
    $env:LOGIN_PASSWORD = $LoginPassword
    $env:ADMIN_MOBILE = $AdminMobile
    $env:ADMIN_PASSWORD = $AdminPassword
    $env:OTP_CODE = $OtpCode

    & $k6.Source run --summary-export $summaryPath $script
    $k6Exit = $LASTEXITCODE

    if (Test-Path $summaryPath) {
        $summary = Get-Content $summaryPath -Raw | ConvertFrom-Json
        $metrics = $summary.metrics
        $rows.Add([pscustomobject]@{
            Profile     = $profile
            Requests    = $metrics.http_reqs.count
            RPS         = [Math]::Round($metrics.http_reqs.rate, 2)
            ErrorRate   = if ($metrics.PSObject.Properties.Name -contains 'vitorize_errors') { [Math]::Round($metrics.vitorize_errors.value, 4) } else { 'n/a' }
            P50ms       = [Math]::Round($metrics.http_req_duration.'p(50)', 1)
            P90ms       = [Math]::Round($metrics.http_req_duration.'p(90)', 1)
            P95ms       = [Math]::Round($metrics.http_req_duration.'p(95)', 1)
            P99ms       = if ($metrics.http_req_duration.PSObject.Properties.Name -contains 'p(99)') { [Math]::Round($metrics.http_req_duration.'p(99)', 1) } else { 'n/a' }
            MaxMs       = [Math]::Round($metrics.http_req_duration.max, 1)
            Thresholds  = if ($k6Exit -eq 0) { 'PASS' } else { 'FAIL' }
        })
    } else {
        $rows.Add([pscustomobject]@{ Profile = $profile; Requests = 'n/a'; Thresholds = 'NO-SUMMARY' })
    }
}

Write-Host "`n===== Phase 4 load results =====" -ForegroundColor Green
$rows | Format-Table -AutoSize
$rows | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $resultsDir 'aggregate.json') -Encoding utf8
Write-Host "Per-profile summaries written to $resultsDir" -ForegroundColor Green
