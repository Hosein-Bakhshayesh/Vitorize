[CmdletBinding()]
param(
    [string] $BaseUrl = 'http://127.0.0.1:5077',
    [int] $Requests = 100,
    [int] $Concurrency = 8
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$routes = @('/', '/shop', '/product/e2e-seo-product', '/search?q=e2e', '/sitemaps/products-1.xml')
Add-Type -AssemblyName System.Net.Http
$handler = New-Object System.Net.Http.HttpClientHandler
$handler.AllowAutoRedirect = $false
$client = New-Object System.Net.Http.HttpClient($handler)
$client.Timeout = [TimeSpan]::FromSeconds(30)
$jobs = New-Object System.Collections.Generic.List[object]
try {
    for ($offset = 0; $offset -lt $Requests; $offset += $Concurrency) {
        $batchSize = [Math]::Min($Concurrency, $Requests - $offset)
        $batch = @()
        for ($index = 0; $index -lt $batchSize; $index++) {
            $requestIndex = $offset + $index
            $route = $routes[$requestIndex % $routes.Count]
            $batch += [pscustomobject]@{
                Route = $route
                Started = [DateTime]::UtcNow
                Task = $client.GetAsync($BaseUrl + $route)
            }
        }
        [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($batch.Task))
        $finished = [DateTime]::UtcNow
        foreach ($request in $batch) {
            try {
                $response = $request.Task.Result
                $jobs.Add([pscustomobject]@{ Route=$request.Route; Status=[int]$response.StatusCode; Milliseconds=($finished - $request.Started).TotalMilliseconds; Error=$null })
                $response.Dispose()
            }
            catch {
                $jobs.Add([pscustomobject]@{ Route=$request.Route; Status=0; Milliseconds=($finished - $request.Started).TotalMilliseconds; Error=$_.Exception.Message })
            }
        }
    }
}
finally {
    $client.Dispose()
    $handler.Dispose()
}

$ordered = @($jobs.Milliseconds | Sort-Object)
$p95 = $ordered[[Math]::Min($ordered.Count - 1, [Math]::Floor($ordered.Count * 0.95))]
$p99 = $ordered[[Math]::Min($ordered.Count - 1, [Math]::Floor($ordered.Count * 0.99))]
$errors = @($jobs | Where-Object { $_.Status -lt 200 -or $_.Status -ge 400 }).Count
$summary = [pscustomobject]@{
    Requests = $Requests
    Concurrency = $Concurrency
    ErrorCount = $errors
    ErrorRate = [Math]::Round($errors / [double]$Requests, 4)
    AverageMs = [Math]::Round(($jobs | Measure-Object Milliseconds -Average).Average, 2)
    P95Ms = [Math]::Round($p95, 2)
    P99Ms = [Math]::Round($p99, 2)
}
$summary | Format-List
if ($errors -gt 0) { $jobs | Where-Object { $_.Status -lt 200 -or $_.Status -ge 400 } | Format-Table; throw 'Load smoke test observed failed responses.' }
