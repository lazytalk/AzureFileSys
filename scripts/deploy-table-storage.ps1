#!/usr/bin/env pwsh
# deploy-table-storage.ps1 - Deploy File Service to Azure using Table Storage

param(
    [string]$SubscriptionId,
    [string]$Location = "chinaeast",
    [string]$ResourcePrefix = "filesvc-stg",
    [string]$ResourceGroup = "${ResourcePrefix}-rg"
)

# Build service names
$storageAccount = ($ResourcePrefix -replace '-', '') + "stg" + (Get-Random -Minimum 1000 -Maximum 9999)
$storageAccount = ($storageAccount.ToLower() -replace '[^a-z0-9]', '').Substring(0, [Math]::Min(24, $storageAccount.Length))
$keyVaultName = "${ResourcePrefix}-kv".ToLower() -replace '[^a-z0-9-]', '-'
$appInsightsName = "${ResourcePrefix}-ai"
$webAppName = "${ResourcePrefix}-app"
$appServicePlan = "${ResourcePrefix}-plan"
$containerName = "userfiles-staging"

Write-Host "=== Azure File Service Deployment (Table Storage) ===" -ForegroundColor Cyan

# Step 0: Azure Cloud and Authentication Setup
Write-Host "Setting up Azure Cloud and authentication..." -ForegroundColor Yellow

# ALWAYS set Azure cloud to China first (critical for API compatibility)
Write-Host "Setting Azure cloud to China..." -ForegroundColor Gray
az cloud set --name AzureChinaCloud

# Check if logged in
$currentUser = az account show --query user.name --output tsv 2>$null
if (-not $currentUser) {
    Write-Host "Please log in to Azure..." -ForegroundColor Gray
    az login
    $currentUser = az account show --query user.name --output tsv
}

Write-Host "Logged in as: $currentUser" -ForegroundColor Green

# Auto-detect subscription if not provided
if (-not $SubscriptionId) {
    Write-Host "Auto-detecting subscription..." -ForegroundColor Gray
    $SubscriptionId = az account show --query id --output tsv
    if (-not $SubscriptionId) {
        Write-Error "Could not determine subscription ID. Please provide -SubscriptionId parameter."
        exit 1
    }
}

# Set subscription
az account set --subscription $SubscriptionId
$subscriptionName = az account show --query name --output tsv
Write-Host "Using subscription: $subscriptionName ($SubscriptionId)" -ForegroundColor Green

Write-Host "Subscription: $SubscriptionId" -ForegroundColor Gray
Write-Host "Location: $Location" -ForegroundColor Gray
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor Gray
Write-Host "Storage Account: $storageAccount" -ForegroundColor Gray
Write-Host "Key Vault: $keyVaultName" -ForegroundColor Gray
Write-Host "Web App: $webAppName" -ForegroundColor Gray
Write-Host ""

# Helper functions
function Test-ResourceExists {
    param([string]$ResourceId)
    try {
        $result = az resource show --ids $ResourceId 2>$null
        return $result -ne $null
    }
    catch {
        return $false
    }
}

function Test-ResourceGroupExists {
    param([string]$RgName)
    try {
        $result = az group exists --name $RgName
        return $result -eq "true"
    }
    catch {
        return $false
    }
}

function Test-StorageAccountExists {
    param([string]$StorageName, [string]$RgName)
    try {
        $result = az storage account show --name $StorageName --resource-group $RgName 2>$null
        return $result -ne $null
    }
    catch {
        return $false
    }
}

function Test-KeyVaultExists {
    param([string]$VaultName)
    try {
        $result = az keyvault show --name $VaultName 2>$null
        return $result -ne $null
    }
    catch {
        return $false
    }
}

function Test-WebAppExists {
    param([string]$AppName, [string]$RgName)
    try {
        $result = az webapp show --name $AppName --resource-group $RgName 2>$null
        return $result -ne $null
    }
    catch {
        return $false
    }
}

# Setup Azure CLI
Write-Host "Setting up Azure CLI..." -ForegroundColor Yellow
az cloud set --name AzureChinaCloud
az account set --subscription $SubscriptionId

# Check authentication
$currentAccount = az account show 2>$null
if (-not $currentAccount) {
    Write-Host "Not authenticated. Please run 'az login' first." -ForegroundColor Red
    exit 1
}

