[CmdletBinding()]
param(
    [string] $PrivateDocumentsRoot = 'D:\Vitorize\Vitorize\Vitorize.Api\App_Data\PrivateDocuments'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath $PrivateDocumentsRoot).Path
$ownerIds = @('31000000000000000000000000000021')
foreach ($number in 71..81) {
    $ownerIds += ([guid]("33000000-0000-0000-0000-0000000000{0:D2}" -f $number)).ToString('N')
    # Remove only an earlier malformed fixture-directory variant if it exists.
    $ownerIds += ("3300000000000000000000000000{0:D2}" -f $number)
}

$removedFiles = 0
$removedDirectories = 0
foreach ($ownerId in $ownerIds | Select-Object -Unique) {
    $candidate = Join-Path $root $ownerId
    if (-not (Test-Path -LiteralPath $candidate -PathType Container)) { continue }

    $resolved = (Resolve-Path -LiteralPath $candidate).Path
    if (-not $resolved.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing cleanup outside the deterministic fixture root: $resolved"
    }

    $children = @(Get-ChildItem -LiteralPath $resolved -Force)
    $nestedDirectories = @($children | Where-Object { $_.PSIsContainer })
    if ($nestedDirectories.Count -gt 0) {
        throw "Refusing recursive cleanup for unexpected nested directories in $resolved"
    }

    foreach ($file in @($children | Where-Object { -not $_.PSIsContainer })) {
        Remove-Item -LiteralPath $file.FullName -Force
        $removedFiles++
    }

    if (-not (Get-ChildItem -LiteralPath $resolved -Force | Select-Object -First 1)) {
        Remove-Item -LiteralPath $resolved -Force
        $removedDirectories++
    }
}

[pscustomobject]@{
    RemovedFiles = $removedFiles
    RemovedDirectories = $removedDirectories
    UsedRecursiveDelete = $false
}
