Param(
  [string]$SqlitePath = "files.db"
)

$ErrorActionPreference = 'Stop'
$env:Persistence__SqlitePath = $SqlitePath
$env:Persistence__UseEf = 'true'
$env:EnvironmentMode = 'Development'
$env:Persistence__AutoMigrate = 'false'

$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }

Write-Host "Applying migrations to $SqlitePath ..." -ForegroundColor Cyan
${infraProj} = Join-Path (Split-Path -Parent $PSScriptRoot) "src\FileService.Infrastructure\FileService.Infrastructure.csproj"
${apiProj} = Join-Path (Split-Path -Parent $PSScriptRoot) "src\FileService.Api\FileService.Api.csproj"
& $dotnet ef database update -p $infraProj -s $apiProj
Write-Host "Done." -ForegroundColor Green