Write-Host "Authenticated to Azure China Cloud" -ForegroundColor Green

# Register required providers
Write-Host "Registering Azure providers..." -ForegroundColor Yellow
$providers = @("Microsoft.Storage", "Microsoft.KeyVault", "Microsoft.Web", "Microsoft.Insights")
foreach ($provider in $providers) {
    $regState = az provider show --namespace $provider --query registrationState -o tsv 2>$null
    if ($regState -ne "Registered") {
        Write-Host "Registering $provider..." -ForegroundColor Gray
        az provider register --namespace $provider | Out-Null
    } else {
        Write-Host "$provider already registered" -ForegroundColor Green
    }
}

# Step 1: Resource Group
Write-Host "Checking Resource Group..." -ForegroundColor Yellow
if (Test-ResourceGroupExists -RgName $ResourceGroup) {
    Write-Host "Resource group '$ResourceGroup' already exists" -ForegroundColor Green
} else {
    Write-Host "Creating resource group '$ResourceGroup'..." -ForegroundColor Gray
    az group create --name $ResourceGroup --location $Location | Out-Null
    Write-Host "Resource group created" -ForegroundColor Green
}

# Step 2: Storage Account
Write-Host "Checking Storage Account..." -ForegroundColor Yellow
$storageConnString = ""
if (Test-StorageAccountExists -StorageName $storageAccount -RgName $ResourceGroup) {
    Write-Host "Storage account '$storageAccount' already exists" -ForegroundColor Green
    $storageConnString = az storage account show-connection-string --name $storageAccount --resource-group $ResourceGroup --output tsv
} else {
    Write-Host "Creating storage account '$storageAccount'..." -ForegroundColor Gray
    az storage account create `
        --name $storageAccount `
        --resource-group $ResourceGroup `
        --location $Location `
        --sku Standard_LRS `
        --kind StorageV2 | Out-Null
    
    # Create blob container
    az storage container create `
        --account-name $storageAccount `
        --name $containerName `
        --auth-mode key `
        --public-access off | Out-Null
    
    $storageConnString = az storage account show-connection-string --name $storageAccount --resource-group $ResourceGroup --output tsv
    Write-Host "Storage account created" -ForegroundColor Green
}

# Ensure blob container exists
$containerExists = az storage container exists --account-name $storageAccount --name $containerName --auth-mode key --output tsv 2>$null
if ($containerExists -ne "true") {
    Write-Host "Creating blob container '$containerName'..." -ForegroundColor Gray
    az storage container create --account-name $storageAccount --name $containerName --auth-mode key --public-access off | Out-Null
}

# Step 3: Application Insights (hardcoded to chinaeast2)
Write-Host "Checking Application Insights..." -ForegroundColor Yellow
$aiKey = ""
$aiExists = $false
try {
    $aiResult = az monitor app-insights component show --app $appInsightsName --resource-group $ResourceGroup 2>$null
    if ($aiResult) {
        $aiExists = $true
        $aiKey = az monitor app-insights component show --app $appInsightsName --resource-group $ResourceGroup --query instrumentationKey --output tsv
        Write-Host "Application Insights '$appInsightsName' already exists" -ForegroundColor Green
    }
}
catch {
    $aiExists = $false
}

if (-not $aiExists) {
    Write-Host "Creating Application Insights '$appInsightsName' in chinaeast2..." -ForegroundColor Gray
    # Install extension if needed
    $extensions = az extension list --query "[?name=='application-insights'].name" -o tsv 2>$null
    if (-not $extensions -or $extensions -notcontains "application-insights") {
        az extension add --name application-insights --allow-preview | Out-Null
    }
    
    az monitor app-insights component create `
        --app $appInsightsName `
        --location "chinaeast2" `
        --resource-group $ResourceGroup `
        --application-type web | Out-Null
    
    $aiKey = az monitor app-insights component show --app $appInsightsName --resource-group $ResourceGroup --query instrumentationKey --output tsv
    Write-Host "Application Insights created" -ForegroundColor Green
}

# Step 4: Key Vault
Write-Host "Checking Key Vault..." -ForegroundColor Yellow
if (Test-KeyVaultExists -VaultName $keyVaultName) {
    Write-Host "Key Vault '$keyVaultName' already exists" -ForegroundColor Green
} else {
    Write-Host "Creating Key Vault '$keyVaultName'..." -ForegroundColor Gray
    az keyvault create `
        --name $keyVaultName `
        --resource-group $ResourceGroup `
        --location $Location | Out-Null
    
    # Wait for Key Vault to be ready
    Write-Host "Waiting for Key Vault to be ready..." -ForegroundColor Gray
    $kvReady = $false
    $attempts = 0
    while (-not $kvReady -and $attempts -lt 12) {
        Start-Sleep -Seconds 5
        $attempts++
        try {
            $kvUri = az keyvault show --name $keyVaultName --resource-group $ResourceGroup --query properties.vaultUri --output tsv 2>$null
            if ($kvUri) { 
                $kvReady = $true 
                Write-Host "Key Vault is ready" -ForegroundColor Green
            } else {
                Write-Host "Waiting for Key Vault... (attempt $attempts/12)" -ForegroundColor Gray
            }
        }
        catch {
            Write-Host "Waiting for Key Vault... (attempt $attempts/12)" -ForegroundColor Gray
        }
    }
    
    if (-not $kvReady) {
        Write-Error "Key Vault failed to become ready after waiting. Check permissions and provider registration."
        exit 1
    }
}

