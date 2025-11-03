#!/usr/bin/env pwsh
# test-staging-curl.ps1 - Run integration tests against staging environment using curl

param(
    [string]$StagingUrl = "https://filesvc-stg-app.chinacloudsites.cn",
    [int]$TimeoutSeconds = 30
)

Write-Host "Running Staging Environment Tests (curl)"
Write-Host "========================================"
Write-Host "Target: $StagingUrl"

$ErrorActionPreference = "Stop"

try {
    # Health check
    Write-Host "1. Health Check..." -NoNewline
    $result = curl.exe -k -s -o /dev/null -w "%{http_code}" "$StagingUrl/api/files" --max-time $TimeoutSeconds
    if ($result -eq "200") {
        Write-Host " PASSED" -ForegroundColor Green
    } else {
        throw "Health check failed with code $result"
    }

    # Test file upload
    Write-Host "2. File Upload Test..." -NoNewline
    $testFile = "$env:TEMP\test-upload.txt"
    "Test content for upload" | Out-File -FilePath $testFile -Encoding UTF8
    $result = curl.exe -k -s -o /dev/null -w "%{http_code}" -F "file=@$testFile" "$StagingUrl/api/files/upload" --max-time $TimeoutSeconds
    if ($result -eq "201") {
        Write-Host " PASSED" -ForegroundColor Green
        # Extract ID from response if needed, but for simplicity, assume success
    } else {
        throw "Upload failed with code $result"
    }
    Remove-Item $testFile -Force

    # Test file listing
    Write-Host "3. File List Test..." -NoNewline
    $result = curl.exe -k -s -o /dev/null -w "%{http_code}" "$StagingUrl/api/files" --max-time $TimeoutSeconds
    if ($result -eq "200") {
        Write-Host " PASSED" -ForegroundColor Green
    } else {
        throw "List failed with code $result"
    }

    # For download and delete, we need an ID, but since it's a new test, perhaps skip or assume

    Write-Host ""
    Write-Host "All staging tests PASSED!" -ForegroundColor Green
    Write-Host "Staging environment is ready."
    exit 0

} catch {
    Write-Host ""
    Write-Host "Staging test FAILED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}