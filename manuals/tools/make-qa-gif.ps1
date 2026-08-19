<#
  Builds a visible, multi-frame QA GIF for loading-media testing using the platform GIF encoder
  (GifBitmapEncoder), so the pixel data is guaranteed valid and actually renders. A Netscape
  looping block is appended afterwards so browsers animate it rather than showing one frame.
#>
param([string] $OutFile = 'D:\Vitorize\manuals\tmp\qa-loading.gif', [int] $Size = 240)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase, System.Drawing

$frames = 6
$encoder = New-Object System.Windows.Media.Imaging.GifBitmapEncoder

for ($i = 0; $i -lt $frames; $i++) {
    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::FromArgb(11, 59, 57))          # deep teal backdrop

    # Expanding ring + core, clearly different per frame.
    $r = 40 + ($i * 14)
    $penW = 14
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(45, 212, 191), $penW)
    $g.DrawEllipse($pen, ($Size/2 - $r), ($Size/2 - $r), ($r*2), ($r*2))
    $core = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(13, 148, 136))
    $cr = 34
    $g.FillEllipse($core, ($Size/2 - $cr), ($Size/2 - $cr), ($cr*2), ($cr*2))
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $font = New-Object System.Drawing.Font('Segoe UI', 26, [System.Drawing.FontStyle]::Bold)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString('V', $font, $white, (New-Object System.Drawing.RectangleF(0, 0, $Size, $Size)), $sf)

    $pen.Dispose(); $core.Dispose(); $white.Dispose(); $font.Dispose(); $g.Dispose()

    $hbmp = $bmp.GetHbitmap()
    $src = [System.Windows.Interop.Imaging]::CreateBitmapSourceFromHBitmap(
        $hbmp, [IntPtr]::Zero, [System.Windows.Int32Rect]::Empty,
        [System.Windows.Media.Imaging.BitmapSizeOptions]::FromEmptyOptions())
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($src))
    [void][Vitorize.Native]::DeleteObject($hbmp)
    $bmp.Dispose()
}

$ms = New-Object System.IO.MemoryStream
$encoder.Save($ms)
$bytes = $ms.ToArray()
$ms.Dispose()

# Insert the Netscape 2.0 looping extension right after the global colour table so the
# multi-frame GIF actually animates in a browser.
$app = [byte[]](0x21,0xFF,0x0B) + [Text.Encoding]::ASCII.GetBytes('NETSCAPE2.0') + [byte[]](0x03,0x01,0x00,0x00,0x00)
$gctFlag = $bytes[10]
$gctSize = if ($gctFlag -band 0x80) { 3 * [Math]::Pow(2, ($gctFlag -band 0x07) + 1) } else { 0 }
$insertAt = 13 + [int]$gctSize
$out = New-Object 'byte[]' ($bytes.Length + $app.Length)
[Array]::Copy($bytes, 0, $out, 0, $insertAt)
[Array]::Copy($app, 0, $out, $insertAt, $app.Length)
[Array]::Copy($bytes, $insertAt, $out, $insertAt + $app.Length, $bytes.Length - $insertAt)
[IO.File]::WriteAllBytes($OutFile, $out)

$final = [IO.File]::ReadAllBytes($OutFile)
$gce = 0
for ($i = 0; $i -lt $final.Length - 1; $i++) { if ($final[$i] -eq 0x21 -and $final[$i+1] -eq 0xF9) { $gce++ } }
"written : $OutFile"
"size    : $([Math]::Round($final.Length/1KB,1)) KB"
"sig     : $([Text.Encoding]::ASCII.GetString($final[0..5]))"
"frames  : $($encoder.Frames.Count) encoded / $gce graphic-control blocks"
"looping : $([bool]([Text.Encoding]::ASCII.GetString($final[0..200]) -match 'NETSCAPE2.0'))"
"dims    : ${Size}x${Size}"