# Step 5: Set Key Vault Secrets
Write-Host "Setting Key Vault secrets..." -ForegroundColor Yellow

# Check and set storage connection string
$blobSecretExists = $false
try {
    az keyvault secret show --vault-name $keyVaultName --name "BlobStorage--ConnectionString" 2>$null | Out-Null
    $blobSecretExists = $true
    Write-Host "BlobStorage connection string secret already exists" -ForegroundColor Green
}
catch {
    az keyvault secret set --vault-name $keyVaultName --name "BlobStorage--ConnectionString" --value $storageConnString | Out-Null
    Write-Host "BlobStorage connection string secret created" -ForegroundColor Green
}

# Check and set table storage connection string
$tableSecretExists = $false
try {
    az keyvault secret show --vault-name $keyVaultName --name "TableStorage--ConnectionString" 2>$null | Out-Null
    $tableSecretExists = $true
    Write-Host "TableStorage connection string secret already exists" -ForegroundColor Green
}
catch {
    az keyvault secret set --vault-name $keyVaultName --name "TableStorage--ConnectionString" --value $storageConnString | Out-Null
    Write-Host "TableStorage connection string secret created" -ForegroundColor Green
}

# Check and set Application Insights key
$aiSecretExists = $false
try {
    az keyvault secret show --vault-name $keyVaultName --name "ApplicationInsights--InstrumentationKey" 2>$null | Out-Null
    $aiSecretExists = $true
    Write-Host "Application Insights key secret already exists" -ForegroundColor Green
}
catch {
    az keyvault secret set --vault-name $keyVaultName --name "ApplicationInsights--InstrumentationKey" --value $aiKey | Out-Null
    Write-Host "Application Insights key secret created" -ForegroundColor Green
}

# Step 6: App Service Plan
Write-Host "Checking App Service Plan..." -ForegroundColor Yellow

# Check if App Service Plan exists
$planId = az appservice plan show --name $appServicePlan --resource-group $ResourceGroup --query id --output tsv 2>$null
if ($planId) {
    Write-Host "App Service Plan '$appServicePlan' already exists" -ForegroundColor Green
} else {
    Write-Host "Creating App Service Plan..." -ForegroundColor Yellow
    Write-Host "Plan Name: $appServicePlan" -ForegroundColor Gray
    Write-Host "Resource Group: $ResourceGroup" -ForegroundColor Gray
    Write-Host "SKU: B1 (Basic, 1 core, 1.75GB RAM)" -ForegroundColor Gray
    
    $planResult = az appservice plan create `
        --name $appServicePlan `
        --resource-group $ResourceGroup `
        --sku B1 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create App Service Plan: $planResult"
        Write-Host "Attempting with minimal parameters..." -ForegroundColor Yellow
        
        # Try again with just the essential parameters
        $planResult2 = az appservice plan create `
            --name $appServicePlan `
            --resource-group $ResourceGroup `
            --sku B1 2>&1
            
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to create App Service Plan (second attempt): $planResult2"
            exit 1
        }
    }
    
    # Verify the plan was created
    Write-Host "Verifying App Service Plan creation..." -ForegroundColor Gray
    Start-Sleep -Seconds 10
    $planId = az appservice plan show --name $appServicePlan --resource-group $ResourceGroup --query id --output tsv 2>$null
    if ($planId) {
        Write-Host "App Service Plan created successfully" -ForegroundColor Green
    } else {
        Write-Error "App Service Plan creation failed - plan not found after creation attempt"
        exit 1
    }
}

