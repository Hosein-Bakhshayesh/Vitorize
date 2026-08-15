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
$phase2fFixture = (Resolve-Path (Join-Path $PSScriptRoot '..\fixtures\seed-fix09-phase2f-customer.sql')).Path
& sqlcmd -S $ServerInstance -d $Database @sqlAuthenticationArguments -b -f 65001 -i $phase2fFixture
if ($LASTEXITCODE -ne 0) { throw 'FIX-09 Phase-2F Customer fixture failed.' }
$phase3abFixture = (Resolve-Path (Join-Path $PSScriptRoot '..\fixtures\seed-fix09-phase3ab-isolated.sql')).Path
& sqlcmd -S $ServerInstance -d $Database @sqlAuthenticationArguments -b -f 65001 -i $phase3abFixture
if ($LASTEXITCODE -ne 0) { throw 'FIX-09 Phase-3A-B isolated fixture failed.' }
$phase3bFixture = (Resolve-Path (Join-Path $PSScriptRoot '..\fixtures\seed-fix09-phase3b-deadline.sql')).Path
& sqlcmd -S $ServerInstance -d $Database @sqlAuthenticationArguments -b -f 65001 -i $phase3bFixture
if ($LASTEXITCODE -ne 0) { throw 'FIX-09 Phase-3B-B deadline fixture failed.' }
$finalBrowserFixture = (Resolve-Path (Join-Path $PSScriptRoot '..\fixtures\seed-fix09-final-browser.sql')).Path
& sqlcmd -S $ServerInstance -d $Database @sqlAuthenticationArguments -b -f 65001 -i $finalBrowserFixture
if ($LASTEXITCODE -ne 0) { throw 'FIX-09 final browser fixture failed.' }

# The browser fixture's instant-code canaries must be valid application ciphertext.
# Generate legacy-compatible AES-CBC values at seed time with the ephemeral Testing
# key instead of committing ciphertext bound to one runner key or plaintext in an
# encrypted column. The application maintains this reader for legacy rows.
$encryptionKey = $env:Encryption__Key
if ([string]::IsNullOrWhiteSpace($encryptionKey) -or [Text.Encoding]::UTF8.GetByteCount($encryptionKey) -ne 32) {
    throw 'A 32-byte Testing Encryption__Key is required to seed the Phase-2G instant-code fixture.'
}
function Protect-E2EFixtureValue([string] $Plaintext) {
    $keyBytes = [Text.Encoding]::UTF8.GetBytes($encryptionKey)
    $plainBytes = [Text.Encoding]::UTF8.GetBytes($Plaintext)
    $aes = [Security.Cryptography.Aes]::Create()
    try {
        $aes.Key = $keyBytes
        $aes.GenerateIV()
        $encryptor = $aes.CreateEncryptor()
        try { $cipherBytes = $encryptor.TransformFinalBlock($plainBytes, 0, $plainBytes.Length) } finally { $encryptor.Dispose() }
        $fullCipher = [byte[]]::new($aes.IV.Length + $cipherBytes.Length)
        [Buffer]::BlockCopy($aes.IV, 0, $fullCipher, 0, $aes.IV.Length)
        [Buffer]::BlockCopy($cipherBytes, 0, $fullCipher, $aes.IV.Length, $cipherBytes.Length)
        return [Convert]::ToBase64String($fullCipher)
    }
    finally { $aes.Dispose() }
}
$phase2fSecrets = @{
    'fix09-p2f-delivered' = 'P2F-DELIVERED-CODE'
    'fix09-p2f-held' = 'FIX09-P2F-HELD-CANARY-DO-NOT-RENDER'
    'fix09-p2f-checkout' = 'P2F-CHECKOUT-AVAILABLE'
    'fix09-p2g-checkout-1' = 'P2G-CHECKOUT-1'
    'fix09-p2g-checkout-2' = 'P2G-CHECKOUT-2'
    'fix09-p2g-checkout-3' = 'P2G-CHECKOUT-3'
    'fix09-p2g-checkout-4' = 'P2G-CHECKOUT-4'
    'fix09-p2g-checkout-5' = 'P2G-CHECKOUT-5'
}
foreach ($entry in $phase2fSecrets.GetEnumerator()) {
    $ciphertext = (Protect-E2EFixtureValue $entry.Value).Replace("'", "''")
    $fingerprint = $entry.Key.Replace("'", "''")
    & sqlcmd -S $ServerInstance -d $Database @sqlAuthenticationArguments -b -Q "SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; UPDATE dbo.GiftCodes SET EncryptedCode = N'$ciphertext', EncryptionVersion = 0 WHERE CodeHashFingerprint = N'$fingerprint';"
    if ($LASTEXITCODE -ne 0) { throw "Could not encrypt E2E gift-code fixture '$($entry.Key)'." }
}
$fixtureOwnerId = '31000000000000000000000000000021'
$fixturePrivateDirectory = Join-Path $root "Vitorize.Api\App_Data\PrivateDocuments\$fixtureOwnerId"
$fixtureDocument = Join-Path $fixturePrivateDirectory 'document-canary.jpg'
$fixtureImage = Join-Path $root 'Vitorize.Api\wwwroot\uploads\products\947c2fd1b9a84f2ea86a683008e7fdc0.jpg'
New-Item -ItemType Directory -Force -Path $fixturePrivateDirectory | Out-Null
Copy-Item -LiteralPath $fixtureImage -Destination $fixtureDocument -Force
$phase3abOwners = 71..81 | ForEach-Object { ([guid]("33000000-0000-0000-0000-0000000000{0:D2}" -f $_)).ToString('N') }
foreach ($ownerId in $phase3abOwners) {
    New-Item -ItemType Directory -Force -Path (Join-Path $root "Vitorize.Api\App_Data\PrivateDocuments\$ownerId") | Out-Null
}
$phase3bbOwners = 91..100 | ForEach-Object { ([guid]("33000000-0000-0000-0000-000000000{0:D3}" -f $_)).ToString('N') }
foreach ($ownerId in $phase3bbOwners) {
    New-Item -ItemType Directory -Force -Path (Join-Path $root "Vitorize.Api\App_Data\PrivateDocuments\$ownerId") | Out-Null
}
$deliveredContent = (Protect-E2EFixtureValue 'P2F-DELIVERED-CODE').Replace("'", "''")
& sqlcmd -S $ServerInstance -d $Database @sqlAuthenticationArguments -b -Q "SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; UPDATE dbo.OrderItemDeliveries SET DeliveredContent = N'$deliveredContent', EncryptionVersion = 0 WHERE GiftCodeId = '32000000-0000-0000-0000-000000000501';"
if ($LASTEXITCODE -ne 0) { throw 'Could not encrypt delivered E2E gift-code fixture content.' }
Write-Host "Prepared deterministic E2E data in $Database."
