<#
.SYNOPSIS
  Read-only conformance probe for the Torob API v3 endpoint after a deployment.

.DESCRIPTION
  Sends the request shapes Torob's document allows (and a few it forbids) to the public endpoint and
  records status, Content-Type and the first bytes of every answer next to this script
  (torob-probe-<timestamp>.txt). Nothing is written server-side; the endpoint is a catalogue read.

  Expected after the 2026-09-10 release:
    A..K  -> 200  (page as string/float, extra limit/size, form, multipart, text/plain, no Content-Type,
                   BOM, missing token, cursor mode, bare product URL)
    M     -> 200  page without sort defaults to date_added_desc
    L,N..P -> 400 with body exactly {"error":"..."} (empty body, page 0, broken JSON, form page=abc)
    First product: current_price equals the Toman price on the storefront page, dates like
    2026-09-03T15:35:38+00:00, "count" present, "guarantee" present (null), no omitted optional keys.

.PARAMETER BaseUrl
  Endpoint to probe. Default: the URL registered in the Torob panel.
#>
param(
    [string]$BaseUrl = 'https://vitorize.com/api/v1/thirdparties/torob/products',
    [string]$ProductUrl = 'https://vitorize.com/product/telegram-stars'
)

$ErrorActionPreference = 'Continue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$report = Join-Path $PSScriptRoot "torob-probe-$stamp.txt"
$token = @{ 'X-Torob-Token' = 'probe.non-secret.value'; 'X-Torob-Token-Version' = '1' }

function Probe($label, $method, $body, $contentType, $headers, $expected) {
    $req = [System.Net.HttpWebRequest]::Create($BaseUrl)
    $req.Method = $method; $req.AllowAutoRedirect = $false; $req.Timeout = 30000
    $req.UserAgent = 'torob.com'; $req.Accept = 'application/json'
    if ($headers) { foreach ($k in $headers.Keys) { $req.Headers[$k] = $headers[$k] } }
    if ($null -ne $body) {
        $bytes = if ($body -is [byte[]]) { $body } else { [Text.Encoding]::UTF8.GetBytes([string]$body) }
        if ($contentType) { $req.ContentType = $contentType }
        $req.ContentLength = $bytes.Length
        $s = $req.GetRequestStream(); $s.Write($bytes, 0, $bytes.Length); $s.Close()
    } elseif ($method -eq 'POST') { $req.ContentLength = 0 }
    try { $resp = $req.GetResponse() } catch [System.Net.WebException] { $resp = $_.Exception.Response }
    if ($null -eq $resp) { $line = "$label -> NO RESPONSE (expected $expected)"; Write-Host $line; Add-Content $report $line; return }
    $code = [int]$resp.StatusCode
    $rs = $resp.GetResponseStream(); $rd = New-Object IO.StreamReader($rs, [Text.Encoding]::UTF8); $txt = $rd.ReadToEnd(); $rd.Close(); $resp.Close()
    $verdict = if ($code -eq $expected) { 'OK ' } else { 'MISMATCH' }
    $preview = if ($txt.Length -gt 400) { $txt.Substring(0, 400) + '...' } else { $txt }
    $line = "[$verdict] $label -> $code (expected $expected) CT=$($resp.ContentType)`n    $preview"
    Write-Host $line; Add-Content $report $line
    return $txt
}

Add-Content $report "Torob probe $stamp against $BaseUrl"
$first = Probe 'A page+sort json'            'POST' '{"page": 1, "sort": "date_added_desc"}' 'application/json' $token 200
$null = Probe 'B page as string + limit/size'        'POST' '{"page": "1", "sort": "date_added_desc", "limit": 100, "size": 100}' 'application/json' $token 200
$null = Probe 'C page as float'                      'POST' '{"page": 1.0, "sort": "date_added_desc"}' 'application/json' $token 200
$null = Probe 'D form-urlencoded'                    'POST' 'page=1&sort=date_added_desc' 'application/x-www-form-urlencoded' $token 200
$null = Probe 'E multipart'                          'POST' "--b`r`nContent-Disposition: form-data; name=`"page`"`r`n`r`n1`r`n--b`r`nContent-Disposition: form-data; name=`"sort`"`r`n`r`ndate_added_desc`r`n--b--`r`n" 'multipart/form-data; boundary=b' $token 200
$null = Probe 'F text/plain json'                    'POST' '{"page": 1, "sort": "date_added_desc"}' 'text/plain' $token 200
$null = Probe 'G no content-type'                    'POST' '{"page": 1, "sort": "date_added_desc"}' $null $token 200
$null = Probe 'H json with BOM'                      'POST' ([byte[]]([Text.Encoding]::UTF8.GetPreamble() + [Text.Encoding]::UTF8.GetBytes('{"page": 1, "sort": "date_added_desc"}'))) 'application/json' $token 200
$null = Probe 'I no token'                           'POST' '{"page": 1, "sort": "date_added_desc"}' 'application/json' $null 200
$cursorPage = Probe 'J cursor first page'    'POST' '{"sort": "product_id_desc", "page": 1, "limit": 100}' 'application/json' $token 200
if ($cursorPage) {
    try { $next = (ConvertFrom-Json $cursorPage).next_cursor } catch { $next = $null }
    if ($next) { Probe 'J2 cursor second page' 'POST' ('{"sort": "product_id_desc", "cursor": "' + $next + '"}') 'application/json' $token 200 | Out-Null }
}
$bare = Probe 'K bare product url'           'POST' ('{"page_urls": ["' + $ProductUrl + '"]}') 'application/json' $token 200
$null = Probe 'L empty body'                         'POST' '' 'application/json' $token 400
$null = Probe 'M page without sort'                  'POST' '{"page": 1}' 'application/json' $token 200
$null = Probe 'N page 0'                             'POST' '{"page": 0, "sort": "date_added_desc"}' 'application/json' $token 400
$null = Probe 'O broken json'                        'POST' '{' 'application/json' $token 400
$null = Probe 'P form page=abc'                      'POST' 'page=abc&sort=date_added_desc' 'application/x-www-form-urlencoded' $token 400

Add-Content $report "`n== Response-shape checks on the first page =="
try {
    $root = ConvertFrom-Json $first
    $checks = @(
        "count present and equal to total: $($null -ne $root.count -and $root.count -eq $root.total)",
        "next_cursor key present: $($root.PSObject.Properties.Name -contains 'next_cursor')"
    )
    $p = $root.products[0]
    $keys = ($p.PSObject.Properties.Name -join ',')
    $checks += "first product keys: $keys"
    $checks += "guarantee key present: $($p.PSObject.Properties.Name -contains 'guarantee')"
    $checks += "old_price key present: $($p.PSObject.Properties.Name -contains 'old_price')"
    $checks += "date_added format ok (seconds + offset): $($p.date_added -match '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[+-]\d{2}:\d{2}$') -> $($p.date_added)"
    $checks += "current_price (must equal the Toman price on $($p.page_url)): $($p.current_price)"
    if ($bare) { $checks += "bare product url total: $((ConvertFrom-Json $bare).total)" }
    $checks | ForEach-Object { Write-Output $_; Add-Content $report $_ }
} catch {
    $line = "shape checks skipped: $($_.Exception.Message)"; Write-Host $line; Add-Content $report $line
}
Write-Output "`nReport: $report"
