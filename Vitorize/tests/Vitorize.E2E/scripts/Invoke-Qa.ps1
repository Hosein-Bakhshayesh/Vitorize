<#
.SYNOPSIS
    Single-command quality gate for Vitorize. Builds Release, provisions the isolated Testing stack,
    seeds deterministic data, runs a selected Playwright suite, and produces HTML/JUnit/JSON reports.

.DESCRIPTION
    This is the entry point every future release is validated with:
        ./scripts/Invoke-Qa.ps1 -Suite smoke
        ./scripts/Invoke-Qa.ps1 -Suite regression -Project all
        ./scripts/Invoke-Qa.ps1 -Suite admin -Repeat 3

    It NEVER touches Production: the stack runs in the Testing environment against a dedicated E2E
    database with fake SMS + fake payment providers and ephemeral keys.

.PARAMETER Suite
    smoke | auth | admin | customer | business | security | seo | ui | a11y | performance | visual | regression | all

.PARAMETER Project
    Playwright project(s): desktop-light (default, fastest) or all (desktop-light, desktop-dark, mobile-dark).

.PARAMETER Tag
    Optional custom --grep expression (e.g. '@business'); overrides the suite's default selection.

.PARAMETER Repeat
    Run the selected suite N times consecutively (stability gate). Default 1.
#>
[CmdletBinding()]
param(
    [ValidateSet('smoke','auth','admin','customer','business','security','seo','ui','a11y','performance','visual','regression','release','all')]
    [string] $Suite = 'smoke',
    [ValidateSet('desktop-light','desktop-dark','mobile-dark','all')]
    [string] $Project = 'desktop-light',
    [string] $Tag = '',
    [int] $Repeat = 1,
    [switch] $Headed,
    [switch] $KeepStack,
    [switch] $SkipBuild,
    # Recreate the E2E database from the schema DACPAC before the run (deterministic pristine state).
    # Implied by -Suite release. Required before re-approving data-driven visual baselines.
    [switch] $Reset,
    # Re-approve Playwright visual baselines. Use ONLY with -Reset so baselines capture a pristine DB.
    [switch] $UpdateSnapshots
)

$ErrorActionPreference = 'Stop'

$e2eRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$apiDll = Join-Path $repoRoot 'Vitorize.Api\bin\Release\net8.0\Vitorize.Api.dll'
$webDll = Join-Path $repoRoot 'Vitorize.Web\bin\Release\net8.0\Vitorize.Web.dll'
$apiUrl = 'http://127.0.0.1:5177'
$webUrl = 'http://127.0.0.1:5077'

# Testing-only deterministic credentials for the isolated E2E database.
$adminMobile = '09120000011'
$adminPassword = 'E2E-Admin-Only-aA1!'
$jwtKey = 'e2eStackSecretKey0123456789abcdefghij'
$encKey = 'e2eStackEncryptionKey01234567890'
$connection = if ($env:E2E_SQL_CONNECTION) { $env:E2E_SQL_CONNECTION } else { 'Server=.;Database=Vitorize_Phase3_Verification;Trusted_Connection=True;TrustServerCertificate=True' }

function Stop-Stack {
    Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and ($_.CommandLine -match 'Vitorize\.(Web|Api)\.dll') } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}

function Wait-Url([string] $url, [int] $seconds = 90) {
    for ($i = 0; $i -lt $seconds; $i += 2) {
        try { Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 4 | Out-Null; return $true } catch { Start-Sleep 2 }
    }
    return $false
}

# suite -> playwright selection (files and/or --grep). Smoke/security use tags; the rest map to files.
$selection = switch ($Suite) {
    'smoke'       { @('--grep','@smoke') }
    'auth'        { @('tests/auth-lifecycle.spec.ts','tests/authentication.spec.ts') }
    'admin'       { @('tests/admin-flows.spec.ts','tests/monitoring.spec.ts','tests/support-delivery.spec.ts') }
    'customer'    { @('tests/customer-account.spec.ts','tests/authentication.spec.ts','tests/storefront-commerce.spec.ts','tests/support-delivery.spec.ts') }
    'business'    { @('tests/storefront-commerce.spec.ts','tests/support-delivery.spec.ts') }
    'security'    { @('--grep','@security') }
    'seo'         { @('tests/seo.spec.ts') }
    'ui'          { @('tests/ui-quality.spec.ts','tests/console-quality.spec.ts','tests/accessibility.spec.ts') }
    'a11y'        { @('tests/accessibility.spec.ts') }
    'performance' { @('tests/performance.spec.ts') }
    'visual'      { @('tests/visual-regression.spec.ts') }
    default       { @() }  # regression / all -> everything
}
if ($Tag) { $selection = @('--grep', $Tag) }

$pwBin = Join-Path $e2eRoot 'node_modules\.bin\playwright.cmd'
if (-not (Test-Path $pwBin)) { throw "Playwright not installed. Run 'npm ci' in $e2eRoot." }
$isRelease = $Suite -eq 'release'
$pwArgs = @('test') + $selection
if ($Project -ne 'all') { $pwArgs += @('--project', $Project) }
if ($Headed) { $pwArgs += '--headed' }
if ($UpdateSnapshots) { $pwArgs += '--update-snapshots' }

# Run a native tool with stderr allowed (native progress on stderr must not trip ErrorActionPreference=Stop).
function Invoke-Checked([string] $label, [scriptblock] $action) {
    $prev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    & $action
    $code = $LASTEXITCODE; $ErrorActionPreference = $prev
    if ($code -ne 0) { throw "$label failed (exit $code)." }
}

