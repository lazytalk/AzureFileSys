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

$ErrorActionPreference = 'Stop'

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

# Check if logged in to Azure
try {
    $accountInfo = az account show 2>&1 | ConvertFrom-Json
    Write-Host "✓ Authenticated as: $($accountInfo.user.name)" -ForegroundColor Green
    Write-Host "✓ Current subscription: $($accountInfo.name) ($($accountInfo.id))" -ForegroundColor Green
    
    # Check if using Azure China Cloud (optional warning)
    $currentCloud = az cloud show --query name -o tsv 2>$null
    if ($currentCloud -ne "AzureChinaCloud") {
        Write-Warning "Not using Azure China Cloud. If deploying to China, run: az cloud set --name AzureChinaCloud"
    } else {
        Write-Host "✓ Using Azure China Cloud" -ForegroundColor Green
    }
} catch {
    Write-Error "Not logged in to Azure CLI. Please run: az login"
    $login = Read-Host "Would you like to login now? (y/n)"
    if ($login -eq 'y') {
        az login
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Azure login failed"
            exit 1
        }
    } else {
        exit 1
    }
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

# ============================================================================
# SECTION 4: CREATE AZURE RESOURCES (if requested)
# ============================================================================

if ($CreateResources) {
    Write-Host "📦 Creating Azure resources for $Environment environment..." -ForegroundColor Yellow
    
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
    Write-Host "Creating resource group: $resourceGroup"
    az group create -n $resourceGroup -l $resources["Location"]
    
    # Create Application Insights
    Write-Host "Creating Application Insights: $appInsightsName"
    az monitor app-insights component create -a $appInsightsName -g $resourceGroup -l $resources["Location"] --application-type web
    $aiKey = az monitor app-insights component show -a $appInsightsName -g $resourceGroup --query instrumentationKey -o tsv
    
    # Create storage account
    Write-Host "Creating storage account: $storageAccount"
    if ($env.IsProduction) {
        az storage account create -n $storageAccount -g $resourceGroup -l $resources["Location"] --sku Standard_GRS --kind StorageV2 --enable-versioning true
        az storage container create --account-name $storageAccount -n "userfiles-$envLabel" --auth-mode key --public-access off
        az storage account blob-service-properties update --account-name $storageAccount --enable-delete-retention true --delete-retention-days 30
    } else {
        az storage account create -n $storageAccount -g $resourceGroup -l $resources["Location"] --sku Standard_LRS --kind StorageV2
        az storage container create --account-name $storageAccount -n "userfiles-$envLabel" --auth-mode key --public-access off
    }
    $storageConnString = az storage account show-connection-string -n $storageAccount -g $resourceGroup -o tsv
    
    # Create SQL Server and Database
    Write-Host "Creating Azure SQL Database: $sqlServerName"
    az sql server create -n $sqlServerName -g $resourceGroup -l $resources["Location"] --admin-user $sqlAdminUser --admin-password $sqlAdminPassword
    az sql db create -s $sqlServerName -g $resourceGroup -n $sqlDbName --service-objective $sqlTier
    az sql server firewall-rule create -s $sqlServerName -g $resourceGroup -n AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
    
    if ($env.IsProduction) {
        az sql db update -s $sqlServerName -n $sqlDbName -g $resourceGroup --backup-storage-redundancy Zone
    }
    
    $sqlConnString = az sql db show-connection-string -s $sqlServerName -n $sqlDbName -c ado.net | ForEach-Object { $_ -replace '<username>', $sqlAdminUser -replace '<password>', $sqlAdminPassword }
    
    # Create Key Vault
    Write-Host "Creating Key Vault: $keyVaultName"
    az keyvault create -n $keyVaultName -g $resourceGroup -l $resources["Location"] --enable-soft-delete true --enable-purge-protection true
    az keyvault secret set --vault-name $keyVaultName -n "BlobStorage__ConnectionString" --value $storageConnString
    az keyvault secret set --vault-name $keyVaultName -n "Sql__ConnectionString" --value $sqlConnString
    az keyvault secret set --vault-name $keyVaultName -n "ApplicationInsights__InstrumentationKey" --value $aiKey
    az keyvault secret set --vault-name $keyVaultName -n "PowerSchool__BaseUrl" --value $resources["PowerSchoolBaseUrl"]
    az keyvault secret set --vault-name $keyVaultName -n "PowerSchool__ApiKey" --value "$envLabel-api-key-placeholder"
    
    # Create App Service
    Write-Host "Creating App Service: $webAppName"
    az appservice plan create -n $appServicePlanName -g $resourceGroup --sku $appServiceSku --is-linux false
    az webapp create -n $webAppName -g $resourceGroup -p $appServicePlanName --runtime "DOTNET|9.0"
    
    # Configure Managed Identity
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
    $settings += ("BlobStorage__ConnectionString=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=BlobStorage__ConnectionString)")
    $settings += ("Sql__ConnectionString=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=Sql__ConnectionString)")
    $settings += ("ApplicationInsights__InstrumentationKey=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=ApplicationInsights__InstrumentationKey)")
    $settings += ("PowerSchool__BaseUrl=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=PowerSchool__BaseUrl)")
    $settings += ("PowerSchool__ApiKey=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=PowerSchool__ApiKey)")
    az webapp config appsettings set -n $webAppName -g $resourceGroup --settings $settings
    
    # Security Settings
    Write-Host "Applying security settings..."
    if ($env.IsProduction) {
        az webapp config set -n $webAppName -g $resourceGroup --https-only true --min-tls-version 1.2 --ftps-state Disabled
    } else {
        az webapp config set -n $webAppName -g $resourceGroup --https-only true --min-tls-version 1.2
    }
    
    Write-Host "✅ $Environment resources created successfully!" -ForegroundColor Green
    Write-Host "$Environment URL: https://$($resources['WebAppName']).azurewebsites.net" -ForegroundColor Green
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
    
    # Create migration bundle
    Write-Host "Creating migration bundle..."
    dotnet ef migrations bundle -p src/FileService.Infrastructure/FileService.Infrastructure.csproj -s src/FileService.Api/FileService.Api.csproj -o "$publishDir/efbundle.exe" --verbose
    
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

Write-Host ""
Write-Host "🎉 $Environment deployment completed!" -ForegroundColor Green
Write-Host "$Environment URL: https://$($resources['WebAppName']).azurewebsites.net/swagger" -ForegroundColor Cyan
$webAppName = $resources['WebAppName']
$resourceGroup = $resources['ResourceGroup']
Write-Host "Monitor logs: az webapp log tail -n $webAppName -g $resourceGroup" -ForegroundColor Gray