# Step 7: Web App
Write-Host "Checking Web App..." -ForegroundColor Yellow
if (Test-WebAppExists -AppName $webAppName -RgName $ResourceGroup) {
    Write-Host "Web App '$webAppName' already exists" -ForegroundColor Green
} else {
    Write-Host "Creating Web App..." -ForegroundColor Yellow
    
    # Double-check that the app service plan exists before creating web app
    try {
        $planCheck = az appservice plan show --name $appServicePlan --resource-group $ResourceGroup --query id --output tsv 2>$null
        if (-not $planCheck) {
            Write-Error "App Service Plan '$appServicePlan' not found before web app creation. Plan creation may have failed silently."
            exit 1
        }
    }
    catch {
        Write-Error "Cannot verify App Service Plan '$appServicePlan' exists. Plan creation may have failed."
        exit 1
    }
    
    $webAppResult = az webapp create `
        --name $webAppName `
        --resource-group $ResourceGroup `
        --plan $appServicePlan `
        --runtime "dotnet:8" 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create web app: $webAppResult"
        exit 1
    }
    Write-Host "Web App created successfully" -ForegroundColor Green
}

# Step 8: Configure Managed Identity and Permissions
Write-Host "Configuring Managed Identity and Permissions..." -ForegroundColor Yellow

# Enable managed identity
$identity = az webapp identity show --name $webAppName --resource-group $ResourceGroup --query principalId --output tsv 2>$null
if (-not $identity) {
    Write-Host "Assigning managed identity..." -ForegroundColor Gray
    az webapp identity assign --name $webAppName --resource-group $ResourceGroup | Out-Null
    Start-Sleep -Seconds 10  # Wait for identity to propagate
    $identity = az webapp identity show --name $webAppName --resource-group $ResourceGroup --query principalId --output tsv
}

$principalId = $identity
Write-Host "Managed Identity Principal ID: $principalId" -ForegroundColor Gray

