[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][uri] $BaseUrl,
    [Parameter(Mandatory = $true)][string] $OutputCsv,
    [ValidateRange(1, 5000)][int] $MaxPages = 1000,
    [ValidateRange(2, 60)][int] $RequestTimeoutSeconds = 10
)

$ErrorActionPreference = 'Stop'
$origin = $BaseUrl.GetLeftPart([System.UriPartial]::Authority)
$queue = [System.Collections.Generic.Queue[uri]]::new()
$seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$rows = [System.Collections.Generic.List[object]]::new()
$queue.Enqueue($BaseUrl)

while ($queue.Count -gt 0 -and $seen.Count -lt $MaxPages) {
    $url = $queue.Dequeue()
    $builder = [System.UriBuilder]::new($url)
    $builder.Query = ''
    $builder.Fragment = ''
    $clean = $builder.Uri.AbsoluteUri.TrimEnd('/')
    if (-not $seen.Add($clean)) { continue }

    try {
        $response = Invoke-WebRequest -Uri $clean -UseBasicParsing -MaximumRedirection 0 -TimeoutSec $RequestTimeoutSeconds -ErrorAction Stop
        $title = if ($response.Content -match '(?is)<title[^>]*>(.*?)</title>') { [Net.WebUtility]::HtmlDecode($matches[1]).Trim() } else { '' }
        $canonical = if ($response.Content -match '(?is)<link[^>]+rel=["'']canonical["''][^>]+href=["'']([^"'']+)') { $matches[1] } else { '' }
        $path = ([uri]$clean).AbsolutePath
        $type = switch -Regex ($path) {
            '^/product/' { 'Product'; break }
            '^/category/' { 'Category'; break }
            '^/brand/' { 'Brand'; break }
            '^/blog/' { 'Blog'; break }
            default { 'Page' }
        }
        $rows.Add([pscustomobject]@{ LegacyUrl = $clean; Path = $path; Type = $type; Status = [int]$response.StatusCode; Title = $title; Canonical = $canonical; ProposedDestination = ''; Decision = 'Review' })

        foreach ($link in $response.Links) {
            if ([string]::IsNullOrWhiteSpace($link.href)) { continue }
            try { $next = [uri]::new($url, $link.href) } catch { continue }
            if ($next.GetLeftPart([System.UriPartial]::Authority) -ne $origin) { continue }
            if ($next.Scheme -notin @('http', 'https')) { continue }
            $queue.Enqueue($next)
        }
    }
    catch {
        $status = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
        $rows.Add([pscustomobject]@{ LegacyUrl = $clean; Path = ([uri]$clean).AbsolutePath; Type = 'Unknown'; Status = $status; Title = ''; Canonical = ''; ProposedDestination = ''; Decision = 'Review' })
    }
}

$directory = Split-Path -Parent $OutputCsv
if ($directory) { [IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($directory)) | Out-Null }
$rows | Sort-Object LegacyUrl -Unique | Export-Csv -LiteralPath $OutputCsv -NoTypeInformation -Encoding utf8
Write-Host "Inventory written to $OutputCsv ($($rows.Count) rows). No site or database data was modified."
