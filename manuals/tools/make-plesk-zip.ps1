<#
.SYNOPSIS
    Packs a published folder into a Plesk-ready ZIP whose contents sit at the archive root.

.DESCRIPTION
    .NET Framework's ZipFile.CreateFromDirectory writes entry names with backslash separators on
    Windows. The ZIP specification requires '/', and extractors on Linux-based hosting panels can
    treat a backslash as part of the file name, producing a flat folder of oddly named files
    instead of a directory tree. This builds the archive entry by entry with normalised paths and
    no wrapper directory, so extracting into the Plesk application root yields web.config and the
    entry assembly directly at that root.

.EXAMPLE
    .\make-plesk-zip.ps1 -SourceDir ..\..\outputs\plesk-ready-20260817\Api `
                         -OutFile   ..\..\outputs\plesk-ready-20260817\Vitorize.Api.Plesk.zip
#>
param(
    [Parameter(Mandatory = $true)][string] $SourceDir,
    [Parameter(Mandatory = $true)][string] $OutFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$resolved = (Resolve-Path -LiteralPath $SourceDir).Path
$prefix = $resolved.TrimEnd([char]92) + [char]92

if (Test-Path -LiteralPath $OutFile) { [System.IO.File]::Delete((Resolve-Path -LiteralPath $OutFile).Path) }

$stream = [System.IO.File]::Open($OutFile, [System.IO.FileMode]::CreateNew)
try {
    $zip = New-Object System.IO.Compression.ZipArchive($stream, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $count = 0
        foreach ($file in Get-ChildItem -LiteralPath $resolved -Recurse -File) {
            $relative = $file.FullName.Substring($prefix.Length).Replace([char]92, '/')
            $entry = $zip.CreateEntry($relative, [System.IO.Compression.CompressionLevel]::Optimal)
            $entryStream = $entry.Open()
            try {
                $input = [System.IO.File]::OpenRead($file.FullName)
                try { $input.CopyTo($entryStream) } finally { $input.Dispose() }
            } finally { $entryStream.Dispose() }
            $count++
        }
    } finally { $zip.Dispose() }
} finally { $stream.Dispose() }

$size = [math]::Round((Get-Item -LiteralPath $OutFile).Length / 1MB, 2)
Write-Host ("{0,-34} {1} files, {2} MB" -f (Split-Path $OutFile -Leaf), $count, $size)
