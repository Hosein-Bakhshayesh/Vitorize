<#
.SYNOPSIS
    Assembles Vitorize.Database.FullBootstrap.sql - a single, database-agnostic fresh-install
    script for SSMS.

.DESCRIPTION
    Schema comes from a sqlpackage-generated publish script produced against the VERIFIED reference
    database (baseline DACPAC + every Production-selected manifest script through V0019). The
    SQLCMD preamble that sqlpackage emits is stripped, so the result runs against whatever database
    is currently selected in SSMS - no database name, no :setvar, no SQLCMD mode.

    Seed data is read live from the same reference database and limited to genuine system tables.
    The migration ledger is written from the real manifest so a later V0020+ deployment sees every
    included script as already applied with a matching checksum.

.PARAMETER RawSchema   sqlpackage /Action:Script output.
.PARAMETER RefDatabase Verified reference database to read seed rows from.
.PARAMETER OutFile     Destination .sql
#>
param(
    [Parameter(Mandatory = $true)][string] $RawSchema,
    [Parameter(Mandatory = $true)][string] $RefDatabase,
    [Parameter(Mandatory = $true)][string] $OutFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$connectionString = "Server=.;Database=$RefDatabase;Integrated Security=true;TrustServerCertificate=true"

# Seed tables, in foreign-key safe order. Only genuine system/reference data - no business rows.
$seedTables = @(
    @{ Name = 'Roles';             Identity = $false },
    @{ Name = 'Settings';          Identity = $false },
    @{ Name = 'Pages';             Identity = $false },
    @{ Name = 'FontAssets';        Identity = $false },
    @{ Name = 'KycPolicies';       Identity = $false },
    @{ Name = 'KycPolicyVersions'; Identity = $false }
)

function Get-SqlLiteral($value, [string] $sqlType) {
    if ($null -eq $value -or $value -is [DBNull]) { return 'NULL' }
    switch -Regex ($sqlType) {
        '^(bit)$'                              { return $(if ([bool]$value) { '1' } else { '0' }) }
        '^(tinyint|smallint|int|bigint)$'      { return [string]$value }
        '^(decimal|numeric|money|float|real)$' { return ([decimal]$value).ToString([Globalization.CultureInfo]::InvariantCulture) }
        '^(uniqueidentifier)$'                 { return "'" + $value.ToString() + "'" }
        '^(datetime2|datetime|date|datetimeoffset)$' {
            return "CONVERT(datetime2, '" + ([datetime]$value).ToString('yyyy-MM-ddTHH:mm:ss.fffffff') + "', 126)"
        }
        '^(varbinary|binary|image)$' {
            return '0x' + (($value | ForEach-Object { $_.ToString('x2') }) -join '')
        }
        default {
            # Unicode literal; double up single quotes.
            return 'N''' + ([string]$value).Replace("'", "''") + ''''
        }
    }
}

# ---------------------------------------------------------------- schema
Write-Host 'reading schema script...'
$lines = Get-Content -LiteralPath $RawSchema

# Drop the SQLCMD preamble: everything up to and including the first "USE [$(DatabaseName)];".
$startIndex = 0
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^\s*USE \[\$\(DatabaseName\)\]') { $startIndex = $i + 1; break }
}
$body = $lines[$startIndex..($lines.Count - 1)] | Where-Object {
    $_ -notmatch '^\s*USE \[' -and $_ -notmatch '^\s*:setvar' -and $_ -notmatch '^\s*:on error' -and
    $_ -notmatch '\$\(__IsSqlCmdEnabled\)' -and $_ -notmatch '^\s*SET NOEXEC ON;\s*$'
}
$schemaSql = ($body -join "`r`n").Trim()
Write-Host ("schema body: {0} lines" -f $body.Count)

# ---------------------------------------------------------------- seed data
$sb = New-Object System.Text.StringBuilder
$connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
$connection.Open()
try {
    foreach ($table in $seedTables) {
        $name = $table.Name

        $schemaCmd = $connection.CreateCommand()
        $schemaCmd.CommandText = @"
SELECT c.name, ty.name AS type_name
FROM sys.columns c
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID(@t) AND c.is_computed = 0
ORDER BY c.column_id
"@
        [void]$schemaCmd.Parameters.AddWithValue('@t', "dbo.$name")
        $reader = $schemaCmd.ExecuteReader()
        $columns = @()
        while ($reader.Read()) { $columns += @{ Name = $reader.GetString(0); Type = $reader.GetString(1) } }
        $reader.Close()

        $columnList = ($columns | ForEach-Object { '[' + $_.Name + ']' }) -join ', '
        $dataCmd = $connection.CreateCommand()
        $dataCmd.CommandText = "SELECT $columnList FROM dbo.[$name]"
        $rows = $dataCmd.ExecuteReader()

        $values = @()
        while ($rows.Read()) {
            $literals = @()
            for ($c = 0; $c -lt $columns.Count; $c++) {
                $literals += Get-SqlLiteral $rows.GetValue($c) $columns[$c].Type
            }
            $values += '  (' + ($literals -join ', ') + ')'
        }
        $rows.Close()

        [void]$sb.AppendLine("PRINT N'Seeding dbo.$name ($($values.Count) row(s))...';")
        [void]$sb.AppendLine('GO')
        # Batched in groups of 200: SQL Server caps a single VALUES clause at 1000 rows.
        for ($offset = 0; $offset -lt $values.Count; $offset += 200) {
            $chunk = $values[$offset..([Math]::Min($offset + 199, $values.Count - 1))]
            [void]$sb.AppendLine("INSERT INTO dbo.[$name] ($columnList) VALUES")
            [void]$sb.AppendLine(($chunk -join ",`r`n") + ';')
            [void]$sb.AppendLine('GO')
        }
        [void]$sb.AppendLine('')
        Write-Host ("seeded {0}: {1} rows" -f $name, $values.Count)
    }

    # ---------------------------------------------------------------- migration ledger
    $ledgerCmd = $connection.CreateCommand()
    $ledgerCmd.CommandText = 'SELECT ScriptName, ScriptVersion, ScriptHash, Environment, Success, Notes FROM dbo.DatabaseScriptHistory ORDER BY Id'
    $ledgerReader = $ledgerCmd.ExecuteReader()
    $ledgerRows = @()
    while ($ledgerReader.Read()) {
        $ledgerRows += '  (' + (@(
            (Get-SqlLiteral $ledgerReader.GetValue(0) 'nvarchar'),
            (Get-SqlLiteral $ledgerReader.GetValue(1) 'nvarchar'),
            (Get-SqlLiteral $ledgerReader.GetValue(2) 'char'),
            'SYSUTCDATETIME()',
            'SUSER_SNAME()',
            (Get-SqlLiteral $ledgerReader.GetValue(3) 'nvarchar'),
            (Get-SqlLiteral $ledgerReader.GetValue(4) 'bit'),
            (Get-SqlLiteral $ledgerReader.GetValue(5) 'nvarchar')
        ) -join ', ') + ')'
    }
    $ledgerReader.Close()
    Write-Host ("ledger rows: {0}" -f $ledgerRows.Count)
} finally { $connection.Close() }

# ---------------------------------------------------------------- assemble
$header = @"
/*=============================================================================
  VITORIZE DATABASE - FRESH INSTALL (FullBootstrap)

  1. Create an EMPTY database on your SQL Server.
  2. Select that database in SSMS (the database dropdown).
  3. Open this file and press Execute.

  This script builds the complete Vitorize schema and seeds the system data the
  application needs on first start. It runs against WHICHEVER DATABASE IS
  CURRENTLY SELECTED - it never names, creates or alters a database.

  DO NOT run this against an existing Vitorize installation. A guard below stops
  the script if Vitorize tables are already present.

  Requires: SQL Server 2019 or later. Plain SSMS execution - no SQLCMD mode,
  no PowerShell, no DACPAC, no parameters.

  Contains no passwords, connection strings or application keys. Those live only
  in the application's appsettings.Production.json on the server.
=============================================================================*/
GO

SET ANSI_NULLS, ANSI_PADDING, ANSI_WARNINGS, ARITHABORT, CONCAT_NULL_YIELDS_NULL, QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
GO

/*----------------------------------------------------------------------------
  Fresh-install guard. SET NOEXEC ON suppresses every later batch in this
  session without dropping or altering anything.
----------------------------------------------------------------------------*/
IF EXISTS (SELECT 1 FROM sys.tables WHERE name IN (N'Settings', N'Users', N'Orders', N'DatabaseScriptHistory'))
BEGIN
    RAISERROR(N'Vitorize FullBootstrap is for a fresh EMPTY database only. This database already contains Vitorize tables - nothing has been changed. Use the versioned upgrade process instead.', 16, 1);
    SET NOEXEC ON;
END
GO

PRINT N'Vitorize fresh install starting...';
GO

"@

$footer = @"

/*----------------------------------------------------------------------------
  Migration ledger: records every script represented by this bootstrap so a
  future versioned deployment applies only newer scripts. Checksums are the
  real canonical values from the deployment manifest.
----------------------------------------------------------------------------*/
PRINT N'Recording deployment history ($($ledgerRows.Count) script(s))...';
GO
INSERT INTO dbo.DatabaseScriptHistory (ScriptName, ScriptVersion, ScriptHash, AppliedAt, AppliedBy, Environment, Success, Notes) VALUES
$($ledgerRows -join ",`r`n");
GO

PRINT N'Vitorize fresh install completed successfully.';
PRINT N'Next: deploy the API and Web packages, then configure the first administrator.';
GO

SET NOEXEC OFF;
GO
"@

$final = $header + $schemaSql + "`r`n`r`n" + $sb.ToString() + $footer
[IO.File]::WriteAllText($OutFile, $final, (New-Object Text.UTF8Encoding($true)))
Write-Host ("written: {0} ({1} KB)" -f $OutFile, [Math]::Round((Get-Item $OutFile).Length / 1KB, 1))
