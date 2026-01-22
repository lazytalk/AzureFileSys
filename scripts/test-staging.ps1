#!/usr/bin/env pwsh
# test-staging.ps1 - Run integration tests against staging environment (emoji-free, aligned to begin-upload flow)

param(
    [string]$Environment = "Staging",
    [string]$StagingUrl = "",
    [int]$TimeoutSeconds = 30
)

# Load environment config from deploy-settings.ps1
$config = & (Join-Path $PSScriptRoot "deploy-settings.ps1") -Environment $Environment
$resources = $config.Resources

if ([string]::IsNullOrWhiteSpace($StagingUrl)) {
    $customDomain = $resources["CustomDomain"]
    if (-not [string]::IsNullOrWhiteSpace($customDomain)) {
        $StagingUrl = "https://$customDomain"
    } else {
        # Fall back to default App Service hostname (China cloud)
        $StagingUrl = "https://$($resources["WebAppName"]).chinacloudsites.cn"
    }
}

$ErrorActionPreference = "Stop"

Write-Host "Running Staging Environment Tests" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host "Target: $StagingUrl" -ForegroundColor Gray

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )
    if (-not $Condition) { throw $Message }
}

try {
    # 1. Health check (Swagger JSON)
    Write-Host "1. Health Check..." -NoNewline
    $health = Invoke-RestMethod "$StagingUrl/swagger/v1/swagger.json" -Method Get -TimeoutSec $TimeoutSeconds
    Assert-True ($null -ne $health) "Health check failed"
    Write-Host " PASSED" -ForegroundColor Green

    # Common headers for auth
    $headers = @{
        "X-PowerSchool-User" = "staging-test-user"
        "X-PowerSchool-Role" = "admin"
    }

    # 2. Begin upload
    Write-Host "2. Begin Upload..." -NoNewline
    $testContent = "Staging test file - $(Get-Date)"
    $testFileName = "staging-test-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
    $testBytes = [System.Text.Encoding]::UTF8.GetBytes($testContent)

    $beginBody = @{
        fileName = $testFileName
        sizeBytes = $testBytes.Length
        contentType = "text/plain"
    } | ConvertTo-Json

    $beginRes = Invoke-RestMethod "$StagingUrl/api/files/begin-upload" -Method Post -Body $beginBody -ContentType "application/json" -Headers $headers -TimeoutSec $TimeoutSeconds
    Assert-True ($beginRes -and $beginRes.fileId -and $beginRes.uploadUrl) "begin-upload response invalid"
    $fileId = [string]$beginRes.fileId
    $uploadUrl = [string]$beginRes.uploadUrl
    Write-Host " PASSED (FileId: $fileId)" -ForegroundColor Green

    # 3. PUT to blob
    Write-Host "3. Upload to Blob..." -NoNewline
    $tmpPath = [System.IO.Path]::GetTempFileName()
    try {
        [System.IO.File]::WriteAllBytes($tmpPath, $testBytes)
        $blobHeaders = @{
            "x-ms-blob-type" = "BlockBlob"
            "x-ms-blob-content-type" = "text/plain"
        }
        Invoke-WebRequest -Uri $uploadUrl -Method Put -InFile $tmpPath -ContentType "text/plain" -Headers $blobHeaders -TimeoutSec $TimeoutSeconds -UseBasicParsing | Out-Null
    } finally {
        Remove-Item $tmpPath -Force -ErrorAction SilentlyContinue
    }
    Write-Host " PASSED" -ForegroundColor Green

    # 4. Complete upload
    Write-Host "4. Complete Upload..." -NoNewline
    $completeRes = Invoke-RestMethod "$StagingUrl/api/files/complete-upload/$fileId" -Method Post -Headers $headers -TimeoutSec $TimeoutSeconds
    Assert-True ($completeRes -and $completeRes.Status -eq "Available") "complete-upload failed"
    Write-Host " PASSED" -ForegroundColor Green

    # 5. List files
    Write-Host "5. List Files..." -NoNewline
    $listRes = Invoke-RestMethod "$StagingUrl/api/files?all=true" -Method Get -Headers $headers -TimeoutSec $TimeoutSeconds
    $count = if ($listRes -is [System.Array]) { $listRes.Count } elseif ($listRes) { 1 } else { 0 }
    $match = $false
    foreach ($item in $listRes) {
        if ([Guid]$item.id -eq ([Guid]$fileId)) { $match = $true; break }
    }
    if (-not $match) {
        Write-Host " List response (debug):" -ForegroundColor Yellow
        Write-Host ($listRes | ConvertTo-Json -Depth 5)
        throw "Uploaded file not found in list"
    }
    Write-Host " PASSED (Count: $count)" -ForegroundColor Green

    # 6. Get file (download URL)
    Write-Host "6. Get File..." -NoNewline
    $getRes = Invoke-RestMethod "$StagingUrl/api/files/$fileId" -Method Get -Headers $headers -TimeoutSec $TimeoutSeconds
    Assert-True ($getRes -and $getRes.DownloadUrl) "Get file failed"
    Write-Host " PASSED" -ForegroundColor Green

    # 7. Delete file
    Write-Host "7. Delete File..." -NoNewline
    Invoke-RestMethod "$StagingUrl/api/files/$fileId" -Method Delete -Headers $headers -TimeoutSec $TimeoutSeconds | Out-Null
    Write-Host " PASSED" -ForegroundColor Green

    # 8. Verify deletion
    Write-Host "8. Verify Deletion..." -NoNewline
    $deleted = $false
    try {
        Invoke-RestMethod "$StagingUrl/api/files/$fileId" -Method Get -Headers $headers -TimeoutSec $TimeoutSeconds | Out-Null
    } catch {
        if ($_.Exception.Response.StatusCode -eq 404) { $deleted = $true }
    }
    Assert-True $deleted "File still exists after delete"
    Write-Host " PASSED" -ForegroundColor Green

    Write-Host ""
    Write-Host "All staging tests PASSED" -ForegroundColor Green
    exit 0

} catch {
    Write-Host ""
    Write-Host "Staging test FAILED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
