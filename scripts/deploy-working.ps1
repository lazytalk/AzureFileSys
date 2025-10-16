#!/usr/bin/env pwsh
# Working Azure China deployment script with Table Storage
# This script deploys the File Service to Azure China Cloud

param(
    [string]$SubscriptionId,
    [string]$Location = "chinaeast",
    [string]$ResourcePrefix = "filesvc-stg"
)

$ResourceGroup = "${ResourcePrefix}-rg"
$storageAccount = "filesvcstg" + $Location
$keyVaultName = "${ResourcePrefix}-kv" 
$appInsightsName = "${ResourcePrefix}-ai"
$webAppName = "${ResourcePrefix}-app"
$appServicePlan = "${ResourcePrefix}-plan"
$containerName = "userfiles-staging"

Write-Host "=== Azure File Service Deployment (China Cloud + Table Storage) ===" -ForegroundColor Cyan

# Step 1: Setup Azure China Cloud
Write-Host "Setting up Azure China Cloud..." -ForegroundColor Yellow
az cloud set --name AzureChinaCloud

if (-not $SubscriptionId) {
    $SubscriptionId = az account show --query id --output tsv
}

az account set --subscription $SubscriptionId
$subscriptionName = az account show --query name --output tsv
Write-Host "Using subscription: $subscriptionName" -ForegroundColor Green

Write-Host "Configuration:" -ForegroundColor Gray
Write-Host "  Resource Group: $ResourceGroup"
Write-Host "  Location: $Location"
Write-Host "  Storage Account: $storageAccount"
Write-Host "  Web App: $webAppName"
Write-Host ""

# Step 2: Create Resource Group
Write-Host "Creating Resource Group..." -ForegroundColor Yellow
$rgExists = az group exists --name $ResourceGroup
if ($rgExists -eq "true") {
    Write-Host "Resource group already exists" -ForegroundColor Green
} else {
    az group create --name $ResourceGroup --location $Location | Out-Null
    Write-Host "Resource group created" -ForegroundColor Green
}

# Step 3: Create Storage Account
Write-Host "Creating Storage Account..." -ForegroundColor Yellow
$storageExists = az storage account show --name $storageAccount --resource-group $ResourceGroup 2>$null
if ($storageExists) {
    Write-Host "Storage account already exists" -ForegroundColor Green
} else {
    az storage account create --name $storageAccount --resource-group $ResourceGroup --location $Location --sku Standard_LRS --kind StorageV2 | Out-Null
    Write-Host "Storage account created" -ForegroundColor Green
}

$storageConnString = az storage account show-connection-string --name $storageAccount --resource-group $ResourceGroup --output tsv

# Step 4: Create Key Vault
Write-Host "Creating Key Vault..." -ForegroundColor Yellow
$kvExists = az keyvault show --name $keyVaultName 2>$null
if ($kvExists) {
    Write-Host "Key Vault already exists" -ForegroundColor Green
} else {
    az keyvault create --name $keyVaultName --resource-group $ResourceGroup --location $Location | Out-Null
    Start-Sleep -Seconds 10
    Write-Host "Key Vault created" -ForegroundColor Green
}

# Step 5: Store secrets
Write-Host "Storing secrets..." -ForegroundColor Yellow
az keyvault secret set --vault-name $keyVaultName --name "BlobStorage--ConnectionString" --value $storageConnString | Out-Null
az keyvault secret set --vault-name $keyVaultName --name "TableStorage--ConnectionString" --value $storageConnString | Out-Null
Write-Host "Secrets stored" -ForegroundColor Green

# Step 6: Create App Service Plan
Write-Host "Creating App Service Plan..." -ForegroundColor Yellow
$planExists = az appservice plan show --name $appServicePlan --resource-group $ResourceGroup 2>$null
if ($planExists) {
    Write-Host "App Service Plan already exists" -ForegroundColor Green
} else {
    az appservice plan create --name $appServicePlan --resource-group $ResourceGroup --sku B1 | Out-Null
    Write-Host "App Service Plan created" -ForegroundColor Green
}

