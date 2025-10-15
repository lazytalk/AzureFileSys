Param(
  [string]$SqlitePath = "dev-files.db"
)

# Computes the same logical path scripts/dev-run.ps1 uses (bin Debug net8.0)
$projectBin = Join-Path (Split-Path -Parent $PSScriptRoot) "src\FileService.Api\bin\Debug\net8.0"
$dbFull = Join-Path $projectBin $SqlitePath

if (-not (Test-Path $dbFull)) {
    Write-Host "SQLite file not found at: $dbFull" -ForegroundColor Yellow
    Write-Host "If you expect a DB, make sure the app was started with EF enabled (not Development mode) and migrations ran."
    exit 1
}

Write-Host "Opening SQLite DB at: $dbFull"
# Attempt to open with the default app
Start-Process -FilePath $dbFull
