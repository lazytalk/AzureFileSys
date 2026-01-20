#!/usr/bin/env pwsh
# deploy.ps1 - Unified deployment script for staging and production environments

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Staging", "Production")]
    [string]$Environment,
    
    [switch]$CreateResources,
    [switch]$DeployApp,
    [switch]$RunMigrations,
    [switch]$PromoteFromStaging,
    [string]$SubscriptionId = "",
    [string]$Location = "",
    [string]$SqlAdminPassword = ""
)

Set-Variable -Name ErrorActionPreference -Value 'Stop' -Scope Script

# Set UTF-8 encoding for better compatibility (must be after param)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

# ============================================================================
# ENVIRONMENT CONFIGURATION
# ============================================================================

$config = @{
    Staging = @{
        Color = "Cyan"
        Emoji = "🧪"
        IsProduction = $false
    }
    Production = @{
        Color = "Magenta"
        Emoji = "🚀"
        IsProduction = $true
    }
}

$env = $config[$Environment]

# ============================================================================
# SECTION 1: CHECK AZURE CLI PREREQUISITES
# ============================================================================

Write-Host "Checking Azure CLI prerequisites..." -ForegroundColor $env.Color

# Check if Azure CLI is installed
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI is not installed. Please install from https://aka.ms/azure-cli"
    exit 1
}

# Set Azure China Cloud
Write-Host "Setting Azure China Cloud..." -ForegroundColor $env.Color

# Unset environment variable that might override the cloud setting
if (Test-Path Env:\AZURE_CLOUD_NAME) { Remove-Item Env:\AZURE_CLOUD_NAME }

az cloud set --name AzureChinaCloud
$currentCloud = az cloud show --query name -o tsv

if ($currentCloud -eq "AzureChinaCloud") {
    Write-Host "✓ Azure China Cloud configured" -ForegroundColor Green
} else {
    Write-Error "Failed to set Azure China Cloud. Current cloud: $currentCloud"
    exit 1
}

# Check if logged in to Azure
try {
    $accountInfo = az account show -o json | ConvertFrom-Json
    Write-Host "✓ Authenticated as: $($accountInfo.user.name)" -ForegroundColor Green
    Write-Host "✓ Current subscription: $($accountInfo.name) ($($accountInfo.id))" -ForegroundColor Green
} catch {
    Write-Host "Not logged in to Azure CLI. Initiating login..." -ForegroundColor Yellow
    az login
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Azure login failed"
        exit 1
    }
    
    # Verify login succeeded
    $accountInfo = az account show -o json | ConvertFrom-Json
    Write-Host "✓ Authenticated as: $($accountInfo.user.name)" -ForegroundColor Green
}

Write-Host "" # Empty line for readability

# ============================================================================
# SECTION 2: INITIALIZE DEPLOYMENT CONTEXT
# ============================================================================

# Load unified configuration (resources + app settings)
$config = & (Join-Path $PSScriptRoot "deploy-settings.ps1") -Environment $Environment -SqlAdminPassword $SqlAdminPassword
$resources = $config.Resources
$appSettings = $config.AppSettings

# Apply Location override if provided via command line
if (-not [string]::IsNullOrWhiteSpace($Location)) {
    $resources["Location"] = $Location
}

# Set subscription if provided
if (-not [string]::IsNullOrWhiteSpace($SubscriptionId)) {
    Write-Host "Setting Azure subscription: $SubscriptionId"
    az account set --subscription $SubscriptionId
}

# ============================================================================
# SECTION 3: PRINT DEPLOYMENT BANNER
# ============================================================================

Write-Host "$($env.Emoji) File Service - $Environment Deployment" -ForegroundColor $env.Color
Write-Host ("=" * (20 + $Environment.Length)) -ForegroundColor $env.Color

# Helper to check if a resource exists to avoid redundant creation calls
function Test-ResourceExists {
    param($Command)
    $ErrorActionPreference = "SilentlyContinue"
    try {
        Invoke-Expression $Command | Out-Null
        return ($LASTEXITCODE -eq 0)
    } catch {
        return $false
    }
    finally {
        $ErrorActionPreference = "Stop"
    }
}

# ============================================================================
# SECTION 4: CREATE AZURE RESOURCES (if requested)
# ============================================================================

