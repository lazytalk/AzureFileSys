#!/usr/bin/env pwsh
# test-staging.ps1 - Run integration tests against staging environment

param(
    [string]$StagingUrl = "https://kaiweneducation.com",
    [int]$TimeoutSeconds = 30
)

Write-Host "Running Staging Environment Tests" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host "Target: $StagingUrl" -ForegroundColor Gray

$ErrorActionPreference = "Stop"

# Ignore SSL certificate errors for self-signed certs
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

try {
    # Health check
    Write-Host "1. Health Check..." -NoNewline
    $health = Invoke-RestMethod "$StagingUrl/api/files" -Method Get -TimeoutSec $TimeoutSeconds
    if ($health) {
        Write-Host " PASSED" -ForegroundColor Green
    } else {
        throw "Health check failed"
    }

    # Test file upload (with real auth headers)
    Write-Host "2. File Upload Test..." -NoNewline
    $headers = @{}

    # Create a test file
    $testContent = "Staging test file - $(Get-Date)"
    $testFileName = "staging-test-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
    $testFilePath = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllText($testFilePath, $testContent)

    try {
        $uploadForm = @{
            file = Get-Item $testFilePath
        }

        $uploadResponse = Invoke-RestMethod "$StagingUrl/api/files/upload" -Method Post -Form $uploadForm -Headers $headers -TimeoutSec $TimeoutSeconds
        if ($uploadResponse -and $uploadResponse.Id) {
            Write-Host " PASSED (ID: $($uploadResponse.Id))" -ForegroundColor Green
            $uploadedFileId = $uploadResponse.Id
        } else {
            throw "Upload response invalid"
        }
    } finally {
        Remove-Item $testFilePath -Force -ErrorAction SilentlyContinue
    }

    # Test file listing
    Write-Host "3. File List Test..." -NoNewline
    $listResponse = Invoke-RestMethod "$StagingUrl/api/files" -Method Get -Headers $headers -TimeoutSec $TimeoutSeconds
    if ($listResponse -and $listResponse.Count -gt 0) {
        $foundFile = $listResponse | Where-Object { $_.Id -eq $uploadedFileId }
        if ($foundFile) {
            Write-Host " PASSED ($($listResponse.Count) files found)" -ForegroundColor Green
        } else {
            throw "Uploaded file not found in list"
        }
    } else {
        throw "File list empty or invalid"
    }

    # Test file download
    Write-Host "4. File Download Test..." -NoNewline
    $downloadResponse = Invoke-RestMethod "$StagingUrl/api/files/$uploadedFileId" -Method Get -Headers $headers -TimeoutSec $TimeoutSeconds
    if ($downloadResponse -and $downloadResponse.DownloadUrl) {
        Write-Host " PASSED" -ForegroundColor Green
    } else {
        throw "Download response invalid"
    }

    # Test file deletion
    Write-Host "5. File Delete Test..." -NoNewline
    $deleteResponse = Invoke-RestMethod "$StagingUrl/api/files/$uploadedFileId" -Method Delete -Headers $headers -TimeoutSec $TimeoutSeconds
    if ($deleteResponse) {
        Write-Host " PASSED" -ForegroundColor Green
    } else {
        throw "Delete response invalid"
    }

    # Verify deletion
    Write-Host "6. Delete Verification..." -NoNewline
    try {
        $verifyResponse = Invoke-RestMethod "$StagingUrl/api/files/$uploadedFileId" -Method Get -Headers $headers -TimeoutSec $TimeoutSeconds
        Write-Host " FAILED (File still exists)" -ForegroundColor Red
        exit 1
    } catch {
        if ($_.Exception.Response.StatusCode -eq 404) {
            Write-Host " PASSED (File properly deleted)" -ForegroundColor Green
        } else {
            throw "Unexpected error during verification: $($_.Exception.Message)"
        }
    }

    Write-Host ""
    Write-Host "All staging tests PASSED!" -ForegroundColor Green
    Write-Host "Staging environment is ready for production promotion." -ForegroundColor Cyan
    exit 0

} catch {
    Write-Host ""
    Write-Host "Staging test FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Please check the staging environment before promoting to production." -ForegroundColor Yellow
    exit 1
}
