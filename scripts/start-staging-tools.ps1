param(
    [int]$Port = 8080
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Push-Location "$PSScriptRoot\..\tests\tools"
try {
    Write-Host "Serving staging-tools.html on http://localhost:$Port" -ForegroundColor Cyan
    Start-Process "http://localhost:$Port/staging-tools.html"

    # Prefer Python if available
    $python = Get-Command python -ErrorAction SilentlyContinue
    if ($python) {
        Write-Host "Starting python -m http.server $Port ..." -ForegroundColor Yellow
        python -m http.server $Port
        return
    }

    # Fallback to PowerShell static server (simple)
    Write-Host "Python not found. Starting simple PowerShell static server..." -ForegroundColor Yellow
    $listener = [System.Net.HttpListener]::new()
    $prefix = "http://localhost:$Port/"
    $listener.Prefixes.Add($prefix)
    $listener.Start()
    Write-Host "Listening on $prefix (Ctrl+C to stop)" -ForegroundColor Green
    while ($listener.IsListening) {
        $context = $listener.GetContext()
        $path = $context.Request.Url.AbsolutePath.TrimStart('/')
        if ([string]::IsNullOrWhiteSpace($path)) { $path = 'index.html' }
        $fullPath = Join-Path (Get-Location) $path
        if (-not (Test-Path $fullPath)) {
            $context.Response.StatusCode = 404
            $bytes = [System.Text.Encoding]::UTF8.GetBytes("Not Found")
            $context.Response.OutputStream.Write($bytes,0,$bytes.Length)
            $context.Response.Close()
            continue
        }
        $bytes = [System.IO.File]::ReadAllBytes($fullPath)
        $context.Response.ContentType = 'text/html'
        $context.Response.ContentLength64 = $bytes.Length
        $context.Response.OutputStream.Write($bytes,0,$bytes.Length)
        $context.Response.Close()
    }
} finally {
    Pop-Location
}