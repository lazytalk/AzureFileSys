# Start the development app in background and wait for it to be ready
Param(
    [string]$Port = "5090"
)

$ErrorActionPreference = 'Stop'

# Get the dotnet path
$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnetCmd) {
    Write-Host "dotnet not found" -ForegroundColor Red
    exit 1
}

$projPath = Join-Path (Split-Path -Parent $PSScriptRoot) "src\FileService.Api\FileService.Api.csproj"

Write-Host "Starting FileService.Api..." -ForegroundColor Cyan

# Start the app in background and capture output
$process = Start-Process -FilePath $dotnetCmd.Source `
    -ArgumentList @("run", "--project", $projPath, "--no-build", "--urls", "http://localhost:$Port") `
    -NoNewWindow `
    -PassThru

Write-Host "Process started with ID: $($process.Id)" -ForegroundColor Green

# Wait for the app to start listening (up to 30 seconds)
$timeout = 30
$startTime = Get-Date
$maxAttempts = 30

for ($i = 0; $i -lt $maxAttempts; $i++) {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:$Port/swagger" -UseBasicParsing -TimeoutSec 1 -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200 -or $response.StatusCode -eq 401) {
            Write-Host "App is ready!" -ForegroundColor Green
            # Write process ID so debugger knows which one to attach to
            Write-Host "Process ID: $($process.Id)"
            # Keep the script running so the process doesn't terminate
            $process.WaitForExit()
            exit 0
        }
    }
    catch {
        Start-Sleep -Milliseconds 500
    }
}

Write-Host "App failed to start within timeout" -ForegroundColor Red
$process.Kill()
exit 1