if ($CreateResources) {
    Write-Host "📦 Ensuring Azure resources for $Environment environment..." -ForegroundColor Yellow
    
    $resourceGroup = $resources["ResourceGroup"]
    $storageAccount = $resources["StorageAccount"]
    $webAppName = $resources["WebAppName"]
    $keyVaultName = $resources["KeyVaultName"]
    $appInsightsName = $resources["AppInsightsName"]
    $sqlServerName = $resources["SqlServerName"]
    $sqlDbName = $resources["SqlDbName"]
    $sqlAdminUser = $resources["SqlAdminUser"]
    $sqlAdminPassword = $resources["SqlAdminPassword"]
    $sqlTier = $resources["SqlTier"]
    $appServiceSku = $resources["AppServiceSku"]
    $appServicePlanName = $resources["AppServicePlanName"]
    $envLabel = $resources["EnvLabel"]
    
    # Create resource group
    if (Test-ResourceExists "az group show -n $resourceGroup") {
        Write-Host "Using existing resource group: $resourceGroup" -ForegroundColor Gray
    } else {
        Write-Host "Creating resource group: $resourceGroup"
        az group create -n $resourceGroup -l $resources["Location"]
    }

    # Create Application Insights
    if (Test-ResourceExists "az monitor app-insights component show -a $appInsightsName -g $resourceGroup") {
        Write-Host "Using existing Application Insights: $appInsightsName" -ForegroundColor Gray
    } else {
        Write-Host "Creating Application Insights: $appInsightsName"
        az monitor app-insights component create -a $appInsightsName -g $resourceGroup -l $resources["Location"] --application-type web
    }
    $aiKey = az monitor app-insights component show -a $appInsightsName -g $resourceGroup --query instrumentationKey -o tsv
    
    # Create storage account
    if (Test-ResourceExists "az storage account show -n $storageAccount -g $resourceGroup") {
        Write-Host "Using existing storage account: $storageAccount" -ForegroundColor Gray
    } else {
        Write-Host "Creating storage account: $storageAccount"
        if ($env.IsProduction) {
            az storage account create -n $storageAccount -g $resourceGroup -l $resources["Location"] --sku Standard_GRS --kind StorageV2 --enable-versioning true
            az storage container create --account-name $storageAccount -n "userfiles-$envLabel" --auth-mode key --public-access off
            az storage account blob-service-properties update --account-name $storageAccount --enable-delete-retention true --delete-retention-days 30
        } else {
            az storage account create -n $storageAccount -g $resourceGroup -l $resources["Location"] --sku Standard_LRS --kind StorageV2
            az storage container create --account-name $storageAccount -n "userfiles-$envLabel" --auth-mode key --public-access off
        }
    }
    $storageConnString = az storage account show-connection-string -n $storageAccount -g $resourceGroup -o tsv
    
    # Create SQL Server and Database
    if (Test-ResourceExists "az sql server show -n $sqlServerName -g $resourceGroup") {
        Write-Host "Using existing SQL Server: $sqlServerName" -ForegroundColor Gray
    } else {
        Write-Host "Creating Azure SQL Database: $sqlServerName"
        az sql server create -n $sqlServerName -g $resourceGroup -l $resources["Location"] --admin-user $sqlAdminUser --admin-password $sqlAdminPassword
        az sql server firewall-rule create -s $sqlServerName -g $resourceGroup -n AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
    }
    
    if (Test-ResourceExists "az sql db show -s $sqlServerName -g $resourceGroup -n $sqlDbName") {
        Write-Host "Using existing SQL Database: $sqlDbName" -ForegroundColor Gray
    } else {
        Write-Host "Creating SQL Database: $sqlDbName"
        az sql db create -s $sqlServerName -g $resourceGroup -n $sqlDbName --service-objective $sqlTier
        
        if ($env.IsProduction) {
            az sql db update -s $sqlServerName -n $sqlDbName -g $resourceGroup --backup-storage-redundancy Zone
        }
    }
    
    $sqlConnString = az sql db show-connection-string -s $sqlServerName -n $sqlDbName -c ado.net | ForEach-Object { $_ -replace '<username>', $sqlAdminUser -replace '<password>', $sqlAdminPassword }
    
    # Create Key Vault
    if (Test-ResourceExists "az keyvault show -n $keyVaultName -g $resourceGroup") {
        Write-Host "Using existing Key Vault: $keyVaultName" -ForegroundColor Gray
    } else {
        Write-Host "Creating Key Vault: $keyVaultName"
        # Note: Purge protection prevented cleanup during development. Removed for non-prod scenarios or handled carefully.
        if ($env.IsProduction) {
            az keyvault create -n $keyVaultName -g $resourceGroup -l $resources["Location"] --enable-purge-protection true
        } else {
            az keyvault create -n $keyVaultName -g $resourceGroup -l $resources["Location"]
        }
    }
    
    # Always update secrets in case they changed
    Write-Host "Updating Key Vault secrets..." -ForegroundColor Gray
    az keyvault secret set --vault-name $keyVaultName -n "BlobStorage--ConnectionString" --value $storageConnString > $null
    az keyvault secret set --vault-name $keyVaultName -n "Sql--ConnectionString" --value $sqlConnString > $null
    az keyvault secret set --vault-name $keyVaultName -n "ApplicationInsights--InstrumentationKey" --value $aiKey > $null
    az keyvault secret set --vault-name $keyVaultName -n "PowerSchool--BaseUrl" --value $resources["PowerSchoolBaseUrl"] > $null
    az keyvault secret set --vault-name $keyVaultName -n "PowerSchool--ApiKey" --value "$envLabel-api-key-placeholder" > $null
    
    # Create App Service
    if (Test-ResourceExists "az appservice plan show -n $appServicePlanName -g $resourceGroup") {
        Write-Host "Using existing App Service Plan: $appServicePlanName" -ForegroundColor Gray
    } else {
        Write-Host "Creating App Service Plan: $appServicePlanName"
        az appservice plan create -n $appServicePlanName -g $resourceGroup --sku $appServiceSku
    }
    
    if (Test-ResourceExists "az webapp show -n $webAppName -g $resourceGroup") {
        Write-Host "Using existing App Service: $webAppName" -ForegroundColor Gray
    } else {
        Write-Host "Creating App Service: $webAppName"
        # Using string explicit quoting to avoid powershell pipe interpretation
        az webapp create -n $webAppName -g $resourceGroup -p $appServicePlanName --runtime "dotnet:9"
    }
    
    # Configure Managed Identity (Idempotent, safe to rerun)
    Write-Host "Configuring Managed Identity..."
    az webapp identity assign -n $webAppName -g $resourceGroup
    $principalId = az webapp identity show -n $webAppName -g $resourceGroup --query principalId -o tsv
    az keyvault set-policy -n $keyVaultName -g $resourceGroup --object-id $principalId --secret-permissions get list
    $storageId = az storage account show -n $storageAccount -g $resourceGroup --query id -o tsv
    az role assignment create --assignee $principalId --role "Storage Blob Data Contributor" --scope $storageId
    
    # Configure App Settings
    Write-Host "Configuring App Settings..."
    $settings = @()
    foreach ($k in $appSettings.Keys) {
        $settings += ("$k=$($appSettings[$k])")
    }
    $settings += ("BlobStorage__ConnectionString=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=BlobStorage--ConnectionString)")
    $settings += ("Sql__ConnectionString=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=Sql--ConnectionString)")
    $settings += ("ApplicationInsights__InstrumentationKey=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=ApplicationInsights--InstrumentationKey)")
    $settings += ("PowerSchool__BaseUrl=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=PowerSchool--BaseUrl)")
    $settings += ("PowerSchool__ApiKey=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=PowerSchool--ApiKey)")
    az webapp config appsettings set -n $webAppName -g $resourceGroup --settings $settings
    
    # Security Settings
    Write-Host "Applying security settings..."
    if ($env.IsProduction) {
        az webapp config set -n $webAppName -g $resourceGroup --min-tls-version 1.2 --ftps-state Disabled
    } else {
        az webapp config set -n $webAppName -g $resourceGroup --min-tls-version 1.2
    }
    
    # Get the actual default host name (handles Azure China .chinacloudsites.cn vs Global .azurewebsites.net)
    $hostName = az webapp show -n $webAppName -g $resourceGroup --query defaultHostName -o tsv
    
    Write-Host "✅ $Environment resources created successfully!" -ForegroundColor Green
    Write-Host "$Environment URL: https://$hostName" -ForegroundColor Green
}

