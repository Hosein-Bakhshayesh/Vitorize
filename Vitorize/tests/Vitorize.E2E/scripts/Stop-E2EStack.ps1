[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string] $PidFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $PidFile)) { return }

try {
    # Multiple Playwright processes can reach teardown together. Another process may
    # remove the PID file after the initial existence check, which is already a
    # successful cleanup outcome rather than an error worth surfacing.
    $rawEntries = Get-Content -LiteralPath $PidFile -Raw -ErrorAction SilentlyContinue
    if ([string]::IsNullOrWhiteSpace($rawEntries)) { return }
    $entries = $rawEntries | ConvertFrom-Json
    foreach ($entry in @($entries)) {
        $process = Get-Process -Id $entry.Id -ErrorAction SilentlyContinue
        if ($null -eq $process -or $process.ProcessName -ne $entry.ProcessName -or $process.Path -ne $entry.Path) {
            continue
        }

        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit(5000)
    }
}
finally {
    Remove-Item -LiteralPath $PidFile -Force -ErrorAction SilentlyContinue
}
