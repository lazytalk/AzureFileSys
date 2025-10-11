Param(
  [string]$Port = "5090",
  [string]$SqlitePath = "dev-files.db",
  [switch]$RecreateDb
)

$ErrorActionPreference = 'Stop'

Write-Host "Starting FileService API in Development mode..." -ForegroundColor Cyan

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:BlobStorage__UseLocalStub = 'true'
$env:BlobStorage__ConnectionString = ''
$env:Persistence__UseEf = 'true'
$env:Persistence__SqlitePath = $SqlitePath
$dbFull = Join-Path (Split-Path -Parent $PSScriptRoot) "src\FileService.Api\bin\Debug\net8.0\$SqlitePath"
if ($RecreateDb -and (Test-Path $dbFull)) {
  Write-Host "Recreating SQLite database: $dbFull" -ForegroundColor Yellow
  Remove-Item $dbFull -Force
}
Write-Host "SQLite DB Path (logical): $SqlitePath (will be auto-created if missing)" -ForegroundColor DarkCyan
$env:EnvironmentMode = 'Development'

$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }

Write-Host "Launching API..." -ForegroundColor Cyan
${projPath} = Join-Path (Split-Path -Parent $PSScriptRoot) "src\FileService.Api\FileService.Api.csproj"
& $dotnet run --project $projPath --urls "http://localhost:$Port"
