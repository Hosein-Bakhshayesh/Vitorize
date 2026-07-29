[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$connection = if ($env:E2E_SQL_CONNECTION) { $env:E2E_SQL_CONNECTION } else { 'Server=.;Database=Vitorize_Phase3_Verification;Trusted_Connection=True;TrustServerCertificate=True' }
$randomBytes = New-Object byte[] 48
$randomGenerator = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $randomGenerator.GetBytes($randomBytes) } finally { $randomGenerator.Dispose() }
$random = [Convert]::ToBase64String($randomBytes)
$env:ASPNETCORE_ENVIRONMENT = 'Testing'
$env:ConnectionStrings__DefaultConnection = $connection
$env:Jwt__SecretKey = $random
$env:Encryption__Key = $random.Substring(0, 32)
$env:BootstrapAdmin__Enabled = 'true'
if (-not $env:E2E_ADMIN_MOBILE -or -not $env:E2E_ADMIN_PASSWORD) { throw 'Playwright must supply randomized E2E bootstrap credentials.' }
$env:BootstrapAdmin__Mobile = $env:E2E_ADMIN_MOBILE
$env:BootstrapAdmin__Password = $env:E2E_ADMIN_PASSWORD
$env:BootstrapAdmin__FullName = 'E2E Monitoring Admin'
$env:DevelopmentDemoUser__Enabled = 'false'
$env:ApiSettings__BaseUrl = 'http://127.0.0.1:5177/api/'
$env:ApiSettings__MediaBaseUrl = 'http://127.0.0.1:5177'
$env:Monitoring__ShowSeqLink = 'true'
$env:Monitoring__SeqUiUrl = 'https://seq.e2e.invalid'
$env:Testing__UseFakeSms = 'true'

$logRoot = Join-Path $PSScriptRoot '..\artifacts\stack'
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null
$pidFile = Join-Path $logRoot 'managed-stack-pids.json'
$stopManagedStack = Join-Path $PSScriptRoot 'Stop-E2EStack.ps1'

& $stopManagedStack -PidFile $pidFile
$apiPath = Join-Path $root 'Vitorize.Api\bin\Release\net8.0\Vitorize.Api.exe'
$webPath = Join-Path $root 'Vitorize.Web\bin\Release\net8.0\Vitorize.Web.exe'
if (-not (Test-Path -LiteralPath $apiPath) -or -not (Test-Path -LiteralPath $webPath)) { throw 'Release executables are required before starting the E2E stack.' }
$apiContentRoot = Join-Path $root 'Vitorize.Api'
$webContentRoot = Join-Path $root 'Vitorize.Web'
$api = Start-Process $apiPath -WorkingDirectory $apiContentRoot -ArgumentList @('--urls','http://127.0.0.1:5177','--contentRoot',$apiContentRoot,'--webroot',(Join-Path $apiContentRoot 'wwwroot')) -PassThru -WindowStyle Hidden -RedirectStandardOutput "$logRoot\api.out.log" -RedirectStandardError "$logRoot\api.err.log"
$web = Start-Process $webPath -WorkingDirectory $webContentRoot -ArgumentList @('--urls','http://127.0.0.1:5077','--contentRoot',$webContentRoot,'--webroot',(Join-Path $webContentRoot 'wwwroot')) -PassThru -WindowStyle Hidden -RedirectStandardOutput "$logRoot\web.out.log" -RedirectStandardError "$logRoot\web.err.log"
@(
    [pscustomobject]@{ Id = $api.Id; ProcessName = $api.ProcessName; Path = $api.Path },
    [pscustomobject]@{ Id = $web.Id; ProcessName = $web.ProcessName; Path = $web.Path }
) | ConvertTo-Json | Set-Content -LiteralPath $pidFile -Encoding UTF8

try {
    while (-not $api.HasExited -and -not $web.HasExited) { Start-Sleep -Milliseconds 500 }
}
finally {
    & $stopManagedStack -PidFile $pidFile
}