# Set Key Vault access (using RBAC instead of access policy)
Write-Host "Setting Key Vault access permissions..." -ForegroundColor Gray
$kvScope = az keyvault show --name $keyVaultName --resource-group $ResourceGroup --query id --output tsv
az role assignment create `
    --assignee $principalId `
    --role "Key Vault Secrets User" `
    --scope $kvScope | Out-Null

# Set storage permissions
Write-Host "Setting storage permissions..." -ForegroundColor Gray
$storageId = az storage account show --name $storageAccount --resource-group $ResourceGroup --query id --output tsv

# Check if role assignments already exist
$blobRoleExists = az role assignment list --assignee $principalId --scope $storageId --role "Storage Blob Data Contributor" --query "[0].id" -o tsv 2>$null
if (-not $blobRoleExists) {
    az role assignment create --assignee $principalId --role "Storage Blob Data Contributor" --scope $storageId | Out-Null
    Write-Host "Storage Blob Data Contributor role assigned" -ForegroundColor Green
} else {
    Write-Host "Storage Blob Data Contributor role already assigned" -ForegroundColor Green
}

$tableRoleExists = az role assignment list --assignee $principalId --scope $storageId --role "Storage Table Data Contributor" --query "[0].id" -o tsv 2>$null
if (-not $tableRoleExists) {
    az role assignment create --assignee $principalId --role "Storage Table Data Contributor" --scope $storageId | Out-Null
    Write-Host "Storage Table Data Contributor role assigned" -ForegroundColor Green
} else {
    Write-Host "Storage Table Data Contributor role already assigned" -ForegroundColor Green
}

# Step 9: Configure App Settings
Write-Host "Configuring App Settings..." -ForegroundColor Yellow

# Build Key Vault reference strings
$kvBlobRef = "@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=BlobStorage--ConnectionString)"
$kvTableRef = "@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=TableStorage--ConnectionString)"
$kvAiRef = "@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=ApplicationInsights--InstrumentationKey)"

# Use REST API for app settings (Azure China CLI compatibility)
Write-Host "Setting app configuration via REST API..." -ForegroundColor Gray
$appSettingsBody = @{
    properties = @{
        "ASPNETCORE_ENVIRONMENT" = "Staging"
        "EnvironmentMode" = "Staging"
        "BlobStorage__UseLocalStub" = "false"
        "BlobStorage__ConnectionString" = $kvBlobRef
        "BlobStorage__ContainerName" = $containerName
        "Persistence__UseEf" = "false"
        "Persistence__UseTableStorage" = "true"
        "TableStorage__ConnectionString" = $kvTableRef
        "ApplicationInsights__InstrumentationKey" = $kvAiRef
    }
} | ConvertTo-Json -Depth 3

$appSettingsUri = "https://management.chinacloudapi.cn/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.Web/sites/$webAppName/config/appsettings?api-version=2022-03-01"

az rest --method PUT --uri $appSettingsUri --body $appSettingsBody | Out-Null

Write-Host "App settings configured successfully" -ForegroundColor Green

# Restart app to apply new settings
Write-Host "Restarting web app to apply settings..." -ForegroundColor Gray
az webapp restart --name $webAppName --resource-group $ResourceGroup | Out-Null

# Configure security settings
az webapp update `
    --name $webAppName `
    --resource-group $ResourceGroup `
    --https-only true `
    --set httpsOnly=true | Out-Null

Write-Host "App settings configured" -ForegroundColor Green

# Step 10: Build and Deploy Application
Write-Host "Building and deploying application..." -ForegroundColor Yellow

# Build and publish
Write-Host "Building application..." -ForegroundColor Gray
dotnet publish src/FileService.Api/FileService.Api.csproj -c Release -o publish-staging --verbosity quiet

if (-not (Test-Path "publish-staging")) {
    Write-Error "Build failed - publish directory not found"
    exit 1
}

# Create deployment package
Write-Host "Creating deployment package..." -ForegroundColor Gray
Push-Location publish-staging
Compress-Archive -Path * -DestinationPath ../deploy-staging.zip -Force
Pop-Location

if (-not (Test-Path "deploy-staging.zip")) {
    Write-Error "Failed to create deployment package"
    exit 1
}

# Deploy to Azure
Write-Host "Deploying to Azure App Service..." -ForegroundColor Gray
$deployResult = az webapp deploy --resource-group $ResourceGroup --name $webAppName --src-path deploy-staging.zip --type zip 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Warning "Deployment may have encountered issues. Check the web app logs."
    Write-Host $deployResult -ForegroundColor Yellow
} else {
    Write-Host "Application deployed successfully" -ForegroundColor Green
}

# Cleanup
Write-Host "Cleaning up temporary files..." -ForegroundColor Gray
Remove-Item -Recurse -Force publish-staging -ErrorAction SilentlyContinue
Remove-Item -Force deploy-staging.zip -ErrorAction SilentlyContinue

# Final summary
Write-Host ""
Write-Host "=== Deployment Complete ===" -ForegroundColor Green
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor Cyan
Write-Host "Storage Account: $storageAccount" -ForegroundColor Cyan
Write-Host "Key Vault: $keyVaultName" -ForegroundColor Cyan
Write-Host "Web App: $webAppName" -ForegroundColor Cyan
Write-Host ""
Write-Host "URLs:" -ForegroundColor White
Write-Host "  Web App: https://$webAppName.chinacloudsites.cn" -ForegroundColor White
Write-Host "  Swagger: https://$webAppName.chinacloudsites.cn/swagger" -ForegroundColor White
Write-Host ""
Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  - Using Azure Table Storage for metadata" -ForegroundColor White
Write-Host "  - Using Azure Blob Storage for file storage" -ForegroundColor White
Write-Host "  - Managed Identity configured for Key Vault and Storage access" -ForegroundColor White
Write-Host "  - Application Insights enabled" -ForegroundColor White
Write-Host ""
Write-Host "To monitor:" -ForegroundColor Yellow
Write-Host "  az webapp log tail --name $webAppName --resource-group $ResourceGroup" -ForegroundColor White