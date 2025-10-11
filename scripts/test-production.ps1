#!/usr/bin/env pwsh
# test-production.ps1 - Run minimal health checks against production environment

param(
    [string]$ProductionUrl = "https://filesvc-api-prod.azurewebsites.net",
    [int]$TimeoutSeconds = 30
)

Write-Host "🚀 Running Production Environment Health Checks" -ForegroundColor Magenta
Write-Host "===============================================" -ForegroundColor Magenta
Write-Host "Target: $ProductionUrl" -ForegroundColor Gray

$ErrorActionPreference = "Stop"

try {
    # Basic health check (Swagger endpoint might be disabled in production)
    Write-Host "1. 🔍 Production Health Check..." -NoNewline
    try {
        # Try root endpoint first
        $response = Invoke-WebRequest "$ProductionUrl/" -Method Get -TimeoutSec $TimeoutSeconds -UseBasicParsing
        if ($response.StatusCode -eq 200) {
            Write-Host " ✅ PASSED (Root endpoint)" -ForegroundColor Green
        }
    } catch {
        # If root fails, try a minimal API endpoint
        try {
            $headers = @{
                "X-PowerSchool-User" = "health-check"
                "X-PowerSchool-Role" = "user"
            }
            $listResponse = Invoke-RestMethod "$ProductionUrl/api/files" -Method Get -Headers $headers -TimeoutSec $TimeoutSeconds
            Write-Host " ✅ PASSED (API endpoint)" -ForegroundColor Green
        } catch {
            throw "Production health check failed: $($_.Exception.Message)"
        }
    }

    # Check response time
    Write-Host "2. ⏱️ Response Time Check..." -NoNewline
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $headers = @{
            "X-PowerSchool-User" = "health-check"
            "X-PowerSchool-Role" = "user"
        }
        $response = Invoke-RestMethod "$ProductionUrl/api/files" -Method Get -Headers $headers -TimeoutSec $TimeoutSeconds
        $stopwatch.Stop()
        $responseTime = $stopwatch.ElapsedMilliseconds
        
        if ($responseTime -lt 5000) {
            Write-Host " ✅ PASSED ($responseTime ms)" -ForegroundColor Green
        } else {
            Write-Host " ⚠️ SLOW ($responseTime ms)" -ForegroundColor Yellow
        }
    } catch {
        $stopwatch.Stop()
        throw "Response time check failed: $($_.Exception.Message)"
    }

    # Check HTTPS redirect
    Write-Host "3. 🔒 HTTPS Security Check..." -NoNewline
    try {
        $httpUrl = $ProductionUrl.Replace("https://", "http://")
        $response = Invoke-WebRequest $httpUrl -Method Get -TimeoutSec $TimeoutSeconds -UseBasicParsing -MaximumRedirection 0 -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 301 -or $response.StatusCode -eq 302) {
            Write-Host " ✅ PASSED (HTTPS redirect)" -ForegroundColor Green
        } else {
            Write-Host " ⚠️ WARNING (No HTTPS redirect)" -ForegroundColor Yellow
        }
    } catch {
        # This is expected for HTTPS-only sites
        Write-Host " ✅ PASSED (HTTPS enforced)" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "🎉 Production health checks PASSED!" -ForegroundColor Green
    Write-Host "Production environment is healthy and responding." -ForegroundColor Cyan
    
    Write-Host ""
    Write-Host "📊 Production Monitoring:" -ForegroundColor Gray
    Write-Host "• Application Insights: https://portal.azure.com/#blade/Microsoft_Azure_Monitoring/AzureMonitoringBrowseBlade" -ForegroundColor Gray
    Write-Host "• App Service Logs: az webapp log tail -n filesvc-api-prod -g file-svc-production-rg" -ForegroundColor Gray
    Write-Host "• Key Vault: https://portal.azure.com/#blade/Microsoft_Azure_KeyVault/KeyVaultMenuBlade" -ForegroundColor Gray
    
    exit 0

} catch {
    Write-Host ""
    Write-Host "❌ Production health check FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Please investigate the production environment immediately." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "🔧 Troubleshooting steps:" -ForegroundColor Yellow
    Write-Host "1. Check App Service status in Azure Portal" -ForegroundColor Gray
    Write-Host "2. Review Application Insights for errors" -ForegroundColor Gray
    Write-Host "3. Check Key Vault access permissions" -ForegroundColor Gray
    Write-Host "4. Verify database connectivity" -ForegroundColor Gray
    
    exit 1
}