# Step 7: Create Web App
Write-Host "Creating Web App..." -ForegroundColor Yellow
$webExists = az webapp show --name $webAppName --resource-group $ResourceGroup 2>$null
if ($webExists) {
    Write-Host "Web App already exists" -ForegroundColor Green
} else {
    az webapp create --name $webAppName --resource-group $ResourceGroup --plan $appServicePlan --runtime "dotnet:8" | Out-Null
    Write-Host "Web App created" -ForegroundColor Green
}

# Step 8: Configure Managed Identity
Write-Host "Configuring Managed Identity..." -ForegroundColor Yellow
$identity = az webapp identity show --name $webAppName --resource-group $ResourceGroup --query principalId --output tsv 2>$null
if (-not $identity) {
    az webapp identity assign --name $webAppName --resource-group $ResourceGroup | Out-Null
    Start-Sleep -Seconds 10
    $identity = az webapp identity show --name $webAppName --resource-group $ResourceGroup --query principalId --output tsv
}

$kvScope = az keyvault show --name $keyVaultName --resource-group $ResourceGroup --query id --output tsv
az role assignment create --assignee $identity --role "Key Vault Secrets User" --scope $kvScope | Out-Null

Write-Host "Managed Identity configured" -ForegroundColor Green

# Step 9: Configure App Settings
Write-Host "Configuring App Settings..." -ForegroundColor Yellow

$kvBlobRef = "@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=BlobStorage--ConnectionString)"
$kvTableRef = "@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=TableStorage--ConnectionString)"

# Use REST API for China Cloud compatibility
$appSettingsBody = @{
    properties = @{
        "ASPNETCORE_ENVIRONMENT" = "Staging"
        "Persistence__UseEf" = "false"
        "Persistence__UseTableStorage" = "true"
        "BlobStorage__ConnectionString" = $kvBlobRef
        "BlobStorage__ContainerName" = $containerName
        "TableStorage__ConnectionString" = $kvTableRef
    }
} | ConvertTo-Json -Depth 3

$uri = "https://management.chinacloudapi.cn/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.Web/sites/$webAppName/config/appsettings?api-version=2022-03-01"
az rest --method PUT --uri $uri --body $appSettingsBody --headers "Content-Type=application/json" | Out-Null

Write-Host "App settings configured" -ForegroundColor Green

# Step 10: Build and Deploy
Write-Host "Building application..." -ForegroundColor Yellow

if (Test-Path "publish-staging") { Remove-Item -Recurse -Force "publish-staging" }
if (Test-Path "deploy-staging.zip") { Remove-Item -Force "deploy-staging.zip" }

$buildOutput = dotnet publish src/FileService.Api/FileService.Api.csproj -c Release -o publish-staging 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed: $buildOutput"
    exit 1
}

Write-Host "Build completed" -ForegroundColor Green

Write-Host "Creating deployment package..." -ForegroundColor Yellow
Push-Location publish-staging
Compress-Archive -Path * -DestinationPath ../deploy-staging.zip -Force
Pop-Location

Write-Host "Deploying to Azure..." -ForegroundColor Yellow
az webapp deploy --resource-group $ResourceGroup --name $webAppName --src-path deploy-staging.zip --type zip | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Host "Deployment completed successfully!" -ForegroundColor Green
} else {
    Write-Warning "Deployment may have issues. Check web app logs."
}

# Cleanup
Remove-Item -Recurse -Force publish-staging -ErrorAction SilentlyContinue
Remove-Item -Force deploy-staging.zip -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "=== Deployment Complete ===" -ForegroundColor Cyan
Write-Host "Web App URL: https://$webAppName.chinacloudsites.cn" -ForegroundColor White
Write-Host "Swagger URL: https://$webAppName.chinacloudsites.cn/swagger" -ForegroundColor White
Write-Host ""
Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "- Using Azure Table Storage for metadata" -ForegroundColor White  
Write-Host "- Using Azure Blob Storage for files" -ForegroundColor White
Write-Host "- Managed Identity authentication" -ForegroundColor White
Write-Host ""
Write-Host "To monitor logs:" -ForegroundColor Yellow
Write-Host "az webapp log tail --name $webAppName --resource-group $ResourceGroup" -ForegroundColor White