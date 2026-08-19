<#
.SYNOPSIS
    Repairs the bundled default Vitorize logo: removes its baked-in opaque white background and
    trims the excess canvas padding.

.DESCRIPTION
    wwwroot/images/logo.png shipped as 24bpp RGB (no alpha) on a solid white background, with the
    mark occupying only ~50% of a 512x512 canvas. Rendered at the header's 42x42 box that reads as
    a blank white tile rather than a logo, and it is the asset every fresh deployment falls back to
    because all branding settings seed empty.

    This keeps the exact same artwork - it only un-composites the white matte into an alpha channel
    and crops to the mark. No new branding is introduced.

.PARAMETER Source     Input PNG.
.PARAMETER Destination Output PNG (32bpp ARGB).
.PARAMETER Padding    Fraction of the mark size to keep as breathing room. Default 0.08.
#>
param(
    [Parameter(Mandatory = $true)][string] $Source,
    [Parameter(Mandatory = $true)][string] $Destination,
    [double] $Padding = 0.08
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Load through memory so the file handle is released and the source can be overwritten in place.
$bytes = [System.IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Source).Path)
$ms = New-Object System.IO.MemoryStream (,$bytes)
$src = [System.Drawing.Bitmap]::FromStream($ms)
try {
    # 1. Locate the mark: anything that is not effectively white.
    $minX = $src.Width; $maxX = -1; $minY = $src.Height; $maxY = -1
    for ($y = 0; $y -lt $src.Height; $y++) {
        for ($x = 0; $x -lt $src.Width; $x++) {
            $c = $src.GetPixel($x, $y)
            if (-not ($c.R -gt 240 -and $c.G -gt 240 -and $c.B -gt 240)) {
                if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    if ($maxX -lt 0) { throw 'No non-white pixels found; refusing to produce an empty logo.' }

    # 2. Square the crop around the mark so the glyph is centred and fills the frame.
    $w = $maxX - $minX + 1; $h = $maxY - $minY + 1
    $side = [Math]::Max($w, $h)
    $pad = [int][Math]::Round($side * $Padding)
    $side += $pad * 2
    $cx = $minX + [int]($w / 2); $cy = $minY + [int]($h / 2)
    $left = $cx - [int]($side / 2); $top = $cy - [int]($side / 2)

    $out = New-Object System.Drawing.Bitmap($side, $side, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        for ($y = 0; $y -lt $side; $y++) {
            for ($x = 0; $x -lt $side; $x++) {
                $sx = $left + $x; $sy = $top + $y
                if ($sx -lt 0 -or $sy -lt 0 -or $sx -ge $src.Width -or $sy -ge $src.Height) {
                    $out.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0)); continue
                }
                $c = $src.GetPixel($sx, $sy)
                # Un-composite the white matte: alpha from how far the pixel is from white,
                # colour recovered so anti-aliased edges stay clean instead of turning grey.
                $lum = [Math]::Min([Math]::Min($c.R, $c.G), $c.B)
                $a = 255 - $lum
                if ($a -le 0) { $out.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0)); continue }
                $af = $a / 255.0
                $r = [int][Math]::Round([Math]::Max(0, [Math]::Min(255, ($c.R - 255 * (1 - $af)) / $af)))
                $g = [int][Math]::Round([Math]::Max(0, [Math]::Min(255, ($c.G - 255 * (1 - $af)) / $af)))
                $b = [int][Math]::Round([Math]::Max(0, [Math]::Min(255, ($c.B - 255 * (1 - $af)) / $af)))
                $out.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($a, $r, $g, $b))
            }
        }
        $tmp = [System.IO.Path]::GetTempFileName() + '.png'
        $out.Save($tmp, [System.Drawing.Imaging.ImageFormat]::Png)
        Copy-Item -LiteralPath $tmp -Destination $Destination -Force
        Remove-Item -LiteralPath $tmp -Force
        Write-Host ("repaired: {0}x{1} -> {2}x{2} ARGB, mark now fills {3}% of the frame" -f `
            $src.Width, $src.Height, $side, [Math]::Round(($w / $side) * 100))
    } finally { $out.Dispose() }
} finally { $src.Dispose() }
