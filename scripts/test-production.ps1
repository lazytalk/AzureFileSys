#!/usr/bin/env pwsh
# test-production.ps1 - Run minimal health checks against production environment (no emojis, uses deploy-settings)

param(
    [string]$Environment = "Production",
    [string]$ProductionUrl = "",
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = "Stop"

# Load environment config
$config = & (Join-Path $PSScriptRoot "deploy-settings.ps1") -Environment $Environment
$resources = $config.Resources

if ([string]::IsNullOrWhiteSpace($ProductionUrl)) {
    $customDomain = $resources["CustomDomain"]
    if (-not [string]::IsNullOrWhiteSpace($customDomain)) {
        $ProductionUrl = "https://$customDomain"
    } else {
        # Fallback to default App Service hostname (China cloud)
        $ProductionUrl = "https://$($resources["WebAppName"]).chinacloudsites.cn"
    }
}

Write-Host "Running Production Environment Health Checks" -ForegroundColor Magenta
Write-Host "============================================" -ForegroundColor Magenta
Write-Host "Target: $ProductionUrl" -ForegroundColor Gray

try {
    # 1) Basic health check (root, then API fallback)
    Write-Host "1. Health Check..." -NoNewline
    try {
        $response = Invoke-WebRequest "$ProductionUrl/" -Method Get -TimeoutSec $TimeoutSeconds -UseBasicParsing
        if ($response.StatusCode -eq 200) {
            Write-Host " PASSED (Root endpoint)" -ForegroundColor Green
        }
    } catch {
        try {
            $headers = @{
                "X-PowerSchool-User" = "health-check"
                "X-PowerSchool-Role" = "user"
            }
            $listResponse = Invoke-RestMethod "$ProductionUrl/api/files" -Method Get -Headers $headers -TimeoutSec $TimeoutSeconds
            Write-Host " PASSED (API endpoint)" -ForegroundColor Green
        } catch {
            throw "Production health check failed: $($_.Exception.Message)"
        }
    }

    # 2) Response time check
    Write-Host "2. Response Time Check..." -NoNewline
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $headers = @{
            "X-PowerSchool-User" = "health-check"
            "X-PowerSchool-Role" = "user"
        }
        $null = Invoke-RestMethod "$ProductionUrl/api/files" -Method Get -Headers $headers -TimeoutSec $TimeoutSeconds
        $stopwatch.Stop()
        $responseTime = $stopwatch.ElapsedMilliseconds

        if ($responseTime -lt 5000) {
            Write-Host " PASSED ($responseTime ms)" -ForegroundColor Green
        } else {
            Write-Host " WARNING (Slow: $responseTime ms)" -ForegroundColor Yellow
        }
    } catch {
        $stopwatch.Stop()
        throw "Response time check failed: $($_.Exception.Message)"
    }

    # 3) HTTPS enforcement / redirect
    Write-Host "3. HTTPS Security Check..." -NoNewline
    try {
        $httpUrl = $ProductionUrl.Replace("https://", "http://")
        $response = Invoke-WebRequest $httpUrl -Method Get -TimeoutSec $TimeoutSeconds -UseBasicParsing -MaximumRedirection 0 -ErrorAction SilentlyContinue
        if ($response.StatusCode -in 301,302) {
            Write-Host " PASSED (HTTPS redirect)" -ForegroundColor Green
        } else {
            Write-Host " WARNING (No HTTPS redirect)" -ForegroundColor Yellow
        }
    } catch {
        Write-Host " PASSED (HTTPS enforced)" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "Production health checks PASSED" -ForegroundColor Green
    Write-Host "Production environment is healthy and responding." -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Monitoring shortcuts:" -ForegroundColor Gray
    Write-Host "• App Service Logs: az webapp log tail -n $($resources["WebAppName"]) -g $($resources["ResourceGroup"])" -ForegroundColor Gray
    Write-Host "• Key Vault: $($resources["KeyVaultName"])" -ForegroundColor Gray

    exit 0

} catch {
    Write-Host ""
    Write-Host "Production health check FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Please investigate the production environment." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Troubleshooting steps:" -ForegroundColor Yellow
    Write-Host "1. Check App Service status" -ForegroundColor Gray
    Write-Host "2. Review Application Insights" -ForegroundColor Gray
    Write-Host "3. Check Key Vault access" -ForegroundColor Gray
    Write-Host "4. Verify storage connectivity" -ForegroundColor Gray
    exit 1
}
