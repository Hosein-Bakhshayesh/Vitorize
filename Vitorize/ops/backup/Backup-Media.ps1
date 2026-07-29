[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string] $PublicMediaRoot,
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string] $BackupRoot,
    [string] $PrivateDocumentRoot,
    [string] $RunId = (Get-Date -Format 'yyyyMMdd-HHmmss')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $PublicMediaRoot -PathType Container)) { throw 'PublicMediaRoot does not exist.' }
if ($PrivateDocumentRoot -and -not (Test-Path -LiteralPath $PrivateDocumentRoot -PathType Container)) { throw 'PrivateDocumentRoot does not exist.' }
if ($RunId -notmatch '^[0-9A-Za-z_-]{8,64}$') { throw 'RunId contains unsupported characters.' }

$target = Join-Path ([IO.Path]::GetFullPath($BackupRoot)) "vitorize-media-$RunId"
if (Test-Path -LiteralPath $target) { throw "Refusing to overwrite existing media backup: $target" }
New-Item -ItemType Directory -Path $target -Force | Out-Null
function Copy-Tree([string] $source, [string] $name) {
    $destination = Join-Path $target $name
    & robocopy $source $destination /E /COPY:DAT /DCOPY:T /R:2 /W:2 /NP /NFL /NDL | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed for $name with exit code $LASTEXITCODE." }
}
Copy-Tree $PublicMediaRoot 'public-media'
if ($PrivateDocumentRoot) { Copy-Tree $PrivateDocumentRoot 'private-documents' }
$manifest = Get-ChildItem -LiteralPath $target -File -Recurse | ForEach-Object {
    [pscustomobject]@{ Path = $_.FullName.Substring($target.Length).TrimStart('\'); Length = $_.Length; Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
}
$manifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $target 'media-manifest.sha256.json') -Encoding utf8
Write-Host "MEDIA BACKUP VERIFIED: $target files=$($manifest.Count)"
