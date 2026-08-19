[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $CsvPath,
    [Parameter(Mandatory = $true)][string] $OutputSql,
    [switch] $Apply,
    [string] $ServerInstance,
    [string] $Database,
    [string] $ConfirmDatabaseName
)

$ErrorActionPreference = 'Stop'
$rows = @(Import-Csv -LiteralPath $CsvPath)
if ($rows.Count -eq 0) { throw 'Redirect mapping is empty.' }
$issues = [System.Collections.Generic.List[string]]::new()
$seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$destinations = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

foreach ($row in $rows) {
    $source = ([string]$row.SourcePath).Trim().TrimEnd('/')
    if (-not $source) { $source = '/' }
    $destination = ([string]$row.DestinationPath).Trim().TrimEnd('/')
    $status = 0
    [void][int]::TryParse([string]$row.StatusCode, [ref]$status)
    if (-not $source.StartsWith('/') -or $source.Contains('?') -or $source.Contains('#')) { $issues.Add("Invalid source: $source") }
    if (-not $seen.Add($source)) { $issues.Add("Duplicate source: $source") }
    if ($status -notin @(301, 308, 410)) { $issues.Add("Invalid status for $source") }
    if ($status -eq 410 -and $destination) { $issues.Add("410 must not have a destination: $source") }
    if ($status -in @(301, 308) -and (-not $destination.StartsWith('/') -or $destination -eq $source)) { $issues.Add("Invalid destination for $source") }
    if ($destination) { [void]$destinations.Add($destination) }
}
foreach ($source in $seen) { if ($destinations.Contains($source)) { $issues.Add("Redirect chain detected through: $source") } }
if ($issues.Count -gt 0) { $issues | ForEach-Object { Write-Error $_ }; throw "Redirect validation failed with $($issues.Count) issue(s)." }

function SqlLiteral([string]$value) { return "N'" + $value.Replace("'", "''") + "'" }
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRANSACTION;')
foreach ($row in $rows) {
    $source = ([string]$row.SourcePath).Trim().TrimEnd('/'); if (-not $source) { $source = '/' }
    $destination = ([string]$row.DestinationPath).Trim().TrimEnd('/')
    $status = [int]$row.StatusCode
    $destinationSql = if ($destination) { SqlLiteral $destination } else { 'NULL' }
    $lines.Add("MERGE dbo.LegacyRedirects WITH (HOLDLOCK) AS target USING (SELECT $(SqlLiteral $source) AS SourcePath) AS source ON target.SourcePath = source.SourcePath WHEN MATCHED THEN UPDATE SET DestinationPath = $destinationSql, StatusCode = $status, IsActive = 1, UpdatedAt = SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT (Id, SourcePath, DestinationPath, StatusCode, IsActive, CreatedAt) VALUES (NEWID(), source.SourcePath, $destinationSql, $status, 1, SYSUTCDATETIME());")
}
$lines.Add('COMMIT TRANSACTION;')
[IO.File]::WriteAllLines([IO.Path]::GetFullPath($OutputSql), $lines, [Text.UTF8Encoding]::new($false))
Write-Host "Validated $($rows.Count) redirects and generated $OutputSql."

if ($Apply) {
    if (-not $ServerInstance -or -not $Database -or $ConfirmDatabaseName -cne $Database) { throw 'Apply requires ServerInstance, Database, and an exact ConfirmDatabaseName.' }
    & sqlcmd -S $ServerInstance -d $Database -E -b -f 65001 -i $OutputSql
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed with exit code $LASTEXITCODE." }
}
