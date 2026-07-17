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
$api = Start-Process dotnet -ArgumentList @('run','--project',"$root\Vitorize.Api\Vitorize.Api.csproj",'-c','Release','--no-build','--no-launch-profile','--urls','http://127.0.0.1:5177') -PassThru -NoNewWindow -RedirectStandardOutput "$logRoot\api.out.log" -RedirectStandardError "$logRoot\api.err.log"
$web = Start-Process dotnet -ArgumentList @('run','--project',"$root\Vitorize.Web\Vitorize.Web.csproj",'-c','Release','--no-build','--no-launch-profile','--urls','http://127.0.0.1:5077') -PassThru -NoNewWindow -RedirectStandardOutput "$logRoot\web.out.log" -RedirectStandardError "$logRoot\web.err.log"

try {
    while (-not $api.HasExited -and -not $web.HasExited) { Start-Sleep -Milliseconds 500 }
    throw "E2E stack stopped unexpectedly. API exit=$($api.ExitCode); Web exit=$($web.ExitCode)."
}
finally {
    foreach ($process in @($web, $api)) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    }
}
