[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string] $PidFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $PidFile)) { return }

try {
    $entries = Get-Content -LiteralPath $PidFile -Raw | ConvertFrom-Json
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
