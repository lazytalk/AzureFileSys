Param(
  [string]$Port = "5090"
)

$ErrorActionPreference = 'Stop'

Write-Host "Checking prerequisites..." -ForegroundColor Cyan

# Helper function to validate .NET 8 SDK
function Assert-DotNet8Required {
    $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnetCmd) {
        Write-Host ".NET SDK not found." -ForegroundColor Red
        Write-Host "Please install .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
        exit 1
    }
    
    $versionOutput = & $dotnetCmd --version 2>&1
    if ($versionOutput -match '^(\d+)\.') {
        $majorVersion = [int]$matches[1]
        if ($majorVersion -lt 8) {
            Write-Host ".NET SDK version $versionOutput found, but .NET 8 or higher is required." -ForegroundColor Red
            Write-Host "Please install .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
            exit 1
        }
    } else {
        Write-Host "Unable to determine .NET SDK version." -ForegroundColor Red
        exit 1
    }
    return $dotnetCmd.Source
}

# Verify .NET 8 SDK is available
$dotnetPath = Assert-DotNet8Required
Write-Host ".NET SDK version check passed" -ForegroundColor Green

# Load development configuration from deploy-settings.ps1
$configPath = Join-Path $PSScriptRoot "deploy-settings.ps1"
$config = & $configPath -Environment "Development"
$appSettings = $config.AppSettings

# Set environment variables for local development
foreach ($key in $appSettings.Keys) {
    Set-Item -Path "env:$key" -Value $appSettings[$key]
}

Write-Host "Configuring for in-memory storage in development mode" -ForegroundColor Cyan

Write-Host "Launching API..." -ForegroundColor Cyan
$projPath = Join-Path (Split-Path -Parent $PSScriptRoot) "src\FileService.Api\FileService.Api.csproj"

# Build the project first
Write-Host "Building project..." -ForegroundColor Cyan
& $dotnetPath build $projPath -c Debug

# Launch with hot-reload enabled
Write-Host "Now listening on: http://localhost:$Port" -ForegroundColor Green
& $dotnetPath watch run --project $projPath --urls "http://localhost:$Port"

