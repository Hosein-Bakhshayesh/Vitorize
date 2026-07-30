[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$logRoot = Join-Path $root 'artifacts\diagnostics'
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null
$logFile = Join-Path $logRoot 'admin-suite-run.log'

$env:E2E_MANAGE_STACK = 'true'
$env:E2E_VIDEO = 'off'
$startedAt = Get-Date
"[$($startedAt.ToString('o'))] Admin suite started. Projects=3; workers=1; discovered executions=93." |
    Tee-Object -FilePath $logFile -Append

Push-Location $root
try {
    & .\node_modules\.bin\playwright.cmd test tests/admin-flows.spec.ts tests/admin-paging-exports.spec.ts tests/monitoring.spec.ts tests/support-delivery.spec.ts tests/product-matrix.spec.ts tests/product-variants.spec.ts tests/product-admin-edit.spec.ts tests/product-negative.spec.ts 2>&1 |
        Tee-Object -FilePath $logFile -Append
    $exitCode = $LASTEXITCODE
}
finally {
    $completedAt = Get-Date
    "[$($completedAt.ToString('o'))] Admin suite finished. ExitCode=$exitCode; Duration=$([Math]::Round(($completedAt - $startedAt).TotalSeconds, 1))s." |
        Tee-Object -FilePath $logFile -Append
    Pop-Location
}

exit $exitCode
