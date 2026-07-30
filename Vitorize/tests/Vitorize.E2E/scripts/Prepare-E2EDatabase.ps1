[CmdletBinding()]
param(
    [string] $ServerInstance = '.',
    [string] $Database = 'Vitorize_Phase3_Verification',
    [string] $SqlConnectionString = $env:E2E_SQL_CONNECTION
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ConnectionValue([string] $ConnectionString, [string[]] $Keys) {
    foreach ($key in $Keys) {
        $match = [regex]::Match($ConnectionString, '(?i)(?:^|;)\s*' + [regex]::Escape($key) + '\s*=\s*([^;]*)')
        if ($match.Success) { return $match.Groups[1].Value.Trim() }
    }
    return ''
}
$sqlAuthenticationArguments = @('-E')
if (-not [string]::IsNullOrWhiteSpace($SqlConnectionString)) {
    $connectionServer = Get-ConnectionValue $SqlConnectionString @('Data Source', 'Server')
    if ([string]::IsNullOrWhiteSpace($connectionServer)) { throw 'SqlConnectionString must include Data Source or Server.' }
    if (-not $PSBoundParameters.ContainsKey('ServerInstance')) { $ServerInstance = $connectionServer }
    $connectionDatabase = Get-ConnectionValue $SqlConnectionString @('Initial Catalog', 'Database')
    if (-not $PSBoundParameters.ContainsKey('Database') -and -not [string]::IsNullOrWhiteSpace($connectionDatabase)) { $Database = $connectionDatabase }
    $integratedValue = Get-ConnectionValue $SqlConnectionString @('Integrated Security', 'Trusted_Connection')
    if ($integratedValue -notmatch '^(?i:true|yes|sspi)$') {
        $userId = Get-ConnectionValue $SqlConnectionString @('User ID', 'UID')
        $password = Get-ConnectionValue $SqlConnectionString @('Password', 'PWD')
        if ([string]::IsNullOrWhiteSpace($userId) -or [string]::IsNullOrWhiteSpace($password)) { throw 'SQL authentication requires both User ID and Password.' }
        $sqlAuthenticationArguments = @('-U', $userId, '-P', $password, '-C')
    }
}
if ($Database -notmatch '^Vitorize_[A-Za-z0-9_]+$') { throw 'The E2E database name must start with Vitorize_ and contain only letters, digits, or underscore.' }
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$fixture = (Resolve-Path (Join-Path $PSScriptRoot '..\fixtures\seed-e2e.sql')).Path
& sqlcmd -S $ServerInstance -d $Database @sqlAuthenticationArguments -b -f 65001 -i $fixture
if ($LASTEXITCODE -ne 0) { throw 'E2E fixture failed.' }
Write-Host "Prepared deterministic E2E data in $Database."