# ============================================================================
# SECTION 5: DEPLOY APPLICATION (if requested)
# ============================================================================

if ($Environment -eq "Production" -and $PromoteFromStaging) {
    Write-Host "🔄 Promoting staging build to production..." -ForegroundColor Yellow
    if (Test-Path "deploy-staging.zip") {
        Copy-Item "deploy-staging.zip" "deploy-production.zip"
        Write-Host "✅ Staging build promoted to production package" -ForegroundColor Green
        
        # Deploy without rebuilding
        Write-Host "Deploying to Azure App Service..."
        az webapp deploy -g $resources["ResourceGroup"] -n $resources["WebAppName"] --src-path "deploy-production.zip" --type zip
    } else {
        Write-Error "Staging deployment package not found. Run staging deployment first."
        exit 1
    }
} elseif ($DeployApp) {
    Write-Host "🚀 Deploying application to $Environment..." -ForegroundColor Yellow
    
    $environmentLabel = $Environment.ToLower()
    $publishDir = "publish-$environmentLabel"
    $deployZip = "deploy-$environmentLabel.zip"
    
    # Build and publish
    Write-Host "Building application..."
    dotnet publish src/FileService.Api/FileService.Api.csproj -c Release -o $publishDir --verbosity quiet
    
    # Run EF Migrations
    Write-Host "Creating migration bundle..."
    $env:Persistence__ForceEf="true"
    dotnet ef migrations bundle -p src/FileService.Infrastructure/FileService.Infrastructure.csproj -s src/FileService.Api/FileService.Api.csproj -o "$publishDir/efbundle.exe" --verbose
    $env:Persistence__ForceEf="false"
    
    # Create deployment package
    Write-Host "Creating deployment package..."
    Set-Location $publishDir
    Compress-Archive -Path * -DestinationPath "../$deployZip" -Force
    Set-Location ..
    
    # Deploy to Azure
    Write-Host "Deploying to Azure App Service..."
    az webapp deploy -g $resources["ResourceGroup"] -n $resources["WebAppName"] --src-path $deployZip --type zip
    
    Write-Host "✅ Application deployed to $Environment!" -ForegroundColor Green
}