Write-Host "== Vitorize QA gate ==  Suite=$Suite  Project=$Project  Repeat=$Repeat  Reset=$($Reset -or $isRelease)" -ForegroundColor Cyan

Stop-Stack
Start-Sleep 1

if ($isRelease -or -not $SkipBuild) {
    Write-Host '-- Building Release (Api + Web) --' -ForegroundColor Yellow
    Invoke-Checked 'API build' { dotnet build (Join-Path $repoRoot 'Vitorize.Api\Vitorize.Api.csproj') -c Release -p:NuGetAudit=false --nologo | Out-Null }
    Invoke-Checked 'Web build' { dotnet build (Join-Path $repoRoot 'Vitorize.Web\Vitorize.Web.csproj') -c Release -p:NuGetAudit=false --nologo | Out-Null }
}
if (-not (Test-Path $apiDll)) { throw "API not built: $apiDll (run without -SkipBuild)." }
if (-not (Test-Path $webDll)) { throw "Web not built: $webDll (run without -SkipBuild)." }

# Release gate also runs the .NET suites, then a pristine DB reset, before the browser suite.
if ($isRelease) {
    Write-Host '-- Unit tests --' -ForegroundColor Yellow
    Invoke-Checked 'Unit tests' { dotnet test (Join-Path $repoRoot 'Vitorize.Tests\Vitorize.Tests.csproj') -c Release -p:NuGetAudit=false --nologo }
    Write-Host '-- Integration tests --' -ForegroundColor Yellow
    Invoke-Checked 'Integration tests' { dotnet test (Join-Path $repoRoot 'tests\Vitorize.IntegrationTests\Vitorize.IntegrationTests.csproj') -c Release -p:NuGetAudit=false --nologo }
}

if ($Reset -or $isRelease) {
    Write-Host '-- Pristine database reset --' -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot 'Reset-E2EDatabase.ps1')
}

$stackLog = Join-Path $e2eRoot 'artifacts\stack'
New-Item -ItemType Directory -Force -Path $stackLog | Out-Null

# Shared Testing-only environment for the stack processes.
$env:ASPNETCORE_ENVIRONMENT = 'Testing'
$env:ConnectionStrings__DefaultConnection = $connection
$env:Jwt__SecretKey = $jwtKey
$env:Encryption__Key = $encKey
$env:Testing__UseFakeSms = 'true'
$env:BootstrapAdmin__Enabled = 'true'
$env:BootstrapAdmin__Mobile = $adminMobile
$env:BootstrapAdmin__Password = $adminPassword
$env:BootstrapAdmin__FullName = 'E2E Monitoring Admin'
$env:DevelopmentDemoUser__Enabled = 'false'
$env:Monitoring__ShowSeqLink = 'true'
$env:Monitoring__SeqUiUrl = 'https://seq.e2e.invalid'

try {
    Write-Host '-- Starting API --' -ForegroundColor Yellow
    $env:ASPNETCORE_URLS = $apiUrl
    # WorkingDirectory = project dir so ASP.NET's ContentRoot (== current directory) resolves wwwroot
    # from the project (static CSS/JS live there for a `build`, not the bin output).
    Start-Process dotnet -ArgumentList $apiDll -WorkingDirectory (Join-Path $repoRoot 'Vitorize.Api') -WindowStyle Hidden `
        -RedirectStandardOutput "$stackLog\api.out.log" -RedirectStandardError "$stackLog\api.err.log" | Out-Null
    if (-not (Wait-Url "$apiUrl/api/health")) { throw 'API did not become healthy.' }

    Write-Host '-- Seeding deterministic E2E data --' -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot 'Prepare-E2EDatabase.ps1')

    Write-Host '-- Starting Web --' -ForegroundColor Yellow
    $env:ASPNETCORE_URLS = $webUrl
    $env:ApiSettings__BaseUrl = "$apiUrl/api/"
    $env:ApiSettings__MediaBaseUrl = $apiUrl
    Start-Process dotnet -ArgumentList $webDll -WorkingDirectory (Join-Path $repoRoot 'Vitorize.Web') -WindowStyle Hidden `
        -RedirectStandardOutput "$stackLog\web.out.log" -RedirectStandardError "$stackLog\web.err.log" | Out-Null
    if (-not (Wait-Url "$webUrl/")) { throw 'Web did not start.' }

    # Playwright-facing environment (credentials for every deterministic role).
    $env:E2E_BASE_URL = $webUrl
    $env:E2E_API_URL = "$apiUrl/api"
    $env:E2E_ADMIN_MOBILE = $adminMobile
    $env:E2E_ADMIN_PASSWORD = $adminPassword

    Push-Location $e2eRoot
    $failed = 0
    for ($run = 1; $run -le $Repeat; $run++) {
        if ($Repeat -gt 1) { Write-Host "-- Run $run / $Repeat --" -ForegroundColor Cyan }
        # Native tools write progress/warnings to stderr; don't let ErrorActionPreference=Stop treat
        # that as a failure. Pass/fail is the process exit code.
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        & $pwBin @pwArgs
        $exit = $LASTEXITCODE
        $ErrorActionPreference = $prevEap
        if ($exit -ne 0) { $failed++ }
    }
    Pop-Location

    Write-Host ''
    Write-Host "HTML report: $e2eRoot\artifacts\report\index.html" -ForegroundColor Green
    Write-Host "JUnit XML:   $e2eRoot\artifacts\results\junit.xml" -ForegroundColor Green
    if ($failed -gt 0) { Write-Error "$failed of $Repeat run(s) had failures."; exit 1 }
    Write-Host "All $Repeat run(s) passed." -ForegroundColor Green
}
finally {
    if (-not $KeepStack) { Stop-Stack }
}