# ============================================================================
# SECTION 6: RUN DATABASE MIGRATIONS (if requested)
# ============================================================================

if ($RunMigrations) {
    Write-Host "🗄️ Running database migrations on $Environment..." -ForegroundColor Yellow
    
    try {
        $connectionString = az keyvault secret show --vault-name $resources["KeyVaultName"] --name "Sql__ConnectionString" --query value -o tsv
        if ($connectionString) {
            Write-Host "Running EF migrations..."
            
            $environmentLabel = $Environment.ToLower()
            if (Test-Path "publish-$environmentLabel/efbundle.exe") {
                ./publish-$environmentLabel/efbundle.exe --connection $connectionString
            } else {
                Write-Error "Migration bundle not found. Deploy application first."
                exit 1
            }
            
            Write-Host "✅ Database migrations completed!" -ForegroundColor Green
        } else {
            Write-Warning "Could not retrieve connection string from Key Vault"
        }
    } catch {
        Write-Error "Failed to run migrations: $($_.Exception.Message)"
    }
}

# ============================================================================
# SECTION 7: CLEANUP TEMPORARY ARTIFACTS
# ============================================================================

$environmentLabel = $Environment.ToLower()
$publishDir = "publish-$environmentLabel"
$deployZip = "deploy-$environmentLabel.zip"

if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
if (Test-Path $deployZip) { Remove-Item -Force $deployZip }

# ============================================================================
# SECTION 8: DEPLOYMENT SUMMARY
# ============================================================================

# Fetch hostname one last time to be sure
$finalHostName = az webapp show -n $resources["WebAppName"] -g $resources["ResourceGroup"] --query defaultHostName -o tsv

Write-Host ""
Write-Host "🎉 $Environment deployment completed!" -ForegroundColor Green
Write-Host "$Environment URL: https://$finalHostName/swagger" -ForegroundColor Cyan
$webAppName = $resources['WebAppName']
$resourceGroup = $resources['ResourceGroup']
Write-Host "Monitor logs: az webapp log tail -n $webAppName -g $resourceGroup" -ForegroundColor Gray

